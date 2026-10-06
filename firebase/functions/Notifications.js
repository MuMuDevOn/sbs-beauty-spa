/**
 * notifications.js
 * -----------------------------------------------------------------------
 * This file handles ALL notifications across three channels:
 *
 *   1. IN-APP — the notification centre. Created instantly.
 *
 *   2. EMAIL — booking confirmation, address-disclosure, receipt.
 *              Triggered specifically when a payment completes.
 *              Sent via SendGrid. Has a "queued → sent/failed" lifecycle.
 *
 *   3. PUSH — booking reminders via Firebase Cloud Messaging.
 *
 * All three channels write into ONE `notifications` collection
 * (with a `channel` field) so the Admin Notification Log can show
 * everything in one place.
 *
 * NOTE: Nothing in this backend yet registers the device token from the
 * client. Until that's built, push attempts will fail with "No device
 * token registered" — that's the honest failure mode, not a silent no-op.
 * -----------------------------------------------------------------------
 */

const { onDocumentUpdated } = require("firebase-functions/v2/firestore");
const { onSchedule } = require("firebase-functions/v2/scheduler");
const { onCall, HttpsError } = require("firebase-functions/v2/https");
const { defineSecret } = require("firebase-functions/params");
const admin = require("firebase-admin");
const { DateTime } = require("luxon");
const sgMail = require("@sendgrid/mail");

// Initialize Firebase if not already done.
if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

// The SendGrid API key lives in Firebase secrets, not in this file.
const SENDGRID_API_KEY = defineSecret("SENDGRID_API_KEY");
const FROM_EMAIL = "bookings@sbsbeauty.co.za"; // must be verified in SendGrid
const BUSINESS_TIMEZONE = "Africa/Johannesburg";

// How far before an appointment a reminder should go out,
// and how wide a window the scheduler checks each run.
const REMINDER_HOURS_BEFORE = 24;
const REMINDER_WINDOW_HOURS = 1;

// Helper: throws an error if the caller isn't an admin.
function assertIsAdmin(request) {
  if (!request.auth) throw new HttpsError("unauthenticated", "Must be signed in.");
  if (request.auth.token.admin !== true) throw new HttpsError("permission-denied", "Admin access required.");
}

// ---------------------------------------------------------------------
// Channel senders
// ---------------------------------------------------------------------

// Sends an email and updates the notification doc's status.
async function sendEmail(notificationRef, { to, subject, html }) {
  try {
    sgMail.setApiKey(SENDGRID_API_KEY.value());
    await sgMail.send({ to, from: FROM_EMAIL, subject, html });
    await notificationRef.update({
      status: "sent",
      lastAttemptAt: admin.firestore.FieldValue.serverTimestamp(),
    });
  } catch (err) {
    await notificationRef.update({
      status: "failed",
      lastError: err.message || String(err),
      attempts: admin.firestore.FieldValue.increment(1),
      lastAttemptAt: admin.firestore.FieldValue.serverTimestamp(),
    });
  }
}

// Sends a push notification and updates the notification doc's status.
//
// IMPORTANT: This depends on users/{uid}.fcmToken existing.
// Nothing in this backend writes that field yet — the CLIENT needs to
// save its own token on login. Until that's built, every push will fail
// with "No device token registered."
async function sendPush(notificationRef, { clientId, title, body }) {
  try {
    const userSnap = await db.collection("users").doc(clientId).get();
    const token = userSnap.exists ? userSnap.data().fcmToken : null;

    if (!token) {
      throw new Error("No device token registered for this user.");
    }

    await admin.messaging().send({ token, notification: { title, body } });
    await notificationRef.update({
      status: "sent",
      lastAttemptAt: admin.firestore.FieldValue.serverTimestamp(),
    });
  } catch (err) {
    await notificationRef.update({
      status: "failed",
      lastError: err.message || String(err),
      attempts: admin.firestore.FieldValue.increment(1),
      lastAttemptAt: admin.firestore.FieldValue.serverTimestamp(),
    });
  }
}

// ---------------------------------------------------------------------
// 1. In-app notification centre — booking status changes
// ---------------------------------------------------------------------

// The message shown for each booking status.
const BOOKING_STATUS_MESSAGES = {
  confirmed: "Your booking has been confirmed.",
  completed: "Your appointment is complete — thanks for visiting!",
  cancelled: "Your booking has been cancelled.",
};

