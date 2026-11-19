/**
 * Shared helpers for delivery + checkout (NOT exported from index.js).
 *
 *  - getRates(): asks The Courier Guy (ShipLogic API) what delivery costs.
 *    Every order is quoted as ONE standard small parcel, so product weights
 *    are never needed. Courier Guy re-weighs at collection.
 *  - loadCartLines(): reads the caller's own cart and re-prices every item
 *    from the product documents (never trusts the client).
 *
 * The API key lives in a Firebase secret (COURIER_GUY_API_KEY). If that secret
 * is set to the word  mock  you get made-up rates, so you can build and test
 * the whole checkout before you have a real key.
 */

const { HttpsError } = require("firebase-functions/v2/https");
const { defineSecret } = require("firebase-functions/params");

const COURIER_GUY_API_KEY = defineSecret("COURIER_GUY_API_KEY");
// From The Courier Guy's API docs:
//   LIVE    : POST https://api.portal.thecourierguy.co.za/rates   (production API key)
//   SANDBOX : POST https://api.shiplogic.com/rates                (sandbox API key, free test account)
// Auth is a Bearer token. To use the sandbox, set the secret to  sandbox:<your-key>
// (the "sandbox:" prefix only switches the address; it is not sent to Courier Guy).
const LIVE_BASE_URL = "https://api.portal.thecourierguy.co.za";
const SANDBOX_BASE_URL = "https://api.shiplogic.com";

// After prototype presentation replace this address with clients address: EDIT THESE: where the parcel is collected from (the spa).
const COLLECTION_ADDRESS = {
  type: "business",
  company: "SBS Beauty Spa",
  street_address: "1 Eglin Road",
  local_area: "Sunninghill",
  city: "Sandton",
  zone: "Gauteng", // province
  country: "ZA",
  code: "2157", // postal code
};

// >>> The one "standard parcel" every order is quoted as. Change if needed.
const STANDARD_PARCEL = {
  submitted_length_cm: 30,
  submitted_width_cm: 20,
  submitted_height_cm: 10,
  submitted_weight_kg: 1,
};

const round2 = (n) => Math.round(n * 100) / 100;

function mockRates() {
  const day = 24 * 60 * 60 * 1000;
  const iso = (d) => new Date(Date.now() + d * day).toISOString();
  return [
    { code: "ECO", name: "Standard Delivery", description: "Mock rate (no Courier Guy key set)", price: 75, deliveryFrom: iso(3), deliveryTo: iso(5) },
    { code: "OVN", name: "Express Delivery", description: "Mock rate (no Courier Guy key set)", price: 110, deliveryFrom: iso(1), deliveryTo: iso(2) },
  ];
}

/**
 * address: a deliveryAddresses doc's data. declaredValue: cart subtotal in Rand.
 * Returns [{ code, name, description, price, deliveryFrom, deliveryTo }]
 */
