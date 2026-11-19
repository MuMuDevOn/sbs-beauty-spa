using CommunityToolkit.Mvvm.ComponentModel;
using NodaTime;

namespace SBSBeautySpa.Mobile.Models
{
    public enum BookingStatus
    {
        Pending,
        Confirmed,
        Completed,
        Cancelled
    }

    public partial class Booking : ObservableObject
    {
        public string Id { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ServiceId { get; set; } = string.Empty;
        public string? ServiceName { get; set; }
        public List<BookingLineItem> Services { get; set; } = new();
        public LocalDate Date { get; set; }
        public LocalTime StartTime { get; set; }
        public LocalTime EndTime { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsPending), nameof(IsConfirmed))]
        private BookingStatus status = BookingStatus.Pending;

        // Used by the admin bookings list to show/hide the Confirm and Complete buttons.
        public bool IsPending => Status == BookingStatus.Pending;
        public bool IsConfirmed => Status == BookingStatus.Confirmed;

        public decimal DepositAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal OutstandingBalance => TotalAmount - DepositAmount;

        // True once the full amount has been paid (set by the server after a "pay in full" payment).
        public bool BalanceSettled { get; set; }

        public string? Notes { get; set; }

        [ObservableProperty]
        private string? cancellationReason;

        public string? ClientDisplayName { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}