// Trigger: fires when a booking's status changes.
exports.onBookingStatusChange = onDocumentUpdated("bookings/{bookingId}", async (event) => {
  const before = event.data.before.data();
  const after = event.data.after.data();

  // Only act if the status actually changed.
  if (before.status === after.status) return;

  const message = BOOKING_STATUS_MESSAGES[after.status];
  if (!message) return;

  // Write an in-app notification.
  await db.collection("notifications").add({
    clientId: after.clientId,
    type: "bookingStatus",
    channel: "inApp",
    relatedId: event.params.bookingId,
    message,
    status: "sent", // in-app writes are immediate — no delivery step to fail
    isRead: false,
    attempts: 0,
    lastError: null,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });
});

// ---------------------------------------------------------------------
// 2. Email — triggered by a payment completing
// ---------------------------------------------------------------------

// Trigger: fires when a payment's status changes to "completed".
exports.onPaymentCompleted = onDocumentUpdated(
  { document: "payments/{paymentId}", secrets: [SENDGRID_API_KEY] },
  async (event) => {
    const before = event.data.before.data();
    const after = event.data.after.data();

    // Only act if the payment just completed.
    if (before.status === after.status || after.status !== "completed") {
      return;
    }

    // Load the related booking.
    const bookingSnap = await db.collection("bookings").doc(after.bookingId).get();
    if (!bookingSnap.exists) return;
    const booking = bookingSnap.data();

    // Get the client's email from Firebase Auth.
    const clientEmail = await getEmailForUser(booking.clientId);

    // Always create an in-app notification.
    await db.collection("notifications").add({
      clientId: booking.clientId,
      type: "paymentCompleted",
      channel: "inApp",
      relatedId: event.params.paymentId,
      message: `Payment of R${after.amount} received — thank you.`,
      status: "sent",
      isRead: false,
      attempts: 0,
      lastError: null,
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    });

    // If no email, we're done.
    if (!clientEmail) return;

    // Deposit confirmation gets the address-disclosure email.
    // Balance/full payment gets a simple receipt.
    const isDepositConfirmation = after.type === "deposit";
    const settingsSnap = await db.collection("businessSettingsPublic").doc("main").get();
    const businessAddress = settingsSnap.exists ? settingsSnap.data().address : "";

    const subject = isDepositConfirmation ? "Your SBS Beauty Spa booking is confirmed" : "Payment received — SBS Beauty Spa";
    const html = isDepositConfirmation
      ? `<p>Hi,</p>
         <p>Your booking for <strong>${escapeHtml(booking.date)} at ${escapeHtml(booking.startTime)}</strong> is confirmed.</p>
         <p>Appointment address: ${escapeHtml(businessAddress)}</p>
         <p>See you then!</p>`
      : `<p>Hi,</p>
         <p>We've received your payment of R${after.amount} for your booking on ${escapeHtml(booking.date)}.</p>
         <p>Thank you.</p>`;

    // Create the email notification record.
    const emailRef = await db.collection("notifications").add({
      clientId: booking.clientId,
      type: "paymentCompleted",
      channel: "email",
      relatedId: event.params.paymentId,
      message: subject,
      status: "queued",
      isRead: true, // email isn't shown in the in-app centre
      attempts: 0,
      lastError: null,
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    });

    // Actually send the email.
    await sendEmail(emailRef, { to: clientEmail, subject, html });
  }
);

// ---------------------------------------------------------------------
// 3. Scheduled booking reminders — runs hourly
// ---------------------------------------------------------------------

