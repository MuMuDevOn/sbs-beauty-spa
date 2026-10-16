using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class BookingHistoryViewModel : BaseViewModel
    {
        private readonly IFirestoreService _firestore;
        private List<(Booking Booking, BookingHistoryItem Item)> _all = new();

        public BookingHistoryViewModel(IFirestoreService firestore)
        {
            _firestore = firestore;
            Title = "Booking History";

            Tabs = new ObservableCollection<BookingHistoryTab>
            {
                new("Upcoming", isSelected: true),
                new("Past"),
                new("Cancelled"),
            };
        }

        public ObservableCollection<BookingHistoryTab> Tabs { get; }
        public ObservableCollection<BookingHistoryItem> Bookings { get; } = new();

        [ObservableProperty]
        private bool hasNoBookings;

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var bookings = await _firestore.GetMyBookingsAsync();
                _all = bookings.Select(b => (b, ToItem(b))).ToList();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load your bookings. Please try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void SelectTab(BookingHistoryTab? tab)
        {
            if (tab is null) return;

            foreach (var t in Tabs) t.IsSelected = t == tab;
            ApplyFilter();
        }

        [RelayCommand]
        private Task RescheduleAsync(BookingHistoryItem? item) =>
            Shell.Current.DisplayAlertAsync("Reschedule", "Rescheduling is coming soon.", "OK");

        [RelayCommand]
        private Task ViewAddressAsync(BookingHistoryItem? item) =>
            Shell.Current.DisplayAlertAsync("Address", "Salon address details are coming soon.", "OK");

        [RelayCommand]
        private Task PayDepositAsync(BookingHistoryItem? item) =>
            Shell.Current.DisplayAlertAsync("Pay deposit", "Deposit payment is coming soon.", "OK");

        private void ApplyFilter()
        {
            var tab = Tabs.FirstOrDefault(t => t.IsSelected)?.Name ?? "Upcoming";
            var today = DateTime.Today;

            bool IsPast(Booking b) => b.Date.ToDateTimeUnspecified().Date < today;
            DateTime When(Booking b) => b.Date.ToDateTimeUnspecified().AddTicks(b.StartTime.TickOfDay);

            IEnumerable<(Booking Booking, BookingHistoryItem Item)> rows = tab switch
            {
                "Cancelled" => _all
                    .Where(r => r.Booking.Status == BookingStatus.Cancelled)
                    .OrderByDescending(r => When(r.Booking)),

                "Past" => _all
                    .Where(r => r.Booking.Status != BookingStatus.Cancelled &&
                                (r.Booking.Status == BookingStatus.Completed || IsPast(r.Booking)))
                    .OrderByDescending(r => When(r.Booking)),

                _ => _all
                    .Where(r => (r.Booking.Status == BookingStatus.Pending ||
                                 r.Booking.Status == BookingStatus.Confirmed) && !IsPast(r.Booking))
                    .OrderBy(r => When(r.Booking)),
            };

            Bookings.Clear();
            foreach (var r in rows) Bookings.Add(r.Item);
            HasNoBookings = Bookings.Count == 0;
        }

        private static BookingHistoryItem ToItem(Booking b)
        {
            var name = b.Services.Count > 0
                ? string.Join(", ", b.Services.Select(s => s.Name))
                : "Appointment";

            var date = b.Date.ToDateTimeUnspecified();
            var start = DateTime.Today.AddTicks(b.StartTime.TickOfDay);
            var minutes = (int)TimeSpan.FromTicks(b.EndTime.TickOfDay - b.StartTime.TickOfDay).TotalMinutes;

            var (label, text, back) = b.Status switch
            {
                BookingStatus.Confirmed => ("Confirmed", "#166534", "#DCFCE7"),
                BookingStatus.Pending => ("Pending Deposit", "#92400E", "#FEF3C7"),
                BookingStatus.Completed => ("Completed", "#374151", "#E5E7EB"),
                _ => ("Cancelled", "#991B1B", "#FEE2E2"),
            };

            return new BookingHistoryItem
            {
                Id = b.Id,
                ServiceName = name,
                Status = label,
                DateText = $"{date:MMM d, yyyy} • {start:h:mm tt} ({FormatDuration(minutes)})",
                PriceText = $"R{b.TotalAmount:0.00}",
                IsConfirmed = b.Status == BookingStatus.Confirmed,
                IsPending = b.Status == BookingStatus.Pending,
                DepositLabel = $"Pay Deposit (R{b.DepositAmount:0.00})",
                StatusTextColor = Color.FromArgb(text),
                StatusBackColor = Color.FromArgb(back),
            };
        }

        private static string FormatDuration(int minutes)
        {
            if (minutes <= 0) return "—";
            var h = minutes / 60;
            var m = minutes % 60;
            if (h == 0) return $"{m}m";
            return m == 0 ? $"{h}h" : $"{h}h {m}m";
        }
    }

    public partial class BookingHistoryTab : ObservableObject
    {
        public BookingHistoryTab(string name, bool isSelected = false)
        {
            Name = name;
            this.isSelected = isSelected;
        }

        public string Name { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ChipBackground), nameof(ChipTextColor))]
        private bool isSelected;

        public Color ChipBackground => IsSelected ? Color.FromArgb("#111827") : Colors.White;
        public Color ChipTextColor => IsSelected ? Colors.White : Color.FromArgb("#1B2A4A");
    }

    public class BookingHistoryItem
    {
        public string Id { get; set; } = string.Empty;
        public string ServiceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DateText { get; set; } = string.Empty;
        public string PriceText { get; set; } = string.Empty;
        public bool IsConfirmed { get; set; }
        public bool IsPending { get; set; }
        public string DepositLabel { get; set; } = string.Empty;
        public Color StatusTextColor { get; set; } = Colors.Black;
        public Color StatusBackColor { get; set; } = Colors.White;
    }
}