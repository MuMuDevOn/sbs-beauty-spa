/**
 * scripts/bootstrapSuperAdmin.js
 * -----------------------------------------------------------------------
 * Run this ONCE, on your own computer, to create the very first super admin.
 *
 * This is NOT a Cloud Function. It is NOT deployed anywhere. It runs only
 * on your machine using a secret service account key. That's what makes it
 * safe — nobody on the internet can call it, because it isn't online at all.
 *
 * After this first super admin exists, all future admin/superAdmin changes
 * go through setAdminClaim / setSuperAdminClaim (in adminClaims.js) instead.
 * Those are the online, permission-checked versions.
 *
 * Setup (do this once):
 *   1. Go to Firebase Console → Project Settings → Service Accounts →
 *      click "Generate new private key" and save the file as
 *      serviceAccountKey.json NEXT TO this script.
 *      Never upload this file to GitHub — add it to .gitignore.
 *   2. Run: npm install firebase-admin
 *      (either in this scripts/ folder, or reuse the functions/ folder's
 *      node_modules if you run it from there)
 *
 * How to use:
 *   node scripts/bootstrapSuperAdmin.js <uid-of-the-user-to-promote>
 *
 * Where to find the uid:
 *   Firebase Console → Authentication → Users
 *   Or have that person sign up in the app first, then copy their uid.
 * -----------------------------------------------------------------------
 */

const admin = require("firebase-admin");
const serviceAccount = require("./serviceAccountKey.json");

// The uid is passed as the first argument after the script name.
const targetUid = process.argv[2];

if (!targetUid) {
  console.error("Usage: node bootstrapSuperAdmin.js <uid>");
  process.exit(1);
}

// Connect to Firebase using the service account key.
admin.initializeApp({
  credential: admin.credential.cert(serviceAccount),
});

async function main() {
  // Give this user the admin and superAdmin roles.
  await admin.auth().setCustomUserClaims(targetUid, {
    admin: true,
    superAdmin: true,
  });

  // Force their old tokens to stop working.
  // This makes sure the new roles take effect.
  await admin.auth().revokeRefreshTokens(targetUid);

  // Get the user so we can show their email in the output.
  const user = await admin.auth().getUser(targetUid);
  console.log(`Done. ${user.email || targetUid} is now a super admin.`);
  console.log("They need to sign out and back in (or otherwise refresh their ID token) for this to take effect.");
  process.exit(0);
}

main().catch((err) => {
  console.error("Failed to set super admin claim:", err);
  process.exit(1);
});