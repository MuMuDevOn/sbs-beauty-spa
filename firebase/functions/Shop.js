/**
 * shop.js
 * -----------------------------------------------------------------------
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

const { onCall, HttpsError } = require("firebase-functions/v2/https");
const admin = require("firebase-admin");
const { logDeletion } = require("./Deletionlog");

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
 * Input: { deliveryAddress?, notes? }
 * Output: { orderId, total }
 *
 * Reads straight from the caller's own cartItems. Never accepts a cart
 * snapshot from the client. Each item's price is re-fetched from the
 * product doc. Stock is decremented for tracked products. The cart is
 * cleared only after the order is written.
 */
exports.createOrder = onCall(async (request) => {
  const uid = request.auth && request.auth.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in to check out.");

  const { deliveryAddress, notes } = request.data || {};
  const orderRef = db.collection("orders").doc();

  const result = await db.runTransaction(async (tx) => {
    // Read everything first (required by Firestore transactions).
    const cartSnap = await tx.get(db.collection("cartItems").where("clientId", "==", uid));
    if (cartSnap.empty) {
      throw new HttpsError("failed-precondition", "Your cart is empty.");
    }

    const cartDocs = cartSnap.docs;
    const productRefs = cartDocs.map((d) => db.collection("products").doc(d.data().productId));
    const productSnaps = await Promise.all(productRefs.map((ref) => tx.get(ref)));

    const lineItems = [];
    let total = 0;

    for (let i = 0; i < cartDocs.length; i++) {
      const cartItem = cartDocs[i].data();
      const productSnap = productSnaps[i];

      // Check the product still exists.
      if (!productSnap.exists) {
        throw new HttpsError("failed-precondition", "One of the items in your cart is no longer available.");
      }
      const product = productSnap.data();

      // Check the product is still active.
      if (product.isActive === false) {
        throw new HttpsError("failed-precondition", `"${product.name}" is no longer available.`);
      }

      // Check there's enough stock.
      if (product.stockQuantity !== null && product.stockQuantity !== undefined && product.stockQuantity < cartItem.quantity) {
        throw new HttpsError("failed-precondition", `Not enough stock for "${product.name}" (${product.stockQuantity} left).`);
      }

      const lineTotal = product.price * cartItem.quantity;
      total += lineTotal;
      lineItems.push({
        productId: productSnap.id,
        name: product.name,
        unitPrice: product.price,
        quantity: cartItem.quantity,
        lineTotal,
      });
    }

    // Write the order.
    tx.set(orderRef, {
      clientId: uid,
      items: lineItems,
      productIds: lineItems.map((li) => li.productId),
      total,
      deliveryAddress: deliveryAddress || null,
      notes: notes || "",
      status: "pending",
      createdAt: admin.firestore.FieldValue.serverTimestamp(),
    });

    // Decrement stock for tracked products.
    for (let i = 0; i < lineItems.length; i++) {
      const product = productSnaps[i].data();
      if (product.stockQuantity !== null && product.stockQuantity !== undefined) {
        tx.update(productRefs[i], { stockQuantity: product.stockQuantity - lineItems[i].quantity });
      }
    }

    // Clear the cart.
    for (const cartDoc of cartDocs) {
      tx.delete(cartDoc.ref);
    }

    return { total };
  });

  return { orderId: orderRef.id, total: result.total };
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