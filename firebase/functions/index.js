const { onCall, onRequest, HttpsError } = require("firebase-functions/v2/https");
const { onDocumentUpdated, onDocumentCreated } = require("firebase-functions/v2/firestore");
const { onSchedule } = require("firebase-functions/v2/scheduler");
const admin = require("firebase-admin");
const crypto = require("crypto");

admin.initializeApp();
const db = admin.firestore();

const PAYGATE_SECRET = process.env.PAYGATE_WEBHOOK_SECRET; // set via `firebase functions:secrets:set`
const HOLD_DURATION_MINUTES = 10;

// ---------------------------------------------------------------------------
// AUTH — set a custom claim (role) on the Firebase Auth token when a user
// document is created, so Firestore Security Rules and the client app can
// read request.auth.token.role without an extra Firestore read.
// ---------------------------------------------------------------------------
exports.onUserCreated = onDocumentCreated("users/{uid}", async (event) => {
  const uid = event.params.uid;
  const role = event.data.data().role || "client";
  await admin.auth().setCustomUserClaims(uid, { role });
});

// ---------------------------------------------------------------------------
// DELETION AUDIT LOG — POPIA requires a record that a deletion request was
// actually fulfilled. These write to deletionLog before soft-deleting the
// record itself (status flips, nothing is hard-deleted from Firestore).
// ---------------------------------------------------------------------------
exports.deleteAccount = onCall(async (request) => {
  if (!request.auth) throw new HttpsError("unauthenticated", "Sign in first.");
  const uid = request.auth.uid;
  const { reason } = request.data || {};

  await db.collection("deletionLog").add({
    entityType: "user",
    entityId: uid,
    deletedBy: uid, // account owner requested their own deletion
    deletedAt: admin.firestore.FieldValue.serverTimestamp(),
    reason: reason || null,
  });

  // Soft-delete: preserve booking/payment history for financial audit,
  // but strip personal data per POPIA's data-minimisation requirement.
  await db.collection("users").doc(uid).update({
    status: "deleted",
    firstName: "Deleted",
    lastName: "User",
    email: null,
    phone: null,
    address: null,
  });
  await admin.auth().updateUser(uid, { disabled: true });

  return { success: true };
});

exports.deactivateCatalogueItem = onCall(async (request) => {
  if (!request.auth) throw new HttpsError("unauthenticated", "Sign in first.");
  const claims = (await admin.auth().getUser(request.auth.uid)).customClaims;
  if (claims?.role !== "owner") {
    throw new HttpsError("permission-denied", "Admin only.");
  }

  const { entityType, entityId, reason } = request.data;
  if (!["service", "product"].includes(entityType) || !entityId) {
    throw new HttpsError("invalid-argument", "entityType must be service or product.");
  }

  await db.collection("deletionLog").add({
    entityType,
    entityId,
    deletedBy: request.auth.uid,
    deletedAt: admin.firestore.FieldValue.serverTimestamp(),
    reason: reason || null,
  });

  const collectionName = entityType === "service" ? "services" : "products";
  await db.collection(collectionName).doc(entityId).update({ status: "inactive" });

  return { success: true };
});


// payment. Uses a transaction so two clients can't hold the same slot.
// ---------------------------------------------------------------------------
exports.holdSlot = onCall(async (request) => {
  if (!request.auth) throw new HttpsError("unauthenticated", "Sign in first.");
  const { slotId } = request.data;
  if (!slotId) throw new HttpsError("invalid-argument", "slotId is required.");

  const slotRef = db.collection("availabilitySlots").doc(slotId);

  await db.runTransaction(async (tx) => {
    const slotSnap = await tx.get(slotRef);
    if (!slotSnap.exists) throw new HttpsError("not-found", "Slot does not exist.");

    const slot = slotSnap.data();
    const now = Date.now();
    const stillHeld = slot.status === "held" && slot.holdExpiresAt?.toMillis() > now;

    if (slot.status === "booked" || stillHeld) {
      throw new HttpsError("failed-precondition", "Slot is no longer available.");
    }

    tx.update(slotRef, {
      status: "held",
      heldBy: request.auth.uid,
      holdExpiresAt: admin.firestore.Timestamp.fromMillis(
        now + HOLD_DURATION_MINUTES * 60 * 1000
      ),
    });
  });

  return { slotId, holdMinutes: HOLD_DURATION_MINUTES };
});

