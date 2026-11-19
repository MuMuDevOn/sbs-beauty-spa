/**
 * Shipments + tracking helpers (NOT exported from index.js).
 *
 *  - createShipmentForOrder(orderId): after an order is paid, books the parcel with
 *    The Courier Guy (ShipLogic API) and saves the tracking reference on the order.
 *    Safe to call more than once (it locks, and skips orders that already have one).
 *  - loadTracking(order): asks The Courier Guy where the parcel is.
 *  - makeView(): the tidy answer the app shows.
 *
 * If COURIER_GUY_API_KEY is  mock  (or empty) nothing real is called: the shipment is
 * simulated and moves through the stages by itself over about 8 minutes, so the whole
 * tracking page can be demonstrated.
 */

const admin = require("firebase-admin");
const { getApiConfig, COLLECTION_ADDRESS, STANDARD_PARCEL, round2 } = require("./courierguy");

if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

// >>> EDIT THESE: who the courier phones at the spa when they come to collect.
const COLLECTION_CONTACT = {
  name: "SBS Beauty Spa",
  mobile_number: "0736416537",
  email: "structsbsbeauty@gmail.com",
};

const STAGE_LABELS = [
  "Order placed",
  "Ready for collection",
  "Collected",
  "In transit",
  "Out for delivery",
  "Delivered",
];

// Minutes after payment at which the MOCK parcel reaches stages 1..5.
const MOCK_MINUTES = [0, 2, 4, 6, 8];

// ---------------------------------------------------------------------
// Pure helpers (no network, easy to test)
// ---------------------------------------------------------------------

// Every Shiplogic shipment status (from their "Shipment statuses" docs page) and where it
// sits on our 6-step timeline:  1 Ready for collection, 2 Collected, 3 In transit,
// 4 Out for delivery, 5 Delivered.  The second value marks a problem to show in red.
const STATUS_MAP = {
  // booking / quote / payment
  "quote": [1, false],
  "quote-expired": [1, true],
  "payment-pending": [1, false],
  "payment-failed": [1, true],
  "pending-approval": [1, false],
  "approval-rejected": [1, true],
  "submitted": [1, false],
  "failed": [1, true],
  // collection
  "collection-accepted": [1, false],
  "collection-assigned": [1, false],
  "collection-unassigned": [1, false],
  "collection-rejected": [1, false],
  "collection-exception": [1, true],
  "collection-failed-attempt": [1, true],
  "collected": [2, false],
  "awaiting-dropoff": [1, false],
  // hub and transit
  "inbound-customs-and-vat": [3, false],
  "at-hub": [3, false],
  "on-hold": [3, true],
  "on-hold-internal": [3, false],
  "damaged": [3, true],
  "under-query": [3, true],
  "returned-to-hub": [3, true],
  "manifested": [3, false],
  "ready-for-dispatch": [3, false],
  "in-transit": [3, false],
  "at-destination-hub": [3, false],
  "floor-check": [3, false],
  // delivery
  "delivery-scheduled": [4, false],
  "delivery-assigned": [4, false],
  "delivery-unassigned": [4, false],
  "delivery-rejected": [4, false],
  "out-for-delivery": [4, false],
  "delivery-exception": [4, true],
  "delivery-failed-attempt": [4, true],
  "in-locker": [4, false],
  "collect-and-return-to-hub": [3, true],
  "return-to-hub": [3, true],
  "ready-for-pickup": [4, false],
  "delivered": [5, false],
  // special
  "returned-to-sender": [3, true],
  "undeliverable": [3, true],
  "cancelled": [1, true],
};

// Turns a Courier Guy status word into one of our stages (0-5) and an "exception" flag.
function stageFromStatus(status) {
  const key = String(status || "").trim().toLowerCase().replace(/[_\s]+/g, "-");
  const hit = STATUS_MAP[key];
  if (hit) return { stage: hit[0], exception: hit[1] };
  if (key) console.warn("Unknown Shiplogic status:", key);
  return { stage: 1, exception: false };
}

function toMillis(ts) {
  if (!ts) return 0;
  if (typeof ts.toMillis === "function") return ts.toMillis();
  const t = Date.parse(ts);
  return Number.isFinite(t) ? t : 0;
}

