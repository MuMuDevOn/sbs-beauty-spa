/**
 * contactMessages.js
 * -----------------------------------------------------------------------
 * This file handles the public "get in touch" contact form.
 *
 * IMPORTANT: submitContactMessage takes NO auth — anyone can call it,
 * even before signing up or logging in.
 *
 * That also makes it the ONE endpoint in this whole backend that's
 * genuinely open to abuse (spam submissions). Real protections like
 * App Check, reCAPTCHA, or IP-based rate limiting are NOT built here.
 * This is a known gap — basic field validation alone isn't enough.
 * -----------------------------------------------------------------------
 */

const { onCall, HttpsError } = require("firebase-functions/v2/https");
const admin = require("firebase-admin");

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
exports.submitContactMessage = onCall(async (request) => {
  const { name, email, phone, subject, message } = request.data || {};

  // Check the input is valid.
  if (!name || !name.trim()) throw new HttpsError("invalid-argument", "name is required.");
  if (!email || !EMAIL_PATTERN.test(email)) throw new HttpsError("invalid-argument", "A valid email is required.");
  if (!message || !message.trim()) throw new HttpsError("invalid-argument", "message is required.");

  // Save the message.
  const docRef = await db.collection("contactMessages").add({
    name: name.trim(),
    email: email.trim(),
    phone: phone || "",
    subject: subject || "",
    message: message.trim(),
    status: "new",
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });

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

  let query = db.collection("contactMessages").orderBy("createdAt", "desc");
  if (status) query = query.where("status", "==", status);

  const snap = await query.get();
  return { messages: snap.docs.map((d) => ({ id: d.id, ...d.data() })) };
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