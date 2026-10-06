/**
 * adminClaims.js
 * -----------------------------------------------------------------------
 * This file sets request.auth.token.admin, which every admin-only
 * function in the app checks.
 *
 * There are two claims, matching the "Admin" CRC card:
 *
 *   admin      — can manage bookings, services, hours, etc.
 *   superAdmin — can do everything admin can do, PLUS grant/revoke
 *                admin access to other users. superAdmin implies admin.
 *
 * Why custom claims instead of a Firestore "role" field?
 *   Claims travel inside the user's ID token. So every function can
 *   check request.auth.token.admin with ZERO extra reads — no Firestore
 *   lookup, no risk of the check and the data drifting apart.
 *
 *   Trade-off: A change here doesn't take effect until the client gets
 *   a new ID token (see the note on revokeRefreshTokens below).
 *
 * THE BOOTSTRAP PROBLEM:
 *   These functions require an existing superAdmin to call them. So they
 *   can't create the very first one — that would need a publicly-callable
 *   function, which is a security hole.
 *
 *   Instead, the first superAdmin is set with a one-off local script:
 *   scripts/bootstrapSuperAdmin.js — run once, outside the deployed app.
 * -----------------------------------------------------------------------
 */

const { onCall, HttpsError } = require("firebase-functions/v2/https");
const admin = require("firebase-admin");

// Initialize Firebase if not already done.
if (admin.apps.length === 0) {
  admin.initializeApp();
}

// Helper: throws an error if the caller isn't a super admin.
function assertIsSuperAdmin(request) {
  if (!request.auth) {
    throw new HttpsError("unauthenticated", "Must be signed in.");
  }
  if (request.auth.token.superAdmin !== true) {
    throw new HttpsError("permission-denied", "Super admin access required.");
  }
}

/**
 * setAdminClaim
 * Input:  { targetUid, makeAdmin: boolean }
 * Output: { uid, admin }
 *
 * Super admin only. Grants or revokes plain admin access.
 */
exports.setAdminClaim = onCall(async (request) => {
  assertIsSuperAdmin(request);

  const { targetUid, makeAdmin } = request.data || {};
  if (!targetUid || typeof makeAdmin !== "boolean") {
    throw new HttpsError("invalid-argument", "targetUid and makeAdmin (boolean) are required.");
  }

  // Load the target user.
  const targetUser = await admin.auth().getUser(targetUid);
  const existingClaims = targetUser.customClaims || {};

  // Prevent a super admin from losing plain admin access.
  // (A super admin always has admin too — that's implied.)
  if (existingClaims.superAdmin === true && makeAdmin === false) {
    throw new HttpsError(
      "failed-precondition",
      "This user is a super admin — remove super admin access first, then revoke admin."
    );
  }

  // Update the claims.
  const newClaims = { ...existingClaims, admin: makeAdmin };
  await admin.auth().setCustomUserClaims(targetUid, newClaims);

  // Force their old token to expire.
  // Without this, the change wouldn't take effect until they sign in again.
  await admin.auth().revokeRefreshTokens(targetUid);

  return { uid: targetUid, admin: makeAdmin };
});

/**
 * setSuperAdminClaim
 * Input:  { targetUid, makeSuperAdmin: boolean }
 * Output: { uid, superAdmin, admin }
 *
 * Super admin only. Grants or revokes super admin access.
 *
 * Granting super admin also grants admin (super admin implies admin).
 * Revoking super admin leaves plain admin access untouched —
 * call setAdminClaim separately if you want to remove that too.
 */
exports.setSuperAdminClaim = onCall(async (request) => {
  assertIsSuperAdmin(request);

  const { targetUid, makeSuperAdmin } = request.data || {};
  if (!targetUid || typeof makeSuperAdmin !== "boolean") {
    throw new HttpsError("invalid-argument", "targetUid and makeSuperAdmin (boolean) are required.");
  }

  // Prevent a super admin from removing their own super admin access.
  // (This avoids accidentally locking out the last super admin.)
  if (targetUid === request.auth.uid && makeSuperAdmin === false) {
    throw new HttpsError(
      "failed-precondition",
      "You can't remove your own super admin access — have another super admin do it, so the account is never accidentally locked out."
    );
  }

  const targetUser = await admin.auth().getUser(targetUid);
  const existingClaims = targetUser.customClaims || {};

  // When granting super admin, also grant plain admin.
  // When revoking super admin, keep plain admin as-is.
  const newClaims = {
    ...existingClaims,
    superAdmin: makeSuperAdmin,
    admin: makeSuperAdmin ? true : existingClaims.admin === true,
  };
  await admin.auth().setCustomUserClaims(targetUid, newClaims);
  await admin.auth().revokeRefreshTokens(targetUid);

  return { uid: targetUid, superAdmin: makeSuperAdmin, admin: newClaims.admin };
});

/**
 * listAdmins
 * Output: { admins: [{ uid, email, admin, superAdmin }, ...] }
 *
 * Super admin only. Lists all admin and super admin users.
 *
 * NOTE: The Admin SDK can't query users by custom claim directly.
 * So this pages through all users and filters in memory.
 * (Capped at 5 pages / 5000 users — way more than a single salon needs.)
 */
exports.listAdmins = onCall(async (request) => {
  assertIsSuperAdmin(request);

  const admins = [];
  let pageToken;
  let pagesFetched = 0;

  do {
    // Get the next page of users (1000 at a time).
    const result = await admin.auth().listUsers(1000, pageToken);

    // Filter to only admin/superAdmin users.
    for (const user of result.users) {
      const claims = user.customClaims || {};
      if (claims.admin === true || claims.superAdmin === true) {
        admins.push({
          uid: user.uid,
          email: user.email || null,
          admin: claims.admin === true,
          superAdmin: claims.superAdmin === true,
        });
      }
    }

    pageToken = result.pageToken;
    pagesFetched += 1;
  } while (pageToken && pagesFetched < 5); // Cap at 5000 users

  return { admins };
});