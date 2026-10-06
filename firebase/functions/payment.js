/**
 * payments.js
 * -----------------------------------------------------------------------
 * This file handles all payment logic using Paystack.
 *
 * How it fits together:
 *   - The secret key NEVER goes to the client. It's stored safely as a
 *     Firebase secret and only read inside these functions.
 *   - The client only ever sees:
 *       1. A URL to open for payment
 *       2. A status like "success" or "failed"
 *     The client never talks to Paystack directly.
 *
 * Two paths confirm a payment:
 *   1. paystackWebhook — Paystack calls this server-to-server. This is
 *      the real source of truth. It still works even if the user closes
 *      the app right after paying.
 *   2. verifyPayment — the app calls this right after the payment page
 *      closes, just so the UI can update immediately instead of waiting
 *      on the webhook.
 *
 * Both paths use the same function reconcilePaymentByReference() so
 * there's only one place that decides "this payment is real."
 *
 * Amounts are converted to cents before calling Paystack, and re-checked
 * against what we expected — never trust an amount coming from the client.
 * -----------------------------------------------------------------------
 */

const { onCall, onRequest, HttpsError } = require("firebase-functions/v2/https");
const { defineSecret } = require("firebase-functions/params");
const admin = require("firebase-admin");
const crypto = require("crypto");

// Initialize Firebase Admin if not already done.
if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

// The Paystack secret key lives in Firebase secrets, not in this file.
const PAYSTACK_SECRET_KEY = defineSecret("PAYSTACK_SECRET_KEY");
const PAYSTACK_BASE_URL = "https://api.paystack.co";
const CURRENCY = "ZAR";

/**
 * initializePayment
 * Input:  { bookingId, type: "deposit" | "balance" | "full" }
 * Output: { authorizationUrl, reference }
 *
 * Called by the app when the user taps "Pay". It tells Paystack to start
 * a payment and gives back a URL the app can open.
 */
exports.initializePayment = onCall({ secrets: [PAYSTACK_SECRET_KEY] }, async (request) => {
  // Make sure the user is logged in.
  const uid = request.auth && request.auth.uid;
  if (!uid) {
    throw new HttpsError("unauthenticated", "Must be signed in to pay.");
  }

  // Make sure we got the right data.
  const { bookingId, type } = request.data || {};
  if (!bookingId || !["deposit", "balance", "full"].includes(type)) {
    throw new HttpsError("invalid-argument", "bookingId and a valid type are required.");
  }

  // Find the booking in the database.
  const bookingRef = db.collection("bookings").doc(bookingId);
  const bookingSnap = await bookingRef.get();
  if (!bookingSnap.exists) {
    throw new HttpsError("not-found", "Booking not found.");
  }
  const booking = bookingSnap.data();

  // Make sure this booking belongs to this user.
  if (booking.clientId !== uid) {
    throw new HttpsError("permission-denied", "This booking doesn't belong to you.");
  }

  // Figure out how much they need to pay.
  const amount = resolveAmount(booking, type);
  if (!amount || amount <= 0) {
    throw new HttpsError("failed-precondition", "Nothing due for this payment type.");
  }

  // Get the user's email (Paystack needs it).
  const userRecord = await admin.auth().getUser(uid);

  // Make a unique reference so we can track this payment later.
  const reference = `bk_${bookingId}_${type}_${Date.now()}`;

  // Ask Paystack to start a payment.
  const paystackRes = await fetch(`${PAYSTACK_BASE_URL}/transaction/initialize`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${PAYSTACK_SECRET_KEY.value()}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      email: userRecord.email,
      amount: Math.round(amount * 100), // Convert Rands to cents
      currency: CURRENCY,
      reference,
      callback_url: "https://sbsbeautyspa.app/payment-callback",
      metadata: { bookingId, type, uid },
    }),
  });
  const paystackData = await paystackRes.json();

  if (!paystackRes.ok || !paystackData.status) {
    throw new HttpsError("internal", `Paystack initialize failed: ${paystackData.message || "unknown error"}`);
  }

  // Save the payment attempt as "pending" right away.
  // This way, whichever path lands first (webhook or verifyPayment),
  // there's always a record to update.
  await db.collection("payments").add({
    bookingId,
    type,
    amount,
    status: "pending",
    reference,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });

  // Send the payment URL back to the app.
  return {
    authorizationUrl: paystackData.data.authorization_url,
    reference,
  };
});

