/**
 * deletionLog.js
 * -----------------------------------------------------------------------
 * This file handles the deletion audit log.
 *
 * There are TWO parts here:
 *   1. logDeletion — a helper function (NOT a Cloud Function) that other
 *      files import and call when they delete something. Currently used by:
 *        - adminServices.js (deleteService)
 *        - shop.js (deleteProduct)
 *
 *   2. listDeletionLog — a real Cloud Function that admins can call to
 *      see the log.
 *
 * Why logDeletion is a helper, not a function:
 *   It's called AFTER the real delete succeeds. If the audit write fails,
 *   we don't want it to block or roll back the deletion. But we also don't
 *   want it to silently vanish. So callers await it, but it can't fail
 *   the deletion itself.
 * -----------------------------------------------------------------------
 */

const { onCall, HttpsError } = require("firebase-functions/v2/https");
const admin = require("firebase-admin");

// Initialize Firebase if not already done.
if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

/**
 * logDeletion
 * Input: { entityType, entityId, deletedBy, reason? }
 *
 * A helper function (NOT a Cloud Function) that other files call
 * after they delete something.
 *
 * @param {{ entityType: string, entityId: string, deletedBy: string, reason?: string }} params
 */
async function logDeletion({ entityType, entityId, deletedBy, reason }) {
  await db.collection("deletionLog").add({
    entityType,
    entityId,
    deletedBy,
    reason: reason || "",
    deletedAt: admin.firestore.FieldValue.serverTimestamp(),
  });
}

// Helper: throws an error if the caller isn't an admin or super admin.
function assertIsAdminOrSuperAdmin(request) {
  if (!request.auth) throw new HttpsError("unauthenticated", "Must be signed in.");
  if (request.auth.token.admin !== true && request.auth.token.superAdmin !== true) {
    throw new HttpsError("permission-denied", "Admin access required.");
  }
}

/**
 * listDeletionLog
 * Input: { entityType? }
 * Output: { entries }
 *
 * Admin only. Lists the deletion log (newest first).
 * Optionally filtered by entity type.
 */
exports.listDeletionLog = onCall(async (request) => {
  assertIsAdminOrSuperAdmin(request);
  const { entityType } = request.data || {};

  let query = db.collection("deletionLog").orderBy("deletedAt", "desc").limit(200);
  if (entityType) query = query.where("entityType", "==", entityType);

  const snap = await query.get();
  return { entries: snap.docs.map((d) => ({ id: d.id, ...d.data() })) };
});

// Export logDeletion too, so other files can do:
//   const { logDeletion } = require("./deletionLog");
//
// This is safe to flow through index.js's spread — Firebase only picks
// up functions made with onCall/onRequest/etc. A plain function like
// this one is just ignored as "not a trigger."
exports.logDeletion = logDeletion;