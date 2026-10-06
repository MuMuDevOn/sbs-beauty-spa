using CommunityToolkit.Mvvm.ComponentModel;
using NodaTime;

namespace SBSBeautySpa.Mobile.Models
{
    // The possible states a booking can be in.
    public enum BookingStatus
    {
        Pending,    // Waiting for deposit payment
        Confirmed,  // Deposit paid, appointment is locked in
        Completed,  // Appointment happened
        Cancelled   // Booking was cancelled
    }

    // Represents one appointment a client has booked.
    //
    // Why NodaTime (LocalDate, LocalTime) instead of System.DateTime?
    // Because DateTime is confusing about time zones — is it UTC? Local?
    // NodaTime is clearer: LocalDate is just a date, LocalTime is just a time.
    //
    // This matches what the server sends back. The server already handled
    // the time zone (Africa/Johannesburg) before sending the values.
    //
    // Why ObservableObject instead of a plain class?
    // Because when an admin confirms or cancels a booking, we want the
    // list on screen to update instantly without reloading everything.
    public partial class Booking : ObservableObject
    {
        // The unique ID of this booking.
        public string Id { get; set; } = string.Empty;

        // The ID of the client who made this booking.
        public string ClientId { get; set; } = string.Empty;

        // The ID of the main service being booked.
        // Kept as a simple field for easy queries.
        public string ServiceId { get; set; } = string.Empty;

        // The full list of services in this booking (main + add-ons).
        // Each one is a snapshot of the price at the time of booking.
        public List<BookingLineItem> Services { get; set; } = new();

        // The date of the appointment.
        public LocalDate Date { get; set; }

        // When the appointment starts.
        public LocalTime StartTime { get; set; }

        // When the appointment ends.
        public LocalTime EndTime { get; set; }

        // The current status of this booking.
        // This is observable so the UI updates when it changes.
        [ObservableProperty]
        private BookingStatus status = BookingStatus.Pending;

        // How much deposit the client paid.
        public decimal DepositAmount { get; set; }

        // The total price of the booking.
        public decimal TotalAmount { get; set; }

        // How much is still owed after the deposit.
        // This is calculated automatically.
        public decimal OutstandingBalance => TotalAmount - DepositAmount;

        // Any special requests from the client.
        public string? Notes { get; set; }

        // The reason the booking was cancelled (if it was).
        // This is observable so the UI updates when it changes.
        [ObservableProperty]
        private string? cancellationReason;

        // The client's name, shown on admin screens.
        // This is NOT stored in the booking document — the admin service
        // fills it in by looking up the client's profile separately.
        public string? ClientDisplayName { get; set; }
    }
}