/**
 * verifyPayment
 * Input:  { reference }
 * Output: { status: "success" | "failed" | "pending", bookingId }
 *
 * Called by the app right after the payment page closes.
 * Gives the UI instant feedback instead of waiting on the webhook.
 */
exports.verifyPayment = onCall({ secrets: [PAYSTACK_SECRET_KEY] }, async (request) => {
  const { reference } = request.data || {};
  if (!reference) {
    throw new HttpsError("invalid-argument", "reference is required.");
  }
  return reconcilePaymentByReference(reference);
});

/**
 * paystackWebhook
 * Paystack calls this directly (not through the Firebase SDK).
 * So it's a plain HTTPS function, not a callable one.
 * We verify the request is really from Paystack using a signature.
 */
exports.paystackWebhook = onRequest({ secrets: [PAYSTACK_SECRET_KEY] }, async (req, res) => {
  // Check the signature to make sure this request really came from Paystack.
  const signature = req.headers["x-paystack-signature"];
  const expected = crypto
    .createHmac("sha512", PAYSTACK_SECRET_KEY.value())
    .update(req.rawBody)
    .digest("hex");

  if (signature !== expected) {
    res.status(401).send("Invalid signature");
    return;
  }

  // If the payment succeeded, update our records.
  const event = req.body;
  if (event.event === "charge.success") {
    try {
      await reconcilePaymentByReference(event.data.reference);
    } catch (err) {
      console.error("Webhook reconciliation failed:", err);
      // Still send 200 back. Paystack retries on non-2xx responses,
      // and our reconcile function is safe to run twice. So a small
      // error here just means verifyPayment (or the next retry) will
      // fix it instead of causing a flood of retries.
    }
  }

  res.status(200).send("ok");
});

// ---------------------------------------------------------------------
// The one place that decides a payment is real.
// Both the webhook and verifyPayment call this.
// ---------------------------------------------------------------------
async function reconcilePaymentByReference(reference) {
  // Find the payment record by its reference.
  const paymentQuery = await db.collection("payments").where("reference", "==", reference).limit(1).get();
  if (paymentQuery.empty) {
    throw new HttpsError("not-found", "No payment record for this reference.");
  }
  const paymentDoc = paymentQuery.docs[0];
  const payment = paymentDoc.data();

  // If we already handled this payment, do nothing — just return success.
  // This makes the function idempotent: it's safe to run more than once.
  if (payment.status === "completed") {
    return { status: "success", bookingId: payment.bookingId };
  }

  // Ask Paystack to verify the payment really succeeded.
  const verifyRes = await fetch(`${PAYSTACK_BASE_URL}/transaction/verify/${reference}`, {
    headers: { Authorization: `Bearer ${PAYSTACK_SECRET_KEY.value()}` },
  });
  const verifyData = await verifyRes.json();

  // If Paystack says it failed, mark it failed.
  if (!verifyRes.ok || verifyData.data.status !== "success") {
    await paymentDoc.ref.update({ status: "failed" });
    return { status: "failed", bookingId: payment.bookingId };
  }

  // Double-check the amount matches what we expected.
  // Never trust an amount coming back from the client.
  const expectedCents = Math.round(payment.amount * 100);
  if (verifyData.data.amount !== expectedCents) {
    console.error(`Amount mismatch for ${reference}: expected ${expectedCents}, got ${verifyData.data.amount}`);
    await paymentDoc.ref.update({ status: "failed" });
    return { status: "failed", bookingId: payment.bookingId };
  }

  // Everything checks out — mark the payment as completed.
  await paymentDoc.ref.update({
    status: "completed",
    gatewayReference: String(verifyData.data.id),
    paidAt: admin.firestore.FieldValue.serverTimestamp(),
  });

  // Update the booking.
  //   - deposit paid  → booking becomes "confirmed"
  //   - balance paid  → booking stays "confirmed" and balanceSettled = true
  const bookingRef = db.collection("bookings").doc(payment.bookingId);
  await bookingRef.update(
    payment.type === "deposit"
      ? { status: "confirmed" }
      : { status: "confirmed", balanceSettled: true }
  );

  return { status: "success", bookingId: payment.bookingId };
}

// Works out how much the user should pay based on the payment type.
function resolveAmount(booking, type) {
  if (type === "deposit") return booking.depositAmount;
  if (type === "full") return booking.totalAmount;
  if (type === "balance") return booking.totalAmount - booking.depositAmount;
  return 0;
}