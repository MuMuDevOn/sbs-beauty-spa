/**
 * gallery.js
 * -----------------------------------------------------------------------
 * This file handles the gallery (portfolio images).
 *
 * Important:
 *   - The images themselves live in Firebase Storage.
 *   - The admin app uploads them directly using the Storage SDK.
 *   - These functions only manage the Firestore records that POINT
 *     at the uploaded images.
 *
 * NOTE: The rules for who can upload to Storage live in storage.rules,
 * which is a separate file. It's not shown here — that's a real gap
 * that needs to be filled.
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

/**
 * createGalleryCategory
 * Input: { name }
 * Output: { categoryId }
 *
 * Admin only. Creates a new gallery category.
 */
exports.createGalleryCategory = onCall(async (request) => {
  assertIsAdmin(request);
  const { name } = request.data || {};
  if (!name) throw new HttpsError("invalid-argument", "name is required.");

  const docRef = await db.collection("galleryCategories").add({
    name,
    isActive: true,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });
  return { categoryId: docRef.id };
});

/**
 * addGalleryImage
 * Input: { categoryId, imageUrl, title?, description? }
 * Output: { imageId }
 *
 * Admin only. Adds a Firestore record pointing at an uploaded image.
 * The imageUrl must already point at a file in Firebase Storage.
 */
exports.addGalleryImage = onCall(async (request) => {
  assertIsAdmin(request);
  const { categoryId, imageUrl, title, description } = request.data || {};
  if (!categoryId || !imageUrl) {
    throw new HttpsError("invalid-argument", "categoryId and imageUrl are required.");
  }

  const docRef = await db.collection("galleryImages").add({
    categoryId,
    imageUrl,
    title: title || "",
    description: description || "",
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
    createdBy: request.auth.uid,
  });
  return { imageId: docRef.id };
});

/**
 * deleteGalleryImage
 * Input: { imageId }
 * Output: { deleted: true }
 *
 * Admin only. Removes the Firestore record.
 *
 * NOTE: This does NOT delete the actual file from Firebase Storage.
 * That's a separate cleanup task (see the note at the top of this file).
 */
exports.deleteGalleryImage = onCall(async (request) => {
  assertIsAdmin(request);
  const { imageId } = request.data || {};
  if (!imageId) throw new HttpsError("invalid-argument", "imageId is required.");

  await db.collection("galleryImages").doc(imageId).delete();
  await logDeletion({ entityType: "galleryImage", entityId: imageId, deletedBy: request.auth.uid });

  return { deleted: true };
});