// Scheduled cleanup — runs every 5 minutes, releases any slot whose hold has
// expired without a completed payment. This is the mechanism behind the
// "10-minute hold auto-releases" requirement.
exports.releaseExpiredHolds = onSchedule("every 5 minutes", async () => {
  const now = admin.firestore.Timestamp.now();
  const expired = await db
    .collection("availabilitySlots")
    .where("status", "==", "held")
    .where("holdExpiresAt", "<=", now)
    .get();

  const batch = db.batch();
  expired.forEach((doc) => {
    batch.update(doc.ref, {
      status: "open",
      heldBy: admin.firestore.FieldValue.delete(),
      holdExpiresAt: admin.firestore.FieldValue.delete(),
    });
  });
  await batch.commit();
});

// ---------------------------------------------------------------------------
// PAYMENT — initiate a deposit payment for a booking.
// ---------------------------------------------------------------------------
exports.initiatePayment = onCall(async (request) => {
  if (!request.auth) throw new HttpsError("unauthenticated", "Sign in first.");
  const { bookingId, amount } = request.data;
  if (!bookingId || !amount) {
    throw new HttpsError("invalid-argument", "bookingId and amount are required.");
  }

  const paymentRef = db.collection("payments").doc();
  await paymentRef.set({
    clientId: request.auth.uid,
    bookingId,
    orderId: null,
    amount,
    status: "pending",
    method: null,
    transactionReference: null,
    paidAt: null,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });

  // Call out to PayGate to create the actual transaction.
  const redirectUrl = await createPayGateTransaction({
    paymentId: paymentRef.id,
    amount,
    returnUrl: `https://sbsbeautyspa.co.za/payment-return?paymentId=${paymentRef.id}`,
  });

  return { paymentId: paymentRef.id, redirectUrl };
});

async function createPayGateTransaction({ paymentId, amount, returnUrl }) {
  // Replace with a real PayGate API call. Left as a clearly-marked
  // integration point rather than a fake implementation.
  const response = await fetch("https://secure.paygate.co.za/payweb3/initiate.trans", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ reference: paymentId, amount, returnUrl }),
  });
  const result = await response.json();
  return result.redirectUrl;
}

// ---------------------------------------------------------------------------
// PAYMENT WEBHOOK — PayGate calls this once a transaction completes.
// Must be idempotent: PayGate can redeliver the same webhook more than once.
// ---------------------------------------------------------------------------
exports.paymentWebhook = onRequest(async (req, res) => {
  if (!verifyPayGateSignature(req)) {
    res.status(401).send("Invalid signature");
    return;
  }

  const { paymentId, status, transactionReference } = req.body;
  const paymentRef = db.collection("payments").doc(paymentId);

  await db.runTransaction(async (tx) => {
    const paymentSnap = await tx.get(paymentRef);
    if (!paymentSnap.exists) return;
    const payment = paymentSnap.data();

    // Idempotency guard — a webhook already processed is a no-op.
    if (payment.status === "paid" || payment.status === "failed") {
      return;
    }

    if (status === "success") {
      tx.update(paymentRef, {
        status: "paid",
        transactionReference,
        paidAt: admin.firestore.FieldValue.serverTimestamp(),
      });

      if (payment.bookingId) {
        const bookingRef = db.collection("bookings").doc(payment.bookingId);
        const bookingSnap = await tx.get(bookingRef);
        const booking = bookingSnap.data();

        tx.update(bookingRef, { status: "confirmed" });

        if (booking?.slotId) {
          tx.update(db.collection("availabilitySlots").doc(booking.slotId), {
            status: "booked",
          });
        }
      }
    } else {
      tx.update(paymentRef, { status: "failed" });

      // Release the slot back to the pool on a failed payment so the client
      // (or someone else) can retry immediately rather than waiting for the
      // hold to expire naturally.
      if (payment.bookingId) {
        const bookingSnap = await tx.get(db.collection("bookings").doc(payment.bookingId));
        const slotId = bookingSnap.data()?.slotId;
        if (slotId) {
          tx.update(db.collection("availabilitySlots").doc(slotId), {
            status: "open",
            heldBy: admin.firestore.FieldValue.delete(),
            holdExpiresAt: admin.firestore.FieldValue.delete(),
          });
        }
      }
    }
  });

  res.status(200).send("ok");
});

