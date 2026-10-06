using NodaTime;

namespace SBSBeautySpa.Mobile.Models
{
    // The kind of payment being made.
    public enum PaymentType
    {
        Deposit,   // The upfront payment to confirm a booking
        Balance,   // The remaining amount owed after the deposit
        Full,      // The full amount paid at once
        Refund     // Money returned to the client
    }

    // Where a payment is in its life cycle.
    public enum PaymentStatus
    {
        Pending,    // Started but not finished
        Completed,  // Payment succeeded
        Failed,     // Payment didn't go through
        Refunded    // Money was returned to the client
    }

    // One payment record. Every time money moves, one of these is created.
    // This class matches the "Payment" CRC card.
    public class Payment
    {
        // The unique ID of this payment.
        public string Id { get; set; } = string.Empty;

        // The booking this payment is for.
        public string BookingId { get; set; } = string.Empty;

        // How much money this payment is for.
        public decimal Amount { get; set; }

        // What kind of payment this is (deposit, balance, etc.).
        public PaymentType Type { get; set; }

        // The current state of this payment (pending, completed, etc.).
        // Starts as Pending by default.
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        // The reference we created when starting the payment.
        // Sent to Paystack so we can look this record up later
        // when the payment is verified or the webhook arrives.
        public string Reference { get; set; } = string.Empty;

        // Paystack's own transaction ID.
        // Set once the payment is confirmed.
        // This is NOT the same as Reference.
        public string? GatewayReference { get; set; }

        // When the payment was successfully completed.
        public Instant? PaidAt { get; set; }
    }
}