namespace SBSBeautySpa.Mobile.Models
{
    /// <summary>
    /// Mirrors one entry of the `services` array createBooking writes onto
    /// a booking doc — a snapshot of a service's name/price/duration at
    /// the moment it was booked, so later price changes to the Service
    /// catalog don't retroactively change what a past booking shows.
    /// </summary>
    public class BookingLineItem
    {
        public string ServiceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal DepositAmount { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsAddOn { get; set; }
    }
}