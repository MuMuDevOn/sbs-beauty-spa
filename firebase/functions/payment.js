/**
 * This file handles all payment logic using Paystack.
 * Two paths confirm a payment:
 *   1. paystackWebhook — Paystack calls this server-to-server. This is
 *      the real source of truth. It still works even if the user closes
 *      the app right after paying.
 *   2. verifyPayment — the app calls this right after the payment page
 *      closes, just so the UI can update immediately instead of waiting
 *      on the webhook.
 */

const { onCall, onRequest, HttpsError } = require("./secure");
const { defineSecret } = require("firebase-functions/params");
const { COURIER_GUY_API_KEY } = require("./courierguy");
const { createShipmentForOrder } = require("./shipping");
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

  // Shop orders take a separate path (amount comes from the order, never the client).
  if (request.data && request.data.orderId) {
    return initializeOrderPayment(uid, String(request.data.orderId));
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
      callback_url: "https://sbs-beauty-spa.web.app/paid.html",
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
exports.verifyPayment = onCall({ secrets: [PAYSTACK_SECRET_KEY, COURIER_GUY_API_KEY] }, async (request) => {
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
exports.paystackWebhook = onRequest({ secrets: [PAYSTACK_SECRET_KEY, COURIER_GUY_API_KEY] }, async (req, res) => {
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

  // Shop order payment -> its own reconcile path.
  if (payment.orderId) {
    return reconcileOrderPayment(paymentDoc, payment, reference);
  }

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

// ---------------------------------------------------------------------
// Shop orders
// ---------------------------------------------------------------------

async function initializeOrderPayment(uid, orderId) {
  const orderRef = db.collection("orders").doc(orderId);
  const orderSnap = await orderRef.get();
  if (!orderSnap.exists) throw new HttpsError("not-found", "Order not found.");
  const order = orderSnap.data();

  if (order.clientId !== uid) throw new HttpsError("permission-denied", "This order doesn't belong to you.");
  if (order.paymentStatus === "paid") throw new HttpsError("failed-precondition", "This order is already paid.");

  const amount = order.total;
  if (!amount || amount <= 0) throw new HttpsError("failed-precondition", "Nothing due for this order.");

  const userRecord = await admin.auth().getUser(uid);
  const reference = `or_${orderId}_${Date.now()}`;

  const paystackRes = await fetch(`${PAYSTACK_BASE_URL}/transaction/initialize`, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${PAYSTACK_SECRET_KEY.value()}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      email: userRecord.email,
      amount: Math.round(amount * 100),
      currency: CURRENCY,
      reference,
      callback_url: "https://sbs-beauty-spa.web.app/paid.html",
      metadata: { orderId, type: "order", uid },
    }),
  });
  const paystackData = await paystackRes.json();
  if (!paystackRes.ok || !paystackData.status) {
    throw new HttpsError("internal", `Paystack initialize failed: ${paystackData.message || "unknown error"}`);
  }

  await db.collection("payments").add({
    orderId,
    type: "order",
    amount,
    status: "pending",
    reference,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });

  return { authorizationUrl: paystackData.data.authorization_url, reference };
}

async function reconcileOrderPayment(paymentDoc, payment, reference) {
  const result = (status) => ({ status, bookingId: "", orderId: payment.orderId });

  if (payment.status === "completed") return result("success");

  const verifyRes = await fetch(`${PAYSTACK_BASE_URL}/transaction/verify/${reference}`, {
    headers: { Authorization: `Bearer ${PAYSTACK_SECRET_KEY.value()}` },
  });
  const verifyData = await verifyRes.json();

  // Not paid (yet): the abandoned/ongoing states stay "pending" so polling can continue.
  if (!verifyRes.ok || !verifyData.data) return result("pending");
  const gatewayStatus = verifyData.data.status;
  if (gatewayStatus === "failed" || gatewayStatus === "reversed") {
    await paymentDoc.ref.update({ status: "failed" });
    return result("failed");
  }
  if (gatewayStatus !== "success") return result("pending");

  // Never trust the amount: it must match what we asked for.
  const expectedCents = Math.round(payment.amount * 100);
  if (verifyData.data.amount !== expectedCents) {
    console.error(`Amount mismatch for ${reference}: expected ${expectedCents}, got ${verifyData.data.amount}`);
    await paymentDoc.ref.update({ status: "failed" });
    return result("failed");
  }

  await paymentDoc.ref.update({
    status: "completed",
    gatewayReference: String(verifyData.data.id),
    paidAt: admin.firestore.FieldValue.serverTimestamp(),
  });

  await finalizeOrderPayment(payment.orderId);

  // Book the courier. A courier problem must never undo or fail a payment that
  // already went through, so any error is logged and the order simply has no
  // tracking number yet (trackOrder tries again when the client opens tracking).
  try {
    await createShipmentForOrder(payment.orderId);
  } catch (err) {
    console.error("createShipmentForOrder failed for order", payment.orderId, err);
  }

  return result("success");
}

// Marks the order paid, takes the stock, and clears the paid-for items from the cart.
// Safe to run twice (it checks paymentStatus first).
async function finalizeOrderPayment(orderId) {
  const orderRef = db.collection("orders").doc(orderId);

  await db.runTransaction(async (tx) => {
    const orderSnap = await tx.get(orderRef);
    if (!orderSnap.exists) return;
    const order = orderSnap.data();
    if (order.paymentStatus === "paid") return;

    const cartSnap = await tx.get(db.collection("cartItems").where("clientId", "==", order.clientId));
    const productRefs = order.items.map((li) => db.collection("products").doc(li.productId));
    const productSnaps = await Promise.all(productRefs.map((ref) => tx.get(ref)));

    let attention = "";
    order.items.forEach((li, i) => {
      const snap = productSnaps[i];
      if (!snap.exists) return;
      const stock = snap.data().stockQuantity;
      if (stock === null || stock === undefined) return; // not stock-tracked
      if (stock < li.quantity) attention += `"${li.name}" had only ${stock} left (ordered ${li.quantity}). `;
      tx.update(productRefs[i], { stockQuantity: Math.max(0, stock - li.quantity) });
    });

    // Only remove the cart lines that were part of this order.
    const orderedIds = new Set(order.productIds);
    cartSnap.docs.forEach((d) => {
      if (orderedIds.has(d.data().productId)) tx.delete(d.ref);
    });

    tx.update(orderRef, {
      paymentStatus: "paid",
      status: "pending",
      paidAt: admin.firestore.FieldValue.serverTimestamp(),
      ...(attention ? { needsAttention: true, attentionNote: attention.trim() } : {}),
    });
  });
}