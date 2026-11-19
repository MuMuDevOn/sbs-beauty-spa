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
        private readonly IPaymentService _payments;
        private List<(Booking Booking, BookingHistoryItem Item)> _all = new();
        private Dictionary<string, string> _serviceNames = new();

        public BookingHistoryViewModel(IFirestoreService firestore, IPaymentService payments)
        {
            _firestore = firestore;
            _payments = payments;
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

                // A booking only stores the service id, so look the names up.
                try
                {
                    var services = await _firestore.GetServicesAsync();
                    _serviceNames = services.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First().Name);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }

                _all = bookings.Select(b => (b, ToItem(b, _serviceNames))).ToList();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Couldn't load your bookings: {ex.Message}";
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
        private async Task RescheduleAsync(BookingHistoryItem? item)
        {
            if (item is null) return;

            // 24-hour rule. (The server enforces it too; this just saves the client a trip.)
            if (item.StartsAt - DateTime.Now < TimeSpan.FromHours(24))
            {
                await Shell.Current.DisplayAlertAsync("Can't reschedule",
                    "Bookings can only be rescheduled at least 24 hours before the appointment. Please contact the salon.", "OK");
                return;
            }

            await Shell.Current.GoToAsync("RescheduleBookingPage", new ShellNavigationQueryParameters
            {
                ["BookingId"] = item.Id,
                ["ServiceId"] = item.ServiceId,
                ["ServiceName"] = item.ServiceName,
                ["CurrentText"] = item.DateText,
            });
        }

        [RelayCommand]
        private Task ViewAddressAsync(BookingHistoryItem? item) =>
            Shell.Current.DisplayAlertAsync("Salon address",
                "SBS Beauty Spa\n1 Eglin Road\nSunninghill, Sandton\nJohannesburg, Gauteng, 2157\nSouth Africa", "OK");

        [RelayCommand]
        private async Task PayDepositAsync(BookingHistoryItem? item)
        {
            if (item is null || IsBusy) return;

            IsBusy = true;
            try
            {
                // Same Paystack flow as the booking screen, for a booking that is still unpaid.
                var init = await _payments.InitializePaymentAsync(item.Id, PaymentRequestType.Deposit);
                await Browser.Default.OpenAsync(init.AuthorizationUrl, BrowserLaunchMode.SystemPreferred);

                // Wait for the SERVER to confirm the payment with Paystack (nothing the client
                // taps can confirm it). Gives up after 2 minutes; the webhook still confirms later.
                var confirmed = false;
                var deadline = DateTime.UtcNow.AddMinutes(2);
                while (DateTime.UtcNow < deadline && !confirmed)
                {
                    await Task.Delay(3000);
                    try
                    {
                        confirmed = (await _payments.VerifyPaymentAsync(init.Reference)).Succeeded;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex);
                    }
                }

                await Shell.Current.DisplayAlertAsync(
                    confirmed ? "Booking confirmed" : "Payment not received",
                    confirmed
                        ? "Your deposit was received. See you soon!"
                        : "We haven't received your payment yet. If you did pay, it will confirm automatically.",
                    "OK");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                await Shell.Current.DisplayAlertAsync("Couldn't start payment", ex.Message, "OK");
                return;
            }
            finally
            {
                IsBusy = false;
            }

            // Refresh the list (after IsBusy is cleared, so the reload isn't skipped).
            await LoadAsync();
        }

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

        private static BookingHistoryItem ToItem(Booking b, Dictionary<string, string> serviceNames)
        {
            var name = b.Services.Count > 0
                ? string.Join(", ", b.Services.Select(s => s.Name))
                : serviceNames.TryGetValue(b.ServiceId, out var serviceName) ? serviceName : "Appointment";

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
                ServiceId = b.ServiceId,
                StartsAt = date.AddTicks(b.StartTime.TickOfDay),
                ServiceName = name,
                Status = label,
                DateText = $"{date:MMM d, yyyy} • {start:h:mm tt} ({FormatDuration(minutes)})",
                PriceText = PriceSummary(b),
                IsConfirmed = b.Status == BookingStatus.Confirmed,
                IsPending = b.Status == BookingStatus.Pending,
                DepositLabel = $"Pay Deposit (R{b.DepositAmount:0.00})",
                StatusTextColor = Color.FromArgb(text),
                StatusBackColor = Color.FromArgb(back),
            };
        }

        // What the client has paid and what is still owed, so the card never looks "paid in full" by mistake.
        private static string PriceSummary(Booking b)
        {
            var total = $"R{b.TotalAmount:0.00}";
            if (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed)
            {
                if (b.BalanceSettled) return $"{total} • Paid in full";
                return $"{total} • Deposit R{b.DepositAmount:0.00} paid • R{b.OutstandingBalance:0.00} due in-store";
            }
            if (b.Status == BookingStatus.Pending)
                return $"{total} • Deposit R{b.DepositAmount:0.00} due";
            return total;
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
        public string ServiceId { get; set; } = string.Empty;
        public DateTime StartsAt { get; set; }
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