function verifyPayGateSignature(req) {
  const signature = req.get("X-PayGate-Signature");
  if (!signature || !PAYGATE_SECRET) return false;
  const expected = crypto
    .createHmac("sha256", PAYGATE_SECRET)
    .update(JSON.stringify(req.body))
    .digest("hex");
  // Constant-time comparison to avoid timing attacks.
  return crypto.timingSafeEqual(Buffer.from(signature), Buffer.from(expected));
}

// ---------------------------------------------------------------------------
// NOTIFICATIONS — triggered the moment a booking's status flips to
// "confirmed". Sends the salon address + booking details immediately on
// deposit success (per the confirmed proposal logic — not a delayed reveal).
// ---------------------------------------------------------------------------
exports.sendBookingConfirmationEmail = onDocumentUpdated("bookings/{bookingId}", async (event) => {
  const before = event.data.before.data();
  const after = event.data.after.data();

  if (before.status !== "confirmed" && after.status === "confirmed") {
    const clientSnap = await db.collection("users").doc(after.clientId).get();
    const client = clientSnap.data();
    const settingsSnap = await db.collection("businessSettings").doc("private").get();
    const settings = settingsSnap.data();

    const notificationRef = db.collection("notifications").doc();
    await notificationRef.set({
      clientId: after.clientId,
      bookingId: event.params.bookingId,
      orderId: null,
      type: "salon_location",
      recipientEmail: client.email,
      subject: "Your SBS Beauty Spa booking is confirmed",
      status: "scheduled",
      scheduledAt: admin.firestore.FieldValue.serverTimestamp(),
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    });

    await sendEmail({
      to: client.email,
      subject: "Your SBS Beauty Spa booking is confirmed",
      body: buildConfirmationEmailBody(after, settings),
    });

    await notificationRef.update({
      status: "sent",
      sentAt: admin.firestore.FieldValue.serverTimestamp(),
    });
  }
});

function buildConfirmationEmailBody(booking, settings) {
  return `
    Your booking is confirmed.

    Salon address: ${settings.address}
    Date & time: ${booking.appointmentDate}
    Cancellation policy: ${settings.cancellationPolicy}
  `.trim();
}

async function sendEmail({ to, subject, body }) {
  // Replace with a real transactional email provider call (SendGrid, etc.)
  // Left as a clearly-marked integration point.
  console.log(`Email queued to ${to}: ${subject}`);
}

// ---------------------------------------------------------------------------
// REMINDERS — scheduled function, runs daily, emails clients with an
// appointment tomorrow.
// ---------------------------------------------------------------------------
exports.sendBookingReminders = onSchedule("every day 08:00", async () => {
  const tomorrowStart = new Date();
  tomorrowStart.setDate(tomorrowStart.getDate() + 1);
  tomorrowStart.setHours(0, 0, 0, 0);
  const tomorrowEnd = new Date(tomorrowStart);
  tomorrowEnd.setHours(23, 59, 59, 999);

  const upcoming = await db
    .collection("bookings")
    .where("status", "==", "confirmed")
    .where("appointmentDate", ">=", tomorrowStart)
    .where("appointmentDate", "<=", tomorrowEnd)
    .get();

  for (const doc of upcoming.docs) {
    const booking = doc.data();
    const clientSnap = await db.collection("users").doc(booking.clientId).get();
    const client = clientSnap.data();

    await sendEmail({
      to: client.email,
      subject: "Reminder: your appointment is tomorrow",
      body: `See you tomorrow at ${booking.appointmentDate} for your SBS Beauty Spa appointment.`,
    });
  }
});