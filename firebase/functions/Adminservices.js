/**
 * adminServices.js
 * -----------------------------------------------------------------------
 * This file handles full CRUD for the Service catalog.
 *
 * The services managed here are the SAME services that getAvailableSlots
 * and createBooking read from. So the validation done here (positive
 * duration, non-negative price, deposit never exceeding price) is what
 * keeps the availability engine from getting bad data.
 *
 * Note on deleteService:
 *   It's a hard delete, but guarded. It refuses if the service has any
 *   pending/confirmed bookings that use it as the primary service.
 *   In that case, you should DEACTIVATE instead of delete.
 *
 *   (Note: the guard only checks the primary `serviceId` field. A service
 *   used only as someone's add-on wouldn't be caught. That's why
 *   deactivating is the safer everyday tool, and deleting is only for
 *   cleanup of something that was never actually used.)
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
  if (!request.auth) {
    throw new HttpsError("unauthenticated", "Must be signed in.");
  }
  if (request.auth.token.admin !== true) {
    throw new HttpsError("permission-denied", "Admin access required.");
  }
}

// Validates the input for creating or updating a service.
function assertValidServiceInput({ name, categoryId, price, durationMinutes, depositAmount, bufferMinutes }) {
  if (!name || typeof name !== "string") {
    throw new HttpsError("invalid-argument", "name is required.");
  }
  if (!categoryId || typeof categoryId !== "string") {
    throw new HttpsError("invalid-argument", "categoryId is required.");
  }
  if (typeof price !== "number" || price < 0) {
    throw new HttpsError("invalid-argument", "price must be a non-negative number.");
  }
  if (typeof durationMinutes !== "number" || durationMinutes <= 0) {
    throw new HttpsError("invalid-argument", "durationMinutes must be a positive number.");
  }
  if (depositAmount !== undefined && depositAmount !== null) {
    if (typeof depositAmount !== "number" || depositAmount < 0) {
      throw new HttpsError("invalid-argument", "depositAmount must be a non-negative number.");
    }
    if (depositAmount > price) {
      throw new HttpsError("invalid-argument", "depositAmount cannot exceed price.");
    }
  }
  if (bufferMinutes !== undefined && bufferMinutes !== null) {
    if (typeof bufferMinutes !== "number" || bufferMinutes < 0) {
      throw new HttpsError("invalid-argument", "bufferMinutes must be a non-negative number.");
    }
  }
}

/**
 * createService
 * Input:  { name, description?, categoryId, price, depositAmount?,
 *           durationMinutes, bufferMinutes?, imageUrl?, addOnServiceIds? }
 * Output: { serviceId }
 *
 * Admin only. Creates a new service.
 */
exports.createService = onCall(async (request) => {
  assertIsAdmin(request);

  const data = request.data || {};
  assertValidServiceInput(data);

  const docRef = await db.collection("services").add({
    name: data.name,
    description: data.description || "",
    categoryId: data.categoryId,
    price: data.price,
    depositAmount: data.depositAmount || 0,
    durationMinutes: data.durationMinutes,
    bufferMinutes: data.bufferMinutes || 0,
    imageUrl: data.imageUrl || null,
    addOnServiceIds: Array.isArray(data.addOnServiceIds) ? data.addOnServiceIds : [],
    isActive: true,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
    createdBy: request.auth.uid,
  });

  return { serviceId: docRef.id };
});

/**
 * updateService
 * Input:  { serviceId, name?, description?, categoryId?, price?,
 *           depositAmount?, durationMinutes?, bufferMinutes?,
 *           imageUrl?, addOnServiceIds? }
 * Output: { serviceId }
 *
 * Admin only. Updates an existing service.
 * Only fields you actually send get changed. Omit a field to leave it
 * as-is. Don't send null to "clear" a field (except imageUrl, which
 * explicitly accepts null).
 */
exports.updateService = onCall(async (request) => {
  assertIsAdmin(request);

  const { serviceId, ...fields } = request.data || {};
  if (!serviceId || typeof serviceId !== "string") {
    throw new HttpsError("invalid-argument", "serviceId is required.");
  }

  // Check the service exists.
  const ref = db.collection("services").doc(serviceId);
  const snap = await ref.get();
  if (!snap.exists) {
    throw new HttpsError("not-found", "Service not found.");
  }

  // Only copy the fields that can be edited AND were actually sent.
  const editable = ["name", "description", "categoryId", "price", "depositAmount", "durationMinutes", "bufferMinutes", "imageUrl", "addOnServiceIds"];
  const update = {};
  for (const key of editable) {
    if (key in fields) {
      update[key] = fields[key];
    }
  }
  if (Object.keys(update).length === 0) {
    throw new HttpsError("invalid-argument", "No editable fields were provided.");
  }

  // Validate the MERGED result (existing values + whatever's changing).
  // This stops a partial update from leaving the service in an invalid
  // state — like lowering the price below an unchanged depositAmount.
  const merged = { ...snap.data(), ...update };
  assertValidServiceInput(merged);

  update.updatedAt = admin.firestore.FieldValue.serverTimestamp();
  update.updatedBy = request.auth.uid;

  await ref.update(update);
  return { serviceId };
});

/**
 * setServiceActive
 * Input:  { serviceId, isActive }
 * Output: { serviceId, isActive }
 *
 * Admin only. Turns a service on or off.
 *
 * This is the everyday "remove from the app" tool:
 *   - An inactive service stops showing in the client catalog.
 *   - It stops being bookable.
 *   - But nothing about it is destroyed.
 *   - Past bookings that reference it still display correctly.
 */
exports.setServiceActive = onCall(async (request) => {
  assertIsAdmin(request);

  const { serviceId, isActive } = request.data || {};
  if (!serviceId || typeof isActive !== "boolean") {
    throw new HttpsError("invalid-argument", "serviceId and isActive (boolean) are required.");
  }

  const ref = db.collection("services").doc(serviceId);
  const snap = await ref.get();
  if (!snap.exists) {
    throw new HttpsError("not-found", "Service not found.");
  }

  await ref.update({
    isActive,
    updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    updatedBy: request.auth.uid,
  });

  return { serviceId, isActive };
});

/**
 * deleteService
 * Input:  { serviceId }
 * Output: { deleted: true }
 *
 * Admin only. Deletes a service permanently.
 *
 * Refuses if there's a pending/confirmed booking with this as its
 * primary service. Use setServiceActive instead for anything that has
 * ever actually been booked.
 */
exports.deleteService = onCall(async (request) => {
  assertIsAdmin(request);

  const { serviceId } = request.data || {};
  if (!serviceId || typeof serviceId !== "string") {
    throw new HttpsError("invalid-argument", "serviceId is required.");
  }

  // Check if any active bookings reference this service as their primary.
  const activeBookings = await db
    .collection("bookings")
    .where("serviceId", "==", serviceId)
    .where("status", "in", ["pending", "confirmed"])
    .limit(1)
    .get();

  if (!activeBookings.empty) {
    throw new HttpsError(
      "failed-precondition",
      "This service has upcoming bookings — deactivate it instead of deleting."
    );
  }

  // Delete the service.
  await db.collection("services").doc(serviceId).delete();

  // Log the deletion for audit trail.
  await logDeletion({ entityType: "service", entityId: serviceId, deletedBy: request.auth.uid });

  return { deleted: true };
});