async function getRates(address, declaredValue) {
  let key = (COURIER_GUY_API_KEY.value() || "").trim();
  if (!key || key === "mock") return mockRates();
  let baseUrl = LIVE_BASE_URL;
  if (key.startsWith("sandbox:")) {
    key = key.slice("sandbox:".length).trim();
    baseUrl = SANDBOX_BASE_URL;
  }

  const today = new Date().toISOString().slice(0, 10);
  const body = {
    collection_address: COLLECTION_ADDRESS,
    delivery_address: {
      type: address.addressType === "business" ? "business" : "residential",
      street_address: [address.complex, address.streetAddress].filter(Boolean).join(", "),
      local_area: address.suburb,
      city: address.city,
      zone: address.province,
      country: "ZA",
      code: address.postalCode,
    },
    parcels: [{ ...STANDARD_PARCEL }],
    declared_value: Math.max(1, Math.round(declaredValue || 1)),
    collection_min_date: today,
    delivery_min_date: today,
  };

  const res = await fetch(`${baseUrl}/rates`, {
    method: "POST",
    headers: { Authorization: `Bearer ${key}`, "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });

  let data = {};
  try { data = await res.json(); } catch (_) { /* leave empty */ }

  if (!res.ok) {
    console.error("ShipLogic rates failed", res.status, JSON.stringify(data));
    throw new HttpsError("unavailable", `Couldn't get delivery prices right now (${data.message || res.status}). Please try again.`);
  }

  console.log("ShipLogic rates raw response:", JSON.stringify(data).slice(0, 3000));
  const rawRates = Array.isArray(data.rates) ? data.rates : (data.rates && Array.isArray(data.rates.rates) ? data.rates.rates : []);
  const rates = rawRates
    .map((r) => ({
      code: (r.service_level && r.service_level.code) || r.service_level_code,
      name: (r.service_level && r.service_level.name) || "Delivery",
      description: (r.service_level && r.service_level.description) || "",
      price: round2(Number(r.rate != null ? r.rate : r.base_rate && r.base_rate.charge != null ? r.rate : r.rate)),
      deliveryFrom: (r.service_level && r.service_level.delivery_date_from) || null,
      deliveryTo: (r.service_level && r.service_level.delivery_date_to) || null,
    }))
    .filter((r) => r.code && Number.isFinite(r.price) && r.price > 0);

  if (rates.length === 0) {
    throw new HttpsError("failed-precondition", "We can't deliver to that address. Please check it or try another one.");
  }
  return rates;
}

/**
 * Reads the caller's cart and prices it from the product docs.
 * Throws a user-friendly HttpsError if anything is wrong.
 * Returns { cartDocs, lines, subtotal }.
 */
async function loadCartLines(db, uid) {
  const cartSnap = await db.collection("cartItems").where("clientId", "==", uid).get();
  if (cartSnap.empty) throw new HttpsError("failed-precondition", "Your cart is empty.");

  const cartDocs = cartSnap.docs;
  const productSnaps = await Promise.all(
    cartDocs.map((d) => db.collection("products").doc(d.data().productId).get())
  );

  const lines = [];
  let subtotal = 0;
  for (let i = 0; i < cartDocs.length; i++) {
    const cartItem = cartDocs[i].data();
    const snap = productSnaps[i];
    if (!snap.exists) throw new HttpsError("failed-precondition", "One of the items in your cart is no longer available.");
    const product = snap.data();
    if (product.isActive === false) throw new HttpsError("failed-precondition", `"${product.name}" is no longer available.`);
    if (product.stockQuantity !== null && product.stockQuantity !== undefined && product.stockQuantity < cartItem.quantity) {
      throw new HttpsError("failed-precondition", `Not enough stock for "${product.name}" (${product.stockQuantity} left).`);
    }
    const lineTotal = round2(product.price * cartItem.quantity);
    subtotal += lineTotal;
    lines.push({ productId: snap.id, name: product.name, unitPrice: product.price, quantity: cartItem.quantity, lineTotal });
  }
  return { cartDocs, lines, subtotal: round2(subtotal) };
}

/**
 * Reads the secret once and says which Courier Guy server to talk to.
 *   mock          -> isMock true (no real calls, made-up shipments)
 *   sandbox:<key> -> test server
 *   <key>         -> live server
 */
function getApiConfig() {
  let key = (COURIER_GUY_API_KEY.value() || "").trim();
  if (!key || key === "mock") return { isMock: true, key: "", baseUrl: "" };
  let baseUrl = LIVE_BASE_URL;
  if (key.startsWith("sandbox:")) {
    key = key.slice("sandbox:".length).trim();
    baseUrl = SANDBOX_BASE_URL;
  }
  return { isMock: false, key, baseUrl };
}

module.exports = { COURIER_GUY_API_KEY, getRates, loadCartLines, round2, getApiConfig, COLLECTION_ADDRESS, STANDARD_PARCEL };