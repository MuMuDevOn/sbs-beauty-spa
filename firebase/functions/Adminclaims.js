/**
 * This file sets request.auth.token.admin, which every admin-only
 * function in the app checks.
 *
 * There are two claims
 *   admin      — can manage bookings, services, hours, etc.
 *   superAdmin — can do everything admin can do, PLUS grant/revoke
 *                admin access to other users. superAdmin implies admin.
 *
 *   The first superAdmin is set with a one-off local script:
 *   scripts/bootstrapSuperAdmin.js — run once, outside the deployed app.
 *
 *   PROTECTED_EMAILS (below) are owner accounts: nobody can demote them
 *   or remove their roles through the app.
 */

const { onCall, HttpsError } = require("./secure");
const admin = require("firebase-admin");

// Initialize Firebase if not already done.
if (admin.apps.length === 0) {
  admin.initializeApp();
}

// Accounts that can never be demoted or have their roles removed.
// When the client takes over, replace this with their real email.
const PROTECTED_EMAILS = ["matsworkbasedbrand@gmail.com"];

function isProtectedEmail(email) {
  return PROTECTED_EMAILS.includes((email || "").toLowerCase());
}

// Helper: throws an error if the target account is a protected owner.
async function assertNotProtected(targetUid) {
  const target = await admin.auth().getUser(targetUid);
  if (isProtectedEmail(target.email)) {
    throw new HttpsError("permission-denied", "The owner account cannot be changed.");
  }
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

  // The owner account can never be changed.
  await assertNotProtected(targetUid);

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

  // The owner account can never be changed.
  await assertNotProtected(targetUid);

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
 * Output: { admins: [{ uid, email, admin, superAdmin, isOwner }, ...] }
 *
 * Super admin only. Lists all admin and super admin users.
 * isOwner is true for protected accounts (so the app can hide their buttons).
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
          isOwner: isProtectedEmail(user.email),
        });
      }
    }

    pageToken = result.pageToken;
    pagesFetched += 1;
  } while (pageToken && pagesFetched < 5); // Cap at 5000 users

  return { admins };
});

/**
 * addStaffByEmail
 * Input:  { email: string }
 * Output: { uid, email, admin: true }
 *
 * Super admin only. Finds an existing account by email and makes it staff
 * (plain admin). The person must have registered in the app first.
 */
exports.addStaffByEmail = onCall(async (request) => {
  assertIsSuperAdmin(request);

  const email = String((request.data && request.data.email) || "").trim().toLowerCase();
  if (!email || !email.includes("@")) {
    throw new HttpsError("invalid-argument", "Enter a valid email address.");
  }

  let user;
  try {
    user = await admin.auth().getUserByEmail(email);
  } catch (err) {
    if (err.code === "auth/user-not-found") {
      throw new HttpsError(
        "not-found",
        "No account found for that email. They need to register in the app first."
      );
    }
    throw err;
  }

  const existingClaims = user.customClaims || {};
  if (existingClaims.admin === true || existingClaims.superAdmin === true) {
    throw new HttpsError("already-exists", "That person is already staff.");
  }

  await admin.auth().setCustomUserClaims(user.uid, { ...existingClaims, admin: true });
  // Their old token expires so the new role applies on next sign-in.
  await admin.auth().revokeRefreshTokens(user.uid);

  return { uid: user.uid, email: user.email, admin: true };
});

/**
 * getMyClaims
 * Output: { admin, superAdmin }
 * Tells the app what the signed-in user is allowed to do.
 */
exports.getMyClaims = onCall(async (request) => {
  if (!request.auth) {
    throw new HttpsError("unauthenticated", "Must be signed in.");
  }
  return {
    admin: request.auth.token.admin === true,
    superAdmin: request.auth.token.superAdmin === true,
  };
});