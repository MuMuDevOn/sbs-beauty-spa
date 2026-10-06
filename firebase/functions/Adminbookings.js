/**
 * adminBookings.js
 * -----------------------------------------------------------------------
 * This file handles the one write an admin needs on a booking:
 * moving its status forward.
 *
 * IMPORTANT: This is NOT a generic "update this booking" function.
 * Only specific, validated transitions are allowed. This means an admin
 * client can't accidentally (or maliciously, if compromised) push a
 * booking into a nonsensical state.
 *
 * Note on cancellation:
 *   Cancelling a booking needs no extra work to free up the slot.
 *   getAvailableSlots already only counts bookings with status
 *   "pending" or "confirmed" as occupying time. So a "cancelled"
 *   booking just stops being counted on the next call.
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
  if (!request.auth) {
    throw new HttpsError("unauthenticated", "Must be signed in.");
  }
  if (request.auth.token.admin !== true) {
    throw new HttpsError("permission-denied", "Admin access required.");
  }
}

// What each current status is allowed to move to.
// Anything not listed here (like trying to "un-complete" a booking)
// is rejected.
const ALLOWED_TRANSITIONS = {
  pending: ["confirmed", "cancelled"],
  confirmed: ["completed", "cancelled"],
  completed: [],  // End state — nothing comes after
  cancelled: [],  // End state — nothing comes after
};

/**
 * updateBookingStatus
 * Input:  { bookingId, newStatus: "confirmed" | "completed" | "cancelled", cancellationReason? }
 * Output: { bookingId, status }
 *
 * Admin only. Changes a booking's status.
 */
exports.updateBookingStatus = onCall(async (request) => {
  assertIsAdmin(request);

  const { bookingId, newStatus, cancellationReason } = request.data || {};

  // Check the input is valid.
  if (!bookingId || typeof bookingId !== "string") {
    throw new HttpsError("invalid-argument", "bookingId is required.");
  }
  if (!["confirmed", "completed", "cancelled"].includes(newStatus)) {
    throw new HttpsError("invalid-argument", 'newStatus must be "confirmed", "completed" or "cancelled".');
  }

  // Load the booking.
  const bookingRef = db.collection("bookings").doc(bookingId);
  const bookingSnap = await bookingRef.get();
  if (!bookingSnap.exists) {
    throw new HttpsError("not-found", "Booking not found.");
  }

  // Check the transition is allowed.
  const currentStatus = bookingSnap.data().status;
  const allowedNext = ALLOWED_TRANSITIONS[currentStatus] || [];

  if (!allowedNext.includes(newStatus)) {
    throw new HttpsError(
      "failed-precondition",
      `Can't move a "${currentStatus}" booking to "${newStatus}".`
    );
  }

  // Build the update.
  const update = {
    status: newStatus,
    updatedAt: admin.firestore.FieldValue.serverTimestamp(),
    updatedBy: request.auth.uid,
  };

  // If cancelling, save the reason (if provided).
  if (newStatus === "cancelled") {
    update.cancellationReason = cancellationReason || "";
  }

  await bookingRef.update(update);

  return { bookingId, status: newStatus };
});