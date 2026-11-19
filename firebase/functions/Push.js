/**
 * Push token + preference for the signed-in client.
 *
 * registerPushToken { token }     saves users/{uid}.fcmToken (the phone calls this after login)
 * clearPushToken    {}            removes it (the phone calls this on logout)
 * setPushPreference { enabled }   users/{uid}.pushEnabled  true/false
 * getPushPreference {}            -> { enabled }
 *
 * Done on the server so Firestore rules can stay strict.
 */

const { onCall, HttpsError } = require("./secure");
const admin = require("firebase-admin");

if (admin.apps.length === 0) {
  admin.initializeApp();
}
const db = admin.firestore();

function uidOf(request) {
  const uid = request.auth && request.auth.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Please log in first.");
  return uid;
}

exports.registerPushToken = onCall(async (request) => {
  const uid = uidOf(request);
  const token = String((request.data && request.data.token) || "").trim();
  if (token.length < 20 || token.length > 4096) throw new HttpsError("invalid-argument", "A valid token is required.");

  // One phone token belongs to one account: remove it from anyone else who had it.
  const others = await db.collection("users").where("fcmToken", "==", token).get();
  const batch = db.batch();
  others.docs.filter((d) => d.id !== uid).forEach((d) => batch.update(d.ref, { fcmToken: admin.firestore.FieldValue.delete() }));
  batch.set(db.collection("users").doc(uid), {
    fcmToken: token,
    fcmTokenUpdatedAt: admin.firestore.FieldValue.serverTimestamp(),
  }, { merge: true });
  await batch.commit();
  return { ok: true };
});

exports.clearPushToken = onCall(async (request) => {
  const uid = uidOf(request);
  await db.collection("users").doc(uid).set({ fcmToken: admin.firestore.FieldValue.delete() }, { merge: true });
  return { ok: true };
});

exports.setPushPreference = onCall(async (request) => {
  const uid = uidOf(request);
  const enabled = request.data && request.data.enabled;
  if (typeof enabled !== "boolean") throw new HttpsError("invalid-argument", "enabled must be true or false.");
  await db.collection("users").doc(uid).set({ pushEnabled: enabled }, { merge: true });
  return { enabled };
});

exports.getPushPreference = onCall(async (request) => {
  const uid = uidOf(request);
  const snap = await db.collection("users").doc(uid).get();
  return { enabled: !(snap.exists && snap.data().pushEnabled === false) };
});
