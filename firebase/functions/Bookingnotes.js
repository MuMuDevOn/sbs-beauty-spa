/**
 * bookingNotes.js
 * -----------------------------------------------------------------------
 * This file handles internal staff notes about a specific appointment.
 *
 * Examples of notes:
 *   - "Client ran 20 minutes late"
 *   - "Used a different shade than requested"
 *   - "Client mentioned an allergy to gel"
 *
 * IMPORTANT: These notes are NEVER shown to the client.
 * They're completely separate from Booking.notes (which the client
 * writes themselves when booking).
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

/**
 * addBookingNote
 * Input: { bookingId, note }
 * Output: { noteId }
 *
 * Admin only. Adds a note to a booking.
 */
exports.addBookingNote = onCall(async (request) => {
  assertIsAdmin(request);
  const { bookingId, note } = request.data || {};

  // Check the input is valid.
  if (!bookingId || !note || !note.trim()) {
    throw new HttpsError("invalid-argument", "bookingId and a non-empty note are required.");
  }

  // Check the booking exists.
  const bookingSnap = await db.collection("bookings").doc(bookingId).get();
  if (!bookingSnap.exists) throw new HttpsError("not-found", "Booking not found.");

  // Save the note.
  const docRef = await db.collection("bookingNotes").add({
    bookingId,
    note: note.trim(),
    createdBy: request.auth.uid,
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });
  return { noteId: docRef.id };
});

/**
 * listBookingNotes
 * Input: { bookingId }
 * Output: { notes }
 *
 * Admin only. Lists all notes for a booking (newest first).
 */
exports.listBookingNotes = onCall(async (request) => {
  assertIsAdmin(request);
  const { bookingId } = request.data || {};
  if (!bookingId) throw new HttpsError("invalid-argument", "bookingId is required.");

  // Load notes, newest first.
  const snap = await db.collection("bookingNotes").where("bookingId", "==", bookingId).orderBy("createdAt", "desc").get();

  const notes = snap.docs.map((d) => ({
    id: d.id,
    note: d.data().note,
    createdAt: d.data().createdAt ? d.data().createdAt.toDate().toISOString() : null,
  }));
  return { notes };
});