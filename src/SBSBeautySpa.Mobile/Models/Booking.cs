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
        public List<BookingLineItem> Services { get; set; } = new();
        public LocalDate Date { get; set; }
        public LocalTime StartTime { get; set; }
        public LocalTime EndTime { get; set; }

        [ObservableProperty]
        private BookingStatus status = BookingStatus.Pending;

        public decimal DepositAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal OutstandingBalance => TotalAmount - DepositAmount;

        public string? Notes { get; set; }

        [ObservableProperty]
        private string? cancellationReason;

        public string? ClientDisplayName { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}