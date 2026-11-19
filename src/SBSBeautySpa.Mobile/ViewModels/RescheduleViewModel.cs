using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // Logic for the "Reschedule" page: pick a new date, pick a free time, confirm.
    // The server decides what is free and enforces the 24-hour rule; this just asks it.
    public partial class RescheduleViewModel : BaseViewModel, IQueryAttributable
    {
        private readonly CallableFunctionClient _functions;
        private int _loadVersion;

        public RescheduleViewModel(CallableFunctionClient functions)
        {
            _functions = functions;
            Title = "Reschedule";
            selectedDate = DateTime.Today;
        }

        public string BookingId { get; private set; } = string.Empty;
        public string ServiceId { get; private set; } = string.Empty;
        public DateTime MinDate => DateTime.Today;

        public ObservableCollection<RescheduleSlot> Slots { get; } = new();

        [ObservableProperty] private string serviceName = string.Empty;
        [ObservableProperty] private string currentText = string.Empty;
        [ObservableProperty] private DateTime selectedDate;
        [ObservableProperty] private bool hasNoSlots;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanConfirm))]
        private RescheduleSlot? selectedSlot;

        public bool CanConfirm => SelectedSlot is not null && !IsBusy;

        // Receives the booking being moved from Booking history.
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            string Text(string key) => query.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
            BookingId = Text("BookingId");
            ServiceId = Text("ServiceId");
            ServiceName = Text("ServiceName");
            CurrentText = Text("CurrentText");
        }

        // Picking a different date reloads the free times for that day.
        partial void OnSelectedDateChanged(DateTime value) => _ = LoadSlotsAsync();

        [RelayCommand]
        public async Task LoadSlotsAsync()
        {
            if (string.IsNullOrEmpty(BookingId)) return;

            // A counter (not IsBusy) so a quick second date change is never dropped.
            var version = ++_loadVersion;
            IsBusy = true;
            ErrorMessage = null;
            SelectedSlot = null;
            Slots.Clear();
            HasNoSlots = false;

            try
            {
                var result = await _functions.CallAsync<SlotsResult>("getAvailableSlots", new
                {
                    serviceIds = new[] { ServiceId },
                    date = SelectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    excludeBookingId = BookingId,
                });

                if (version != _loadVersion) return; // a newer request replaced this one

                foreach (var s in result.Slots)
                    Slots.Add(new RescheduleSlot(s.StartTime, FormatTime(s.StartTime)));
                HasNoSlots = Slots.Count == 0;
            }
            catch (Exception ex)
            {
                if (version != _loadVersion) return;
                ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                if (version == _loadVersion) IsBusy = false;
                OnPropertyChanged(nameof(CanConfirm));
            }
        }

        [RelayCommand]
        private void SelectSlot(RescheduleSlot? slot)
        {
            if (slot is null) return;
            foreach (var s in Slots) s.IsSelected = s == slot;
            SelectedSlot = slot;
        }

        [RelayCommand]
        private async Task ConfirmAsync()
        {
            if (SelectedSlot is null || IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            try
            {
                await _functions.CallAsync("rescheduleBooking", new
                {
                    bookingId = BookingId,
                    date = SelectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    startTime = SelectedSlot.StartTime,
                });
            }
            catch (Exception ex)
            {
                // Includes the server's own message, e.g. the 24-hour rule.
                ErrorMessage = CleanError(ex.Message);
                await Shell.Current.DisplayAlertAsync("Couldn't reschedule", ErrorMessage, "OK");
                return;
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(CanConfirm));
            }

            await Shell.Current.DisplayAlertAsync("Rescheduled",
                $"Your appointment is now {SelectedDate:MMM d, yyyy} at {SelectedSlot!.Label}.", "OK");
            await Shell.Current.GoToAsync("..");
        }

        // "rescheduleBooking failed: <message>" -> "<message>"
        private static string CleanError(string message)
        {
            var i = message.IndexOf("failed: ", StringComparison.Ordinal);
            return i >= 0 ? message[(i + 8)..] : message;
        }

        private static string FormatTime(string hhmm) =>
            TimeOnly.TryParse(hhmm, CultureInfo.InvariantCulture, out var t)
                ? DateTime.Today.Add(t.ToTimeSpan()).ToString("h:mm tt", CultureInfo.InvariantCulture)
                : hhmm;

        // What getAvailableSlots sends back.
        private class SlotsResult
        {
            public List<SlotDto> Slots { get; set; } = new();
        }

        private class SlotDto
        {
            public string StartTime { get; set; } = string.Empty;
            public string EndTime { get; set; } = string.Empty;
        }
    }

    // One time button on the Reschedule page.
    public partial class RescheduleSlot : ObservableObject
    {
        public RescheduleSlot(string startTime, string label)
        {
            StartTime = startTime;
            Label = label;
        }

        public string StartTime { get; }
        public string Label { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Background), nameof(TextColor))]
        private bool isSelected;

        public Color Background => IsSelected ? Color.FromArgb("#6D3FE0") : Colors.White;
        public Color TextColor => IsSelected ? Colors.White : Color.FromArgb("#2B1B6B");
    }
}