// Runs every 60 minutes.
exports.sendBookingReminders = onSchedule("every 60 minutes", async () => {
  const now = DateTime.now().setZone(BUSINESS_TIMEZONE);
  const windowStart = now.plus({ hours: REMINDER_HOURS_BEFORE });
  const windowEnd = windowStart.plus({ hours: REMINDER_WINDOW_HOURS });

  // Bookings are stored as separate date/startTime strings, so we can't
  // query them as one timestamp. Pull today's and tomorrow's bookings
  // and filter in memory.
  const candidateDates = [windowStart.toFormat("yyyy-MM-dd"), windowEnd.toFormat("yyyy-MM-dd")];
  const uniqueDates = [...new Set(candidateDates)];

  const snaps = await Promise.all(
    uniqueDates.map((date) =>
      db
        .collection("bookings")
        .where("date", "==", date)
        .where("status", "in", ["pending", "confirmed"])
        .get()
    )
  );

  // Find bookings due for a reminder.
  const dueBookings = [];
  for (const snap of snaps) {
    for (const doc of snap.docs) {
      const booking = doc.data();
      if (booking.reminderSentAt) continue; // already reminded

      const [hour, minute] = booking.startTime.split(":").map(Number);
      const appointmentAt = DateTime.fromISO(booking.date, { zone: BUSINESS_TIMEZONE }).set({ hour, minute });

      if (appointmentAt >= windowStart && appointmentAt < windowEnd) {
        dueBookings.push({ id: doc.id, ...booking });
      }
    }
  }

  // Send reminders.
  for (const booking of dueBookings) {
    const message = `Reminder: your appointment is tomorrow at ${booking.startTime}.`;

    // In-app notification.
    await db.collection("notifications").add({
      clientId: booking.clientId,
      type: "bookingReminder",
      channel: "inApp",
      relatedId: booking.id,
      message,
      status: "sent",
      isRead: false,
      attempts: 0,
      lastError: null,
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    });

    // Push notification.
    const pushRef = await db.collection("notifications").add({
      clientId: booking.clientId,
      type: "bookingReminder",
      channel: "push",
      relatedId: booking.id,
      message,
      status: "queued",
      isRead: true,
      attempts: 0,
      lastError: null,
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    });
    await sendPush(pushRef, { clientId: booking.clientId, title: "Appointment reminder", body: message });

    // Mark that we've reminded this booking.
    await db.collection("bookings").doc(booking.id).update({
      reminderSentAt: admin.firestore.FieldValue.serverTimestamp(),
    });
  }
});

// ---------------------------------------------------------------------
// Admin: Notification Log screen
// ---------------------------------------------------------------------

/**
 * listNotificationLog
 * Input: { status?, channel? }
 * Output: { notifications }
 *
 * Admin only. Lists all notifications (newest first).
 */
exports.listNotificationLog = onCall(async (request) => {
  assertIsAdmin(request);
  const { status, channel } = request.data || {};

  let query = db.collection("notifications").orderBy("createdAt", "desc").limit(200);
  if (status) query = query.where("status", "==", status);
  if (channel) query = query.where("channel", "==", channel);

  const snap = await query.get();
  return { notifications: snap.docs.map((d) => ({ id: d.id, ...d.data() })) };
});

/**
 * resendNotification
 * Input: { notificationId }
 * Output: { notificationId, status }
 *
 * Admin only. Resends a failed email or push notification.
 * In-app notifications can't be resent (no delivery step).
 */
exports.resendNotification = onCall({ secrets: [SENDGRID_API_KEY] }, async (request) => {
  assertIsAdmin(request);
  const { notificationId } = request.data || {};
  if (!notificationId) throw new HttpsError("invalid-argument", "notificationId is required.");

  const ref = db.collection("notifications").doc(notificationId);
  const snap = await ref.get();
  if (!snap.exists) throw new HttpsError("not-found", "Notification not found.");

  const notification = snap.data();

  // In-app notifications don't have a delivery step.
  if (notification.channel === "inApp") {
    throw new HttpsError("failed-precondition", "In-app notifications don't need resending.");
  }

  // Don't resend something that already sent.
  if (notification.status === "sent") {
    throw new HttpsError("failed-precondition", "This notification already sent successfully.");
  }

  await ref.update({ status: "queued" });

  if (notification.channel === "email") {
    const clientEmail = await getEmailForUser(notification.clientId);
    if (!clientEmail) {
      await ref.update({ status: "failed", lastError: "No email on file for this client." });
      throw new HttpsError("failed-precondition", "No email on file for this client.");
    }
    await sendEmail(ref, { to: clientEmail, subject: notification.message, html: `<p>${escapeHtml(notification.message)}</p>` });
  } else if (notification.channel === "push") {
    await sendPush(ref, { clientId: notification.clientId, title: "SBS Beauty Spa", body: notification.message });
  }

  const updated = await ref.get();
  return { notificationId, status: updated.data().status };
});

// Gets a user's email from Firebase Auth.
async function getEmailForUser(uid) {
  try {
    const user = await admin.auth().getUser(uid);
    return user.email || null;
  } catch {
    return null;
  }
}

// Escapes special HTML characters to prevent XSS.
function escapeHtml(str) {
  return String(str || "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}