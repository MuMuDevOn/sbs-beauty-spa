/**
 * This file handles the public "get in touch" contact form.
 */

const { onCall, HttpsError } = require("./secure");
const { defineSecret } = require("firebase-functions/params");
const admin = require("firebase-admin");
const sgMail = require("@sendgrid/mail");

// Initialize Firebase if not already done.
if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

// Helper: throws an error if the caller isn't an admin.
function assertIsAdmin(request) {
  if (!request.auth) throw new HttpsError("unauthenticated", "Must be signed in.");
  if (request.auth.token.admin !== true) throw new HttpsError("permission-denied", "Admin access required.");
}

// Where new contact messages are emailed to (the shared business inbox).
const SENDGRID_API_KEY = defineSecret("SENDGRID_API_KEY");
const FROM_EMAIL = "structsbsbeauty@gmail.com"; // the verified SendGrid sender
const ADMIN_INBOX = "structsbsbeauty@gmail.com";

// Stops HTML typed by a visitor from being rendered inside the email.
function esc(s) {
  return String(s || "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
}

// A simple email pattern — not perfect, but catches obvious mistakes.
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * submitContactMessage
 * Input: { name, email, phone?, subject?, message }
 * Output: { messageId }
 *
 * PUBLIC — no sign-in required.
 * Called by the contact form on the website or app.
 */
exports.submitContactMessage = onCall({ secrets: [SENDGRID_API_KEY] }, async (request) => {
  const { name, email, phone, subject, message } = request.data || {};

  // Check the input is valid.
  if (!name || !name.trim()) throw new HttpsError("invalid-argument", "name is required.");
  if (!email || !EMAIL_PATTERN.test(email)) throw new HttpsError("invalid-argument", "A valid email is required.");
  if (!message || !message.trim()) throw new HttpsError("invalid-argument", "message is required.");
  if (name.length > 100 || email.length > 200 || message.length > 3000) {
    throw new HttpsError("invalid-argument", "That message is too long.");
  }

  // Simple spam brake: at most 5 messages per email address per hour.
  const recent = await db.collection("contactMessages").where("email", "==", email.trim()).get();
  const hourAgo = Date.now() - 60 * 60 * 1000;
  const lastHour = recent.docs.filter((d) => {
    const t = d.data().createdAt;
    return t && t.toMillis() > hourAgo;
  }).length;
  if (lastHour >= 5) {
    throw new HttpsError("resource-exhausted", "You've sent a few messages already. Please wait a while before sending another.");
  }

  // Save the message.
  const docRef = await db.collection("contactMessages").add({
    name: name.trim(),
    email: email.trim(),
    phone: phone || "",
    subject: subject || "",
    message: message.trim(),
    clientId: (request.auth && request.auth.uid) || null,   // set when the sender was signed in
    status: "new",
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });

  // Email the business inbox. If the email fails the message is still saved.
  try {
    sgMail.setApiKey(SENDGRID_API_KEY.value());
    await sgMail.send({
      to: ADMIN_INBOX,
      from: FROM_EMAIL,
      replyTo: { email: email.trim(), name: name.trim() },   // "Reply" goes straight to the client
      subject: `New message${subject ? " - " + subject : ""} from ${name.trim()}`,
      html:
        `<h3>New contact message</h3>` +
        `<p><b>Name:</b> ${esc(name.trim())}<br>` +
        `<b>Email:</b> ${esc(email.trim())}<br>` +
        (phone ? `<b>Phone:</b> ${esc(phone)}<br>` : "") +
        `<b>Topic:</b> ${esc(subject || "-")}</p>` +
        `<p style="white-space:pre-wrap">${esc(message.trim())}</p>` +
        `<hr><p style="color:#888">Press Reply to answer ${esc(name.trim())} directly.</p>`,
    });
    await docRef.update({ emailedAt: admin.firestore.FieldValue.serverTimestamp() });
  } catch (err) {
    console.error("Contact email failed:", err.message || err);
    await docRef.update({ emailError: String(err.message || err).slice(0, 300) });
  }

  return { messageId: docRef.id };
});

/**
 * listContactMessages
 * Input: { status? }
 * Output: { messages }
 *
 * Admin only. Lists all contact messages (newest first).
 * Optionally filtered by status (new, handled, etc.).
 */
exports.listContactMessages = onCall(async (request) => {
  assertIsAdmin(request);
  const { status } = request.data || {};

  let query = db.collection("contactMessages").orderBy("createdAt", "desc").limit(300);
  if (status) query = query.where("status", "==", status);

  const snap = await query.get();
  const iso = (t) => (t && t.toDate ? t.toDate().toISOString() : null);
  return {
    messages: snap.docs.map((d) => {
      const m = d.data();
      return {
        id: d.id,
        name: m.name || "",
        email: m.email || "",
        phone: m.phone || "",
        subject: m.subject || "",
        message: m.message || "",
        status: m.status || "new",
        createdAt: iso(m.createdAt),
        reply: m.reply || "",
        repliedByName: m.repliedByName || "",
        repliedAt: iso(m.repliedAt),
      };
    }),
  };
});

/**
 * markContactMessageHandled
 * Input: { messageId }
 * Output: { messageId, status: "handled" }
 *
 * Admin only. Marks a contact message as handled (replied to).
 */
exports.markContactMessageHandled = onCall(async (request) => {
  assertIsAdmin(request);
  const { messageId } = request.data || {};
  if (!messageId) throw new HttpsError("invalid-argument", "messageId is required.");

  await db.collection("contactMessages").doc(messageId).update({
    status: "handled",
    handledBy: request.auth.uid,
    handledAt: admin.firestore.FieldValue.serverTimestamp(),
  });

  return { messageId, status: "handled" };
});

/**
 * replyToContactMessage
 * Input: { messageId, reply }
 * Output: { messageId, repliedByName }
 *
 * Admin only. Saves the reply together with WHICH admin wrote it, then tells the client:
 * an email (always), plus an in-app notification and a phone push when the client was
 * signed in and has a registered device.
 */
exports.replyToContactMessage = onCall({ secrets: [SENDGRID_API_KEY] }, async (request) => {
  assertIsAdmin(request);
  const { messageId, reply } = request.data || {};
  if (!messageId) throw new HttpsError("invalid-argument", "messageId is required.");
  if (!reply || !reply.trim()) throw new HttpsError("invalid-argument", "Please type a reply.");
  if (reply.length > 3000) throw new HttpsError("invalid-argument", "That reply is too long.");

  const ref = db.collection("contactMessages").doc(String(messageId));
  const snap = await ref.get();
  if (!snap.exists) throw new HttpsError("not-found", "Message not found.");
  const msg = snap.data();

  // Which admin is replying?
  let adminName = request.auth.token.name || "";
  try {
    const u = await admin.auth().getUser(request.auth.uid);
    adminName = u.displayName || adminName || (u.email ? u.email.split("@")[0] : "");
  } catch (e) { /* fall back below */ }
  if (!adminName) adminName = "SBS Beauty Spa team";

  await ref.update({
    reply: reply.trim(),
    repliedBy: request.auth.uid,
    repliedByName: adminName,
    repliedAt: admin.firestore.FieldValue.serverTimestamp(),
    status: "handled",
    handledBy: request.auth.uid,
    handledAt: admin.firestore.FieldValue.serverTimestamp(),
  });

  // 1. Email the client (replies to this email go to the shared business inbox).
  try {
    sgMail.setApiKey(SENDGRID_API_KEY.value());
    await sgMail.send({
      to: msg.email,
      from: FROM_EMAIL,
      replyTo: { email: ADMIN_INBOX, name: "SBS Beauty Spa" },
      subject: "Reply from SBS Beauty Spa" + (msg.subject ? " - " + msg.subject : ""),
      html:
        `<p>Hi ${esc(msg.name)},</p>` +
        `<p style="white-space:pre-wrap">${esc(reply.trim())}</p>` +
        `<p>Kind regards,<br>${esc(adminName)}<br>SBS Beauty Spa</p>` +
        `<hr><p style="color:#888">Your message: ${esc(msg.message)}</p>`,
    });
    await ref.update({ replyEmailedAt: admin.firestore.FieldValue.serverTimestamp() });
  } catch (err) {
    console.error("Reply email failed:", err.message || err);
    await ref.update({ replyEmailError: String(err.message || err).slice(0, 300) });
  }

  // 2 + 3. In-app notification and phone push, only if we know which account sent it.
  if (msg.clientId) {
    const text = `${adminName} replied to your message.`;
    await db.collection("notifications").add({
      clientId: msg.clientId,
      type: "contactReply",
      channel: "inApp",
      relatedId: messageId,
      message: text,
      status: "sent",
      isRead: false,
      attempts: 0,
      lastError: null,
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    });

    try {
      const user = await db.collection("users").doc(msg.clientId).get();
      const token = user.exists ? user.data().fcmToken : null;
      if (token) {
        await admin.messaging().send({
          token,
          notification: { title: "SBS Beauty Spa", body: text },
        });
      }
    } catch (err) {
      console.error("Reply push failed:", err.message || err);
    }
  }

  return { messageId, repliedByName: adminName };
});