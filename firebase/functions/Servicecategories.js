/**
 * serviceCategories.js
 * -----------------------------------------------------------------------
 * This file handles service categories (nails, lashes, press-ons).
 *
 * Categories are what each service points to (via Service.categoryId).
 *
 * Reading the list is a direct Firestore read (the rules allow public
 * reads of serviceCategories). Only the WRITES go through functions here.
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

/**
 * createServiceCategory
 * Input: { name, description? }
 * Output: { categoryId }
 *
 * Admin only. Creates a new category.
 */
exports.createServiceCategory = onCall(async (request) => {
  assertIsAdmin(request);
  const { name, description } = request.data || {};
  if (!name || typeof name !== "string") throw new HttpsError("invalid-argument", "name is required.");

  const docRef = await db.collection("serviceCategories").add({
    name,
    description: description || "",
    isActive: true,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });
  return { categoryId: docRef.id };
});

/**
 * updateServiceCategory
 * Input: { categoryId, name?, description? }
 * Output: { categoryId }
 *
 * Admin only. Updates an existing category.
 */
exports.updateServiceCategory = onCall(async (request) => {
  assertIsAdmin(request);
  const { categoryId, name, description } = request.data || {};
  if (!categoryId) throw new HttpsError("invalid-argument", "categoryId is required.");

  // Check the category exists.
  const ref = db.collection("serviceCategories").doc(categoryId);
  if (!(await ref.get()).exists) throw new HttpsError("not-found", "Category not found.");

  // Only update fields that were provided.
  const update = { updatedAt: admin.firestore.FieldValue.serverTimestamp() };
  if (name !== undefined) update.name = name;
  if (description !== undefined) update.description = description;

  await ref.update(update);
  return { categoryId };
});

/**
 * setServiceCategoryActive
 * Input: { categoryId, isActive }
 * Output: { categoryId, isActive }
 *
 * Admin only. Turns a category on or off.
 */
exports.setServiceCategoryActive = onCall(async (request) => {
  assertIsAdmin(request);
  const { categoryId, isActive } = request.data || {};
  if (!categoryId || typeof isActive !== "boolean") {
    throw new HttpsError("invalid-argument", "categoryId and isActive (boolean) are required.");
  }

  const ref = db.collection("serviceCategories").doc(categoryId);
  if (!(await ref.get()).exists) throw new HttpsError("not-found", "Category not found.");

  await ref.update({ isActive, updatedAt: admin.firestore.FieldValue.serverTimestamp() });
  return { categoryId, isActive };
});