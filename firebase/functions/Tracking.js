/**
 * trackOrder
 * Input:  { orderId }
 * Output: { orderId, trackingReference, carrier, isMock, stage (0-5), statusText, message,
 *           exception, steps: [{ label, done, current }], events: [{ date, title, location }] }
 *
 * The signed-in client can track their OWN orders; admins can track any order.
 * If the order is paid but no courier has been booked yet (for example the booking
 * failed earlier), this tries to book it now.
 */

const { onCall, HttpsError } = require("./secure");
const admin = require("firebase-admin");
const { COURIER_GUY_API_KEY } = require("./courierguy");
const { createShipmentForOrder, loadTracking, makeView } = require("./shipping");

if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

exports.trackOrder = onCall({ secrets: [COURIER_GUY_API_KEY] }, async (request) => {
  const uid = request.auth && request.auth.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Please log in first.");

  const orderId = String((request.data && request.data.orderId) || "").trim();
  if (!orderId) throw new HttpsError("invalid-argument", "orderId is required.");

  const ref = db.collection("orders").doc(orderId);
  const snap = await ref.get();
  if (!snap.exists) throw new HttpsError("not-found", "Order not found.");
  let order = snap.data();

  const isAdmin = request.auth.token.admin === true;
  if (order.clientId !== uid && !isAdmin) {
    throw new HttpsError("permission-denied", "This order doesn't belong to you.");
  }

  if (order.status === "cancelled") {
    return makeView(orderId, order, { stage: 0, statusText: "Cancelled", message: "This order was cancelled.", exception: true });
  }
  if (order.paymentStatus !== "paid") {
    return makeView(orderId, order, { stage: 0, statusText: "Waiting for payment", message: "Tracking starts once your payment is confirmed." });
  }

  if (!order.trackingReference) {
    await createShipmentForOrder(orderId);
    order = (await ref.get()).data();
  }
  if (!order.trackingReference) {
    return makeView(orderId, order, { stage: 0, statusText: "Arranging your courier", message: "We're booking your courier. Please check again in a few minutes." });
  }

  try {
    const t = await loadTracking(order);
    return makeView(orderId, order, t);
  } catch (err) {
    console.error("trackOrder lookup failed for", orderId, err);
    return makeView(orderId, order, { stage: 1, message: "Live tracking is temporarily unavailable. Please try again shortly." });
  }
});
