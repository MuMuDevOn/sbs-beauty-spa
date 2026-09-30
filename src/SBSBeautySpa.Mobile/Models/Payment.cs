using NodaTime;
namespace SBSBeautySpa.Mobile.Models
{
    public enum PaymentType

    {
        Deposit,
        Balance,
        Full,
        Refund

    }

    public enum PaymentStatus

    {
        Pending,
        Completed,
        Failed,
        Refunded
    }

    ///<summary> Represents the "Payment: CRC card - one record per transaction against a Booking. </summary>
    
    public class Payment
    {
        public string Id { get; set; } = String.Empty;
        public string BookingId { get; set; } = string.Empty;

        public decimal Amount { get; set; }
        public PaymentType Type { get; set; }
        public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

        ///<summary> The reference generated at intialisedPayment time and sent to Paystack - sued to look this record up on verify/webhook. </summary>
        public string Reference {get; set; } = string.Empty;

        ///<summary> Paystack's own transaction ID, set once the payment is confirmed (not the same as Reference). </summary>
        
        public string? GatewayReference {get; set; }

        public Instant? PaidAt { get; set; }

    }
}