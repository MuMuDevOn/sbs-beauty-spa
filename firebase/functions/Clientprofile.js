/**
 * clientProfile.js
 * -----------------------------------------------------------------------
 * This file handles two things:
 *   1. ClientPreference — a client managing their own preferences
 *      (preferred staff, contact method).
 *   2. ClientNote — staff-authored notes ABOUT a client (allergies,
 *      past issues, preferences noticed in person).
 *
 * Why preferences use a Cloud Function:
 *   Even though it's low-stakes, setMyPreferences validates the
 *   contact method value instead of trusting whatever string the
 *   client sends.
 *
 * Why notes are admin-only:
 *   A client never sees notes about themselves — only admins can
 *   read or write them.
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

// The only valid contact methods a client can pick.
const CONTACT_METHODS = ["email", "sms", "phone"];

/**
 * getMyPreferences
 * Output: { preferredStaffId, preferredContactMethod }
 *
 * Returns the current user's preferences.
 * Any signed-in user can call this.
 */
exports.getMyPreferences = onCall(async (request) => {
  if (!request.auth) throw new HttpsError("unauthenticated", "Must be signed in.");

  // Load the preferences doc for this user.
  const snap = await db.collection("clientPreferences").doc(request.auth.uid).get();
  const data = snap.exists ? snap.data() : {};

  return {
    preferredStaffId: data.preferredStaffId || null,
    preferredContactMethod: data.preferredContactMethod || null,
  };
});

/**
 * setMyPreferences
 * Input: { preferredStaffId?, preferredContactMethod? }
 *
 * Saves the current user's preferences.
 * Any signed-in user can call this.
 */
exports.setMyPreferences = onCall(async (request) => {
  if (!request.auth) throw new HttpsError("unauthenticated", "Must be signed in.");

  const { preferredStaffId, preferredContactMethod } = request.data || {};

  // Check the contact method is one of the allowed values.
  if (preferredContactMethod && !CONTACT_METHODS.includes(preferredContactMethod)) {
    throw new HttpsError("invalid-argument", `preferredContactMethod must be one of: ${CONTACT_METHODS.join(", ")}.`);
  }

  // Save the preferences. The "merge: true" means only the provided
  // fields are updated — anything else stays the same.
  await db.collection("clientPreferences").doc(request.auth.uid).set(
    {
      preferredStaffId: preferredStaffId || null,
      preferredContactMethod: preferredContactMethod || null,
      updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    },
    { merge: true }
  );

  return { success: true };
});

/**
 * addClientNote
 * Input: { clientId, note }
 *
 * Adds a note about a client. Admin only.
 */
exports.addClientNote = onCall(async (request) => {
  assertIsAdmin(request);

  const { clientId, note } = request.data || {};

  // Check the input is valid.
  if (!clientId || !note || typeof note !== "string" || !note.trim()) {
    throw new HttpsError("invalid-argument", "clientId and a non-empty note are required.");
  }

  // Save the note.
  const docRef = await db.collection("clientNotes").add({
    clientId,
    note: note.trim(),
    createdBy: request.auth.uid,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });

  return { noteId: docRef.id };
});

/**
 * listClientNotes
 * Input: { clientId }
 *
 * Lists all notes about a client. Admin only.
 */
exports.listClientNotes = onCall(async (request) => {
  assertIsAdmin(request);

  const { clientId } = request.data || {};
  if (!clientId) throw new HttpsError("invalid-argument", "clientId is required.");

  // Load notes for this client, newest first.
  const snap = await db.collection("clientNotes").where("clientId", "==", clientId).orderBy("createdAt", "desc").get();

  const notes = snap.docs.map((d) => ({
    id: d.id,
    note: d.data().note,
    createdAt: d.data().createdAt ? d.data().createdAt.toDate().toISOString() : null,
  }));

  return { notes };
});