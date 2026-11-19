/**
 * Delivery addresses + delivery prices (The Courier Guy).
 *
 *  - listMyAddresses / saveMyAddress / deleteMyAddress : the caller's own saved addresses
 *  - getDeliveryRates : live Courier Guy options + prices for one saved address
 *
 * Addresses are read/written ONLY through these functions, so no Firestore
 * rules change is needed.
 */

const { onCall, HttpsError } = require("./secure");
const admin = require("firebase-admin");
const { COURIER_GUY_API_KEY, getRates, loadCartLines } = require("./courierguy");

if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

const MAX_ADDRESSES = 10;
const PROVINCES = ["Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo", "Mpumalanga", "Northern Cape", "North West", "Western Cape"];

function requireUid(request) {
  const uid = request.auth && request.auth.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in.");
  return uid;
}

const clean = (v) => (typeof v === "string" ? v.trim() : "");

function toClient(doc) {
  const d = doc.data();
  return {
    id: doc.id,
    recipientName: d.recipientName || "",
    phone: d.phone || "",
    addressType: d.addressType || "residential",
    complex: d.complex || "",
    streetAddress: d.streetAddress || "",
    suburb: d.suburb || "",
    city: d.city || "",
    province: d.province || "",
    postalCode: d.postalCode || "",
  };
}

/** listMyAddresses  Output: { addresses } */
exports.listMyAddresses = onCall(async (request) => {
  const uid = requireUid(request);
  const snap = await db.collection("deliveryAddresses").where("clientId", "==", uid).get();
  const docs = snap.docs.slice().sort((a, b) => {
    const ta = a.data().createdAt ? a.data().createdAt.toMillis() : 0;
    const tb = b.data().createdAt ? b.data().createdAt.toMillis() : 0;
    return ta - tb;
  });
  return { addresses: docs.map(toClient) };
});

/**
 * saveMyAddress
 * Input: { id?, recipientName, phone, addressType, complex?, streetAddress, suburb, city, province, postalCode }
 * Output: { addressId }
 */
exports.saveMyAddress = onCall(async (request) => {
  const uid = requireUid(request);
  const d = request.data || {};

  const data = {
    recipientName: clean(d.recipientName),
    phone: clean(d.phone),
    addressType: d.addressType === "business" ? "business" : "residential",
    complex: clean(d.complex),
    streetAddress: clean(d.streetAddress),
    suburb: clean(d.suburb),
    city: clean(d.city),
    province: clean(d.province),
    postalCode: clean(d.postalCode),
  };

  if (!data.recipientName) throw new HttpsError("invalid-argument", "Please enter the recipient's name.");
  if (!/^[0-9+() \-]{9,15}$/.test(data.phone)) throw new HttpsError("invalid-argument", "Please enter a valid phone number.");
  if (!data.streetAddress) throw new HttpsError("invalid-argument", "Please enter the street address.");
  if (!data.suburb) throw new HttpsError("invalid-argument", "Please enter the suburb.");
  if (!data.city) throw new HttpsError("invalid-argument", "Please enter the city.");
  if (!PROVINCES.includes(data.province)) throw new HttpsError("invalid-argument", "Please choose a province.");
  if (!/^[0-9]{4}$/.test(data.postalCode)) throw new HttpsError("invalid-argument", "Postal code must be 4 digits.");

  if (d.id) {
    const ref = db.collection("deliveryAddresses").doc(String(d.id));
    const snap = await ref.get();
    if (!snap.exists || snap.data().clientId !== uid) throw new HttpsError("not-found", "Address not found.");
    await ref.update({ ...data, updatedAt: admin.firestore.FieldValue.serverTimestamp() });
    return { addressId: ref.id };
  }

  const existing = await db.collection("deliveryAddresses").where("clientId", "==", uid).get();
  if (existing.size >= MAX_ADDRESSES) throw new HttpsError("failed-precondition", `You can save up to ${MAX_ADDRESSES} addresses.`);

  const ref = await db.collection("deliveryAddresses").add({
    ...data,
    clientId: uid,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });
  return { addressId: ref.id };
});

/** deleteMyAddress  Input: { addressId } */
exports.deleteMyAddress = onCall(async (request) => {
  const uid = requireUid(request);
  const { addressId } = request.data || {};
  if (!addressId) throw new HttpsError("invalid-argument", "addressId is required.");
  const ref = db.collection("deliveryAddresses").doc(String(addressId));
  const snap = await ref.get();
  if (!snap.exists || snap.data().clientId !== uid) throw new HttpsError("not-found", "Address not found.");
  await ref.delete();
  return { deleted: true };
});

/**
 * getDeliveryRates
 * Input: { addressId }
 * Output: { rates: [{ code, name, description, price, deliveryFrom, deliveryTo }] }
 */
exports.getDeliveryRates = onCall({ secrets: [COURIER_GUY_API_KEY] }, async (request) => {
  const uid = requireUid(request);
  const { addressId } = request.data || {};
  if (!addressId) throw new HttpsError("invalid-argument", "addressId is required.");

  const snap = await db.collection("deliveryAddresses").doc(String(addressId)).get();
  if (!snap.exists || snap.data().clientId !== uid) throw new HttpsError("not-found", "Address not found.");

  const { subtotal } = await loadCartLines(db, uid);
  const rates = await getRates(snap.data(), subtotal);
  return { rates };
});

/**
 * listMyOrders
 * Output: { orders: [...] } — the caller's own PAID shop orders, newest first.
 * (Unpaid / abandoned checkouts are not shown.)
 */
exports.listMyOrders = onCall(async (request) => {
  const uid = requireUid(request);
  const snap = await db.collection("orders")
    .where("clientId", "==", uid)
    .where("paymentStatus", "==", "paid")
    .get();

  const toIso = (ts) => (ts && ts.toDate ? ts.toDate().toISOString() : null);

  const orders = snap.docs.map((doc) => {
    const d = doc.data();
    return {
      id: doc.id,
      status: d.status || "pending",
      createdAt: toIso(d.paidAt) || toIso(d.createdAt),
      subtotal: d.subtotal || 0,
      shippingFee: d.shippingFee || 0,
      total: d.total || 0,
      deliveryServiceName: (d.deliveryService && d.deliveryService.name) || "",
      deliveryAddress: d.deliveryAddress || {},
      items: (d.items || []).map((li) => ({
        name: li.name || "",
        quantity: li.quantity || 0,
        unitPrice: li.unitPrice || 0,
        lineTotal: li.lineTotal || 0,
      })),
    };
  });

  orders.sort((a, b) => String(b.createdAt || "").localeCompare(String(a.createdAt || "")));
  return { orders };
});