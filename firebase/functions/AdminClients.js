/**
 * Admin-only reads and writes for client (customer) management.
 *
 * Clients live in Firebase Auth (email/password) AND have a profile doc
 * in Firestore at users/{uid}. This file reads that combined view.
 *
 * The Admin SDK can't query Auth users by custom claim efficiently, so
 * we page through Auth users and join with Firestore profile docs.
 */

const { onCall, HttpsError } = require("firebase-functions/v2/https");
const admin = require("firebase-admin");

if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

function assertIsAdmin(request) {
  if (!request.auth) throw new HttpsError("unauthenticated", "Must be signed in.");
  if (request.auth.token.admin !== true) throw new HttpsError("permission-denied", "Admin access required.");
}

/**
 * listClients
 * Input:  { search?: string, limit?: number }
 * Output: { clients: [{ uid, name, email, phoneNumber, createdAt, isActive }] }
 */
exports.listClients = onCall(async (request) => {
  assertIsAdmin(request);

  const { search, limit = 500 } = request.data || {};
  const searchLower = (search || "").toLowerCase().trim();

  const clients = [];
  let pageToken;
  let pagesFetched = 0;
  const maxPages = Math.ceil(limit / 1000);

  do {
    const result = await admin.auth().listUsers(1000, pageToken);

    for (const user of result.users) {
      const claims = user.customClaims || {};
      // Skip admins/super admins — this is the client list.
      if (claims.admin === true || claims.superAdmin === true) continue;

      if (searchLower) {
        const name = (user.displayName || "").toLowerCase();
        const email = (user.email || "").toLowerCase();
        if (!name.includes(searchLower) && !email.includes(searchLower)) continue;
      }

      clients.push({
        uid: user.uid,
        name: user.displayName || "",
        email: user.email || "",
        phoneNumber: user.phoneNumber || "",
        isActive: user.disabled !== true,
        createdAt: user.metadata.creationTime,
      });

      if (clients.length >= limit) break;
    }

    pageToken = result.pageToken;
    pagesFetched += 1;
  } while (pageToken && pagesFetched < maxPages && clients.length < limit);

  // Enrich with the profile doc (name / phone may live there).
  const profileRefs = clients.map((c) => db.collection("users").doc(c.uid));
  const profileSnaps = await Promise.all(profileRefs.map((r) => r.get()));

  for (let i = 0; i < clients.length; i++) {
    const snap = profileSnaps[i];
    if (snap.exists) {
      const data = snap.data();
      if (!clients[i].name && data.name) clients[i].name = data.name;
      if (!clients[i].phoneNumber && data.phoneNumber) clients[i].phoneNumber = data.phoneNumber;
    }
  }

  return { clients };
});

/**
 * getClientDetails
 * Input:  { clientId }
 * Output: { profile, bookings, notes }
 */
exports.getClientDetails = onCall(async (request) => {
  assertIsAdmin(request);

  const { clientId } = request.data || {};
  if (!clientId || typeof clientId !== "string") {
    throw new HttpsError("invalid-argument", "clientId is required.");
  }

  let profile = null;
  const profileSnap = await db.collection("users").doc(clientId).get();
  if (profileSnap.exists) profile = { id: profileSnap.id, ...profileSnap.data() };

  const bookingsSnap = await db
    .collection("bookings")
    .where("clientId", "==", clientId)
    .orderBy("date", "desc")
    .limit(50)
    .get();
  const bookings = bookingsSnap.docs.map((d) => ({ id: d.id, ...d.data() }));

  const notesSnap = await db
    .collection("clientNotes")
    .where("clientId", "==", clientId)
    .orderBy("createdAt", "desc")
    .get();
  const notes = notesSnap.docs.map((d) => ({
    id: d.id,
    note: d.data().note,
    createdAt: d.data().createdAt ? d.data().createdAt.toDate().toISOString() : null,
  }));

  return { profile, bookings, notes };
});

/**
 * setClientDisabled
 * Input:  { clientId, disabled }
 * Output: { clientId, disabled }
 */
exports.setClientDisabled = onCall(async (request) => {
  assertIsAdmin(request);

  const { clientId, disabled } = request.data || {};
  if (!clientId || typeof disabled !== "boolean") {
    throw new HttpsError("invalid-argument", "clientId and disabled (boolean) are required.");
  }

  await admin.auth().updateUser(clientId, { disabled });
  await admin.auth().revokeRefreshTokens(clientId);

  return { clientId, disabled };
});