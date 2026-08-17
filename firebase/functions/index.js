const { onCall, onRequest } = require("firebase-functions/v2/https");
const { onDocumentUpdated } = require("firebase-functions/v2/firestore");
const admin = require("firebase-admin");
admin.initializeApp();

/**
 * Stub functions matching the functional requirements in the proposal.
 * Fill in the logic during Phase 3 (Development of project prototype).
 */

// FR-06: initiate a deposit payment via the payment gateway
exports.initiatePayment = onCall(async (request) => {
  // TODO: create a "pending" payment record, call PayGate, return the redirect URL
  throw new Error("Not implemented");
});

// FR-06/FR-09: payment gateway webhook/callback — must be idempotent
exports.paymentWebhook = onRequest(async (req, res) => {
  // TODO: verify the gateway signature, look up the transaction reference,
  // guard against duplicate webhook delivery (idempotency), then update
  // the booking + payment status.
  res.status(501).send("Not implemented");
});

// FR-09: address-disclosure email, triggered on deposit confirmation
exports.sendBookingConfirmationEmail = onDocumentUpdated(
  "bookings/{bookingId}",
  async (event) => {
    // TODO: on status change to "Confirmed", send the salon address +
    // booking details email within the target window (see NFR: Performance).
  }
);

// FR-13: scheduled booking reminders
exports.sendBookingReminders = onRequest(async (req, res) => {
  // TODO: query upcoming bookings and dispatch reminder notifications
  res.status(501).send("Not implemented");
});
