/**
 * This file handles the retail shop (products, cart, orders).
 *
 * How it's split:
 *   - Browsing products/categories: direct Firestore reads (public data)
 *   - Cart (CartItem): direct Firestore reads/writes (own data only)
 *   - Checkout (createOrder): THE function that matters — it re-reads
 *     every cart item from Firestore and re-totals everything. Never
 *     trusts the client's cart contents or prices.
 *   - Admin (products, categories, order status): Cloud Functions
 * -----------------------------------------------------------------------
 */

const { onCall, HttpsError } = require("./secure");
const admin = require("firebase-admin");
const { logDeletion } = require("./Deletionlog");
const { COURIER_GUY_API_KEY, getRates, loadCartLines, round2 } = require("./courierguy");

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

// Validates the input for creating or updating a product.
function assertValidProductInput({ name, categoryId, price, stockQuantity }) {
  if (!name || typeof name !== "string") throw new HttpsError("invalid-argument", "name is required.");
  if (!categoryId || typeof categoryId !== "string") throw new HttpsError("invalid-argument", "categoryId is required.");
  if (typeof price !== "number" || price < 0) throw new HttpsError("invalid-argument", "price must be a non-negative number.");
  if (stockQuantity !== undefined && stockQuantity !== null) {
    if (typeof stockQuantity !== "number" || stockQuantity < 0 || !Number.isInteger(stockQuantity)) {
      throw new HttpsError("invalid-argument", "stockQuantity must be a non-negative integer.");
    }
  }
}

// ---------------------------------------------------------------------
// Admin: Products and Categories
// ---------------------------------------------------------------------

/**
 * createProductCategory
 * Input: { name, description? }
 * Output: { categoryId }
 *
 * Admin only. Creates a new product category.
 */
exports.createProductCategory = onCall(async (request) => {
  assertIsAdmin(request);
  const { name, description } = request.data || {};
  if (!name) throw new HttpsError("invalid-argument", "name is required.");

  const docRef = await db.collection("productCategories").add({
    name,
    description: description || "",
    isActive: true,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });
  return { categoryId: docRef.id };
});

/**
 * setProductCategoryActive
 * Input: { categoryId, isActive }
 *
 * Admin only. Turns a product category on or off.
 */
exports.setProductCategoryActive = onCall(async (request) => {
  assertIsAdmin(request);
  const { categoryId, isActive } = request.data || {};
  if (!categoryId || typeof isActive !== "boolean") {
    throw new HttpsError("invalid-argument", "categoryId and isActive (boolean) are required.");
  }
  await db.collection("productCategories").doc(categoryId).update({
    isActive,
    updatedAt: admin.firestore.FieldValue.serverTimestamp(),
  });
  return { categoryId, isActive };
});

/**
 * createProduct
 * Input: { name, description?, categoryId, price, stockQuantity?, imageUrl? }
 * Output: { productId }
 *
 * Admin only. Creates a new product.
 */
exports.createProduct = onCall(async (request) => {
  assertIsAdmin(request);
  const data = request.data || {};
  assertValidProductInput(data);

  const docRef = await db.collection("products").add({
    name: data.name,
    description: data.description || "",
    categoryId: data.categoryId,
    price: data.price,
    stockQuantity: data.stockQuantity ?? null, // null = not stock-tracked
    imageUrl: data.imageUrl || null,
    isActive: true,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
    createdBy: request.auth.uid,
  });
  return { productId: docRef.id };
});

/**
 * updateProduct
 * Input: { productId, name?, description?, categoryId?, price?, stockQuantity?, imageUrl? }
 *
 * Admin only. Updates an existing product.
 */
exports.updateProduct = onCall(async (request) => {
  assertIsAdmin(request);
  const { productId, ...fields } = request.data || {};
  if (!productId) throw new HttpsError("invalid-argument", "productId is required.");

  const ref = db.collection("products").doc(productId);
  const snap = await ref.get();
  if (!snap.exists) throw new HttpsError("not-found", "Product not found.");

  // Only copy provided fields.
  const editable = ["name", "description", "categoryId", "price", "stockQuantity", "imageUrl"];
  const update = {};
  for (const key of editable) if (key in fields) update[key] = fields[key];
  if (Object.keys(update).length === 0) throw new HttpsError("invalid-argument", "No editable fields were provided.");

  // Re-validate with the merged data.
  assertValidProductInput({ ...snap.data(), ...update });

  update.updatedAt = admin.firestore.FieldValue.serverTimestamp();
  update.updatedBy = request.auth.uid;
  await ref.update(update);
  return { productId };
});

/**
 * setProductActive
 * Input: { productId, isActive }
 *
 * Admin only. Turns a product on or off.
 */
exports.setProductActive = onCall(async (request) => {
  assertIsAdmin(request);
  const { productId, isActive } = request.data || {};
  if (!productId || typeof isActive !== "boolean") {
    throw new HttpsError("invalid-argument", "productId and isActive (boolean) are required.");
  }
  await db.collection("products").doc(productId).update({
    isActive,
    updatedAt: admin.firestore.FieldValue.serverTimestamp(),
  });
  return { productId, isActive };
});

/**
 * deleteProduct
 * Input: { productId }
 *
 * Admin only. Refuses if the product is on an active order.
 */
