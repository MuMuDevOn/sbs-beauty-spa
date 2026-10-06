/**
 * reviews.js
 * -----------------------------------------------------------------------
 * This file handles reviews.
 *
 * Reading reviews for a service is a direct, public Firestore read —
 * nothing trust-sensitive there.
 *
 * Submitting one goes through a Cloud Function because it needs real
 * validation that a direct write can't do:
 *   - Only the client who had that booking can review
 *   - Only after the booking is "completed"
 *   - Only once per booking
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
 * submitReview
 * Input: { bookingId, rating: 1-5, comment? }
 * Output: { reviewId }
 *
 * Called by a signed-in client to submit a review.
 */
exports.submitReview = onCall(async (request) => {
  const uid = request.auth && request.auth.uid;
  if (!uid) throw new HttpsError("unauthenticated", "Must be signed in to leave a review.");

  const { bookingId, rating, comment } = request.data || {};

  // Check the input.
  if (!bookingId) throw new HttpsError("invalid-argument", "bookingId is required.");
  if (!Number.isInteger(rating) || rating < 1 || rating > 5) {
    throw new HttpsError("invalid-argument", "rating must be an integer from 1 to 5.");
  }

  // Load the booking.
  const bookingSnap = await db.collection("bookings").doc(bookingId).get();
  if (!bookingSnap.exists) throw new HttpsError("not-found", "Booking not found.");

  const booking = bookingSnap.data();

  // Check the booking belongs to this user.
  if (booking.clientId !== uid) {
    throw new HttpsError("permission-denied", "This isn't your booking.");
  }

  // Check the booking is completed.
  if (booking.status !== "completed") {
    throw new HttpsError("failed-precondition", "You can only review a completed appointment.");
  }

  // Check they haven't already reviewed this booking.
  const existing = await db.collection("reviews").where("bookingId", "==", bookingId).limit(1).get();
  if (!existing.empty) {
    throw new HttpsError("already-exists", "You've already reviewed this booking.");
  }

  // Save the review.
  const docRef = await db.collection("reviews").add({
    bookingId,
    clientId: uid,
    serviceId: booking.serviceId,
    rating,
    comment: comment || "",
    createdAt: admin.firestore.FieldValue.serverTimestamp(),
  });

  return { reviewId: docRef.id };
});