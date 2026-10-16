/**
 * bootstrapSuperAdmin.js
 * Creates the first super admin in the Firebase project.
 * Run ONCE, on your computer, before using the admin app.
 *
 */

// Import Firebase Admin (v13+ uses modular imports)
const { initializeApp, cert } = require("firebase-admin/app");
const { getAuth } = require("firebase-admin/auth");

// Load the service account key (must be next to this script)
const serviceAccount = require("./serviceAccountKey.json");

// The UID is the first argument after the script name
const targetUid = process.argv[2];

if (!targetUid) {
  console.error("Usage: node bootstrapSuperAdmin.js <uid>");
  process.exit(1);
}

// Connect to Firebase using the service account key
initializeApp({
  credential: cert(serviceAccount),
});

async function main() {
  const auth = getAuth();

  // Give this user both admin and superAdmin roles
  await auth.setCustomUserClaims(targetUid, {
    admin: true,
    superAdmin: true,
  });

  // Force their old tokens to stop working
  await auth.revokeRefreshTokens(targetUid);

  // Get the user so we can show their email
  const user = await auth.getUser(targetUid);

  console.log(`Done. ${user.email || targetUid} is now a super admin.`);
  console.log("They need to sign out and back in for this to take effect.");
  process.exit(0);
}

main().catch((err) => {
  console.error("Failed to set super admin claim:", err);
  process.exit(1);
});