// The simulated parcel journey, worked out from how long ago the order was paid.
function mockTracking(order, nowMs = Date.now()) {
  const start = toMillis(order.paidAt) || toMillis(order.shipmentCreatedAt) || nowMs;
  const minutesGone = (nowMs - start) / 60000;
  let stage = 1;
  MOCK_MINUTES.forEach((m, i) => {
    if (minutesGone >= m) stage = i + 1;
  });

  const city = (order.deliveryAddress && order.deliveryAddress.city) || "";
  const titles = [
    ["Courier booked", COLLECTION_ADDRESS.city],
    ["Parcel collected from SBS Beauty Spa", COLLECTION_ADDRESS.city],
    ["In transit to the delivery hub", ""],
    ["Out for delivery", city],
    ["Delivered", city],
  ];
  const events = [];
  for (let i = 0; i < stage; i++) {
    events.push({
      date: new Date(start + MOCK_MINUTES[i] * 60000).toISOString(),
      title: titles[i][0],
      location: titles[i][1],
    });
  }
  events.reverse(); // newest first
  return { stage, exception: false, events };
}

function makeView(orderId, order, p) {
  const stage = Math.max(0, Math.min(5, p.stage || 0));
  return {
    orderId,
    trackingReference: order.trackingReference || null,
    carrier: "The Courier Guy",
    isMock: !!order.shipmentIsMock,
    stage,
    statusText: p.statusText || STAGE_LABELS[stage],
    message: p.message || "",
    exception: !!p.exception,
    steps: STAGE_LABELS.map((label, i) => ({ label, done: i <= stage, current: i === stage })),
    events: p.events || [],
  };
}

// ---------------------------------------------------------------------
// Courier Guy calls
// ---------------------------------------------------------------------