exports.deleteProduct = onCall(async (request) => {
  assertIsAdmin(request);
  const { productId } = request.data || {};
  if (!productId) throw new HttpsError("invalid-argument", "productId is required.");

  // Check if any active order references this product.
  const ordersSnap = await db
    .collection("orders")
    .where("productIds", "array-contains", productId)
    .where("status", "in", ["pending", "confirmed"])
    .limit(1)
    .get();

  if (!ordersSnap.empty) {
    throw new HttpsError("failed-precondition", "This product is on an active order — deactivate it instead of deleting.");
  }

  await db.collection("products").doc(productId).delete();
  await logDeletion({ entityType: "product", entityId: productId, deletedBy: request.auth.uid });

  return { deleted: true };
});

// ---------------------------------------------------------------------
// Client: Checkout
// ---------------------------------------------------------------------

/**
 * createOrder
 * Input: { addressId, serviceCode, notes? }
 * Output: { orderId, subtotal, shippingFee, total }
 *
 * Creates an order that is "awaiting_payment". Nothing is taken from stock and
 * the cart is NOT cleared yet — that only happens once Paystack confirms the
 * payment (see finalizeOrderPayment in payment.js), so a failed or abandoned
 * payment never loses the customer's cart.
 *
 * Reads straight from the caller's own cartItems and re-prices every item from
 * the product docs. The delivery fee is re-quoted from Courier Guy here on the
 * server; the client only says WHICH delivery option it picked.
 */
exports.createOrder = onCall({ secrets: [COURIER_GUY_API_KEY] }, async (request) => {
  const uid = request.auth && request.auth.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in to check out.");

  const { addressId, serviceCode, notes } = request.data || {};
  if (!addressId || !serviceCode) {
    throw new HttpsError("invalid-argument", "Please choose a delivery address and delivery option.");
  }

  const addressSnap = await db.collection("deliveryAddresses").doc(String(addressId)).get();
  if (!addressSnap.exists || addressSnap.data().clientId !== uid) {
    throw new HttpsError("not-found", "Delivery address not found.");
  }
  const address = addressSnap.data();

  const { lines, subtotal } = await loadCartLines(db, uid);

  const rates = await getRates(address, subtotal);
  const rate = rates.find((r) => r.code === serviceCode);
  if (!rate) {
    throw new HttpsError("failed-precondition", "That delivery option is no longer available. Please choose again.");
  }

  const shippingFee = rate.price;
  const total = round2(subtotal + shippingFee);
  const orderRef = db.collection("orders").doc();

  await orderRef.set({
    clientId: uid,
    items: lines,
    productIds: lines.map((li) => li.productId),
    subtotal,
    shippingFee,
    total,
    deliveryService: { code: rate.code, name: rate.name },
    deliveryAddress: {
      recipientName: address.recipientName,
      phone: address.phone,
      addressType: address.addressType,
      complex: address.complex || "",
      streetAddress: address.streetAddress,
      suburb: address.suburb,
      city: address.city,
      province: address.province,
      postalCode: address.postalCode,
    },
    notes: notes || "",
    status: "awaiting_payment",
    paymentStatus: "unpaid",
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });

  return { orderId: orderRef.id, subtotal, shippingFee, total };
});

// ---------------------------------------------------------------------
// Admin: Order Management
// ---------------------------------------------------------------------

// Valid status transitions for orders.
const ORDER_TRANSITIONS = {
  pending: ["confirmed", "cancelled"],
  confirmed: ["fulfilled", "cancelled"],
  fulfilled: [],
  cancelled: [],
};

/**
 * updateOrderStatus
 * Input: { orderId, newStatus }
 *
 * Admin only. Changes an order's status (with valid transitions).
 */
exports.updateOrderStatus = onCall(async (request) => {
  assertIsAdmin(request);
  const { orderId, newStatus } = request.data || {};
  if (!orderId || !["confirmed", "fulfilled", "cancelled"].includes(newStatus)) {
    throw new HttpsError("invalid-argument", 'orderId and a valid newStatus ("confirmed"|"fulfilled"|"cancelled") are required.');
  }

  const ref = db.collection("orders").doc(orderId);
  const snap = await ref.get();
  if (!snap.exists) throw new HttpsError("not-found", "Order not found.");

  const currentStatus = snap.data().status;
  const allowed = ORDER_TRANSITIONS[currentStatus] || [];
  if (!allowed.includes(newStatus)) {
    throw new HttpsError("failed-precondition", `Can't move a "${currentStatus}" order to "${newStatus}".`);
  }

  // If cancelling, restock tracked products.
  if (newStatus === "cancelled") {
    const order = snap.data();
    const batch = db.batch();
    for (const item of order.items) {
      const productRef = db.collection("products").doc(item.productId);
      const productSnap = await productRef.get();
      if (productSnap.exists && productSnap.data().stockQuantity !== null && productSnap.data().stockQuantity !== undefined) {
        batch.update(productRef, { stockQuantity: admin.firestore.FieldValue.increment(item.quantity) });
      }
    }
    batch.update(ref, { status: newStatus, updatedAt: admin.firestore.FieldValue.serverTimestamp(), updatedBy: request.auth.uid });
    await batch.commit();
  } else {
    await ref.update({ status: newStatus, updatedAt: admin.firestore.FieldValue.serverTimestamp(), updatedBy: request.auth.uid });
  }

  return { orderId, status: newStatus };
});

/**
 * listAllOrders
 * Input: { status? }
 * Output: { orders }
 *
 * Admin only. Lists all orders, optionally filtered by status.
 */
exports.listAllOrders = onCall(async (request) => {
  assertIsAdmin(request);
  const { status } = request.data || {};

  let query = db.collection("orders").orderBy("createdAt", "desc");
  if (status) query = query.where("status", "==", status);

  const snap = await query.get();
  const orders = snap.docs.map((d) => ({ id: d.id, ...d.data() }));
  return { orders };
});