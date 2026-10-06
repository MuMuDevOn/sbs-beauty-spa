/**
 * businessSettings.js
 * -----------------------------------------------------------------------
 * This file handles the business settings for a single-location business.
 *
 * There are TWO settings documents:
 *   1. businessSettingsPublic/main  — name, contact, address, logo,
 *                                     cancellation policy, terms
 *   2. businessSettingsPrivate/main — bank details, payment notes
 *
 * The public settings can be read by ANYONE (even before login).
 * The private settings can only be read by admins.
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

// These two docs are singletons — there's only ever one of each.
const PUBLIC_DOC = db.collection("businessSettingsPublic").doc("main");
const PRIVATE_DOC = db.collection("businessSettingsPrivate").doc("main");

/**
 * getBusinessSettingsPublic
 * Output: { settings }
 *
 * Anyone can call this — even before signing in.
 * Used for the "About us" screen and the cancellation policy text
 * shown before a client signs up.
 */
exports.getBusinessSettingsPublic = onCall(async () => {
  const snap = await PUBLIC_DOC.get();
  return { settings: snap.exists ? snap.data() : null };
});

/**
 * getBusinessSettingsPrivate
 * Output: { settings }
 *
 * Admin only — contains bank details and payment notes.
 */
exports.getBusinessSettingsPrivate = onCall(async (request) => {
  assertIsAdmin(request);
  const snap = await PRIVATE_DOC.get();
  return { settings: snap.exists ? snap.data() : null };
});

/**
 * updateBusinessSettingsPublic
 * Input: { name?, phone?, email?, address?, logoUrl?, description?,
 *          cancellationPolicy?, termsAndConditions? }
 *
 * Admin only. Updates the public settings.
 */
exports.updateBusinessSettingsPublic = onCall(async (request) => {
  assertIsAdmin(request);

  // Only these fields can be updated.
  const editable = ["name", "phone", "email", "address", "logoUrl", "description", "cancellationPolicy", "termsAndConditions"];

  // Copy only the fields that were actually provided.
  const update = {};
  for (const key of editable) {
    if (key in (request.data || {})) update[key] = request.data[key];
  }

  // If no editable fields were provided, error out.
  if (Object.keys(update).length === 0) {
    throw new HttpsError("invalid-argument", "No editable fields were provided.");
  }

  // Add tracking info.
  update.updatedAt = admin.firestore.FieldValue.serverTimestamp();
  update.updatedBy = request.auth.uid;

  // Save with merge: true so other fields stay the same.
  await PUBLIC_DOC.set(update, { merge: true });

  return { success: true };
});

/**
 * updateBusinessSettingsPrivate
 * Input: { bankName?, accountName?, accountNumber?, paymentNotes? }
 *
 * Admin only. Updates the private settings.
 */
exports.updateBusinessSettingsPrivate = onCall(async (request) => {
  assertIsAdmin(request);

  const editable = ["bankName", "accountName", "accountNumber", "paymentNotes"];
  const update = {};
  for (const key of editable) {
    if (key in (request.data || {})) update[key] = request.data[key];
  }

  if (Object.keys(update).length === 0) {
    throw new HttpsError("invalid-argument", "No editable fields were provided.");
  }

  update.updatedAt = admin.firestore.FieldValue.serverTimestamp();
  update.updatedBy = request.auth.uid;

  await PRIVATE_DOC.set(update, { merge: true });

  return { success: true };
});