async function createRealShipment(orderId, order, cfg) {
  const a = order.deliveryAddress || {};

  let email = "";
  try {
    email = (await admin.auth().getUser(order.clientId)).email || "";
  } catch (_) { /* the courier email is optional */ }

  // Courier Guy wants a full date-and-time here (2026-11-03T08:15:00.000Z), not just 2026-11-03.
  const now = new Date().toISOString();
  const body = {
    collection_address: COLLECTION_ADDRESS,
    collection_contact: COLLECTION_CONTACT,
    delivery_address: {
      type: a.addressType === "business" ? "business" : "residential",
      street_address: [a.complex, a.streetAddress].filter(Boolean).join(", "),
      local_area: a.suburb,
      city: a.city,
      zone: a.province,
      country: "ZA",
      code: a.postalCode,
    },
    delivery_contact: { name: a.recipientName, mobile_number: a.phone, email },
    parcels: [{ parcel_description: "SBS Beauty Spa order", ...STANDARD_PARCEL }],
    declared_value: Math.max(1, Math.round(order.subtotal || order.total || 1)),
    service_level_code: order.deliveryService && order.deliveryService.code,
    customer_reference: `SBS-${orderId.slice(0, 8).toUpperCase()}`,
    collection_min_date: now,
    delivery_min_date: now,
  };

  const res = await fetch(`${cfg.baseUrl}/shipments`, {
    method: "POST",
    headers: { Authorization: `Bearer ${cfg.key}`, "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  const raw = await res.text();
  let data = {};
  try { data = JSON.parse(raw); } catch (_) { /* not JSON: raw text is logged below */ }

  if (!res.ok) {
    // Log what we sent (without names/phone/email) and exactly what Courier Guy said back.
    console.error(
      "ShipLogic create shipment failed", res.status, "content-type:", res.headers.get("content-type"),
      "| server:", cfg.baseUrl,
      "| sent:", JSON.stringify({
        service_level_code: body.service_level_code,
        declared_value: body.declared_value,
        delivery_zone: body.delivery_address.zone,
        delivery_city: body.delivery_address.city,
        delivery_code: body.delivery_address.code,
        delivery_mobile_digits: String(body.delivery_contact.mobile_number || "").replace(/\D/g, "").length,
        collection_mobile_digits: String(body.collection_contact.mobile_number || "").replace(/\D/g, "").length,
        has_email: !!body.delivery_contact.email,
      }),
      "| reply:", raw.slice(0, 1500) || "(empty)"
    );
    const reason = data.message || data.error || (data.errors ? JSON.stringify(data.errors).slice(0, 200) : "") || raw.slice(0, 200) || "no details";
    throw new Error(`Courier Guy refused the shipment (${res.status}: ${reason})`);
  }
  console.log("ShipLogic create shipment response:", JSON.stringify(data).slice(0, 2000));

  const trackingReference = data.short_tracking_reference || data.tracking_reference || data.custom_tracking_reference;
  if (!trackingReference) throw new Error("Courier Guy did not return a tracking reference");
  return { trackingReference: String(trackingReference), shipmentId: String(data.id || ""), isMock: false };
}

/**
 * Books the courier for a PAID order and saves trackingReference on it.
 * Returns the result, or null if there was nothing to do.
 */
async function createShipmentForOrder(orderId) {
  const ref = db.collection("orders").doc(orderId);

  // Claim the job so the webhook and verifyPayment can't both book a courier.
  const claimed = await db.runTransaction(async (tx) => {
    const snap = await tx.get(ref);
    if (!snap.exists) return null;
    const o = snap.data();
    if (o.paymentStatus !== "paid") return null;
    if (o.trackingReference) return null;
    if (Date.now() - toMillis(o.shipmentLockAt) < 120000) return null;
    tx.update(ref, { shipmentLockAt: admin.firestore.Timestamp.now() });
    return o;
  });
  if (!claimed) return null;

  try {
    const cfg = getApiConfig();
    const result = cfg.isMock
      ? { trackingReference: `MOCK${orderId.slice(0, 6).toUpperCase()}`, shipmentId: "mock", isMock: true }
      : await createRealShipment(orderId, claimed, cfg);

    await ref.update({
      trackingReference: result.trackingReference,
      shipmentId: result.shipmentId,
      shipmentIsMock: result.isMock,
      shipmentCreatedAt: admin.firestore.FieldValue.serverTimestamp(),
      shipmentError: admin.firestore.FieldValue.delete(),
      shipmentLockAt: admin.firestore.FieldValue.delete(),
    });
    return result;
  } catch (err) {
    console.error("Shipment creation failed for order", orderId, err);
    await ref.update({
      shipmentError: String((err && err.message) || err).slice(0, 300),
      shipmentLockAt: admin.firestore.FieldValue.delete(),
    });
    return null;
  }
}

/** Where is the parcel? Returns { stage, exception, events: [{ date, title, location }] }. */
async function loadTracking(order) {
  if (order.shipmentIsMock) return mockTracking(order);

  const cfg = getApiConfig();
  if (cfg.isMock) throw new Error("Courier Guy key is not set");

  const url = `${cfg.baseUrl}/tracking/shipments?tracking_reference=${encodeURIComponent(order.trackingReference)}`;
  const res = await fetch(url, { headers: { Authorization: `Bearer ${cfg.key}` } });
  const raw = await res.text();
  let data = {};
  try { data = JSON.parse(raw); } catch (_) { /* not JSON */ }
  if (!res.ok) {
    console.error("ShipLogic tracking failed", res.status, raw.slice(0, 1500) || "(empty)");
    throw new Error(`Tracking lookup failed (${res.status}: ${data.message || raw.slice(0, 200) || "no details"})`);
  }
  console.log("ShipLogic tracking raw response:", JSON.stringify(data).slice(0, 3000));

  const s = (Array.isArray(data.shipments) && data.shipments[0]) || data.shipment || data;
  const rawEvents = s.tracking_events || s.events || data.tracking_events || [];
  const events = (Array.isArray(rawEvents) ? rawEvents : [])
    .map((e) => ({
      date: e.date || e.created_at || null,
      status: e.status || "",
      title: e.message || e.status || "Update",
      location: e.location || "",
    }))
    .sort((x, y) => toMillis(y.date) - toMillis(x.date));

  const status = (events[0] && events[0].status) || s.status || "";
  const { stage, exception } = stageFromStatus(status);
  return { stage, exception, events: events.map(({ date, title, location }) => ({ date, title, location })) };
}

module.exports = {
  createShipmentForOrder,
  loadTracking,
  makeView,
  stageFromStatus,
  mockTracking,
  STAGE_LABELS,
  round2,
};