using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NodaTime;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // This class represents one time slot in the list.
    // Example: "09:00 - 10:30"
    public partial class TimeSlotCell : ObservableObject
    {
        public AvailabilitySlot Slot { get; }
        public string Label => Slot.DisplayLabel;

        // Tells us if this slot is currently selected by the user.
        [ObservableProperty]
        private bool isSelected;

        public TimeSlotCell(AvailabilitySlot slot) => Slot = slot;
    }

    // This class controls the "Select Date & Time" screen.
    //
    // The calendar itself (the month view and day grid) is handled by
    // Syncfusion's SfCalendar control in the View — not by this class.
    //
    // What this class DOES do:
    //   - Whenever a date is picked, it asks the server for the real
    //     bookable time slots for that date.
    //   - The calendar control only knows about dates. It does NOT know
    //     which dates actually have free slots. That's why we always ask
    //     the server through AvailabilityService.
    //
    // One tricky part: SfCalendar uses System.DateTime (a normal C# date),
    // but the rest of this app uses NodaTime's LocalDate. So:
    //   - SelectedDateTime is what the calendar binds to.
    //   - SelectedDate is the NodaTime version, derived from it, and is
    //     what the rest of the app (BookingViewModel, AddOnsViewModel) uses.
    public partial class CalendarViewModel : BaseViewModel
    {
        private readonly IAvailabilityService _availabilityService;
        private readonly IClock _clock; // NodaTime clock. Use SystemClock.Instance in real life, fake it in tests.

        public CalendarViewModel(IAvailabilityService availabilityService, IClock clock)
        {
            _availabilityService = availabilityService;
            _clock = clock;

            // Get today's date in South African time (Johannesburg).
            var today = _clock.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb["Africa/Johannesburg"]).Date;
            selectedDateTime = new DateTime(today.Year, today.Month, today.Day);

            Title = "Select Date & Time";
        }

        // Set by the previous screen (Service Detail) before opening this one.
        public string ServiceId { get; set; } = string.Empty;

        // The date the user picked on the calendar.
        [ObservableProperty]
        private DateTime? selectedDateTime;

        // The time slot the user selected.
        [ObservableProperty]
        private AvailabilitySlot? selectedSlot;

        // The list of available time slots shown below the calendar.
        public ObservableCollection<TimeSlotCell> TimeSlots { get; } = new();

        // The NodaTime version of the selected date.
        // This is what the rest of the app reads — never bind to it directly.
        public LocalDate SelectedDate
        {
            get
            {
                var d = SelectedDateTime ?? DateTime.Today;
                return new LocalDate(d.Year, d.Month, d.Day);
            }
        }

        // Shows a line like: "Selected: Wednesday, Oct 15, 2026"
        public string SelectedDateLabel =>
            $"Selected: {SelectedDate.ToString("dddd, MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture)}";

        // True when the user has picked a time slot.
        public bool CanContinue => SelectedSlot is not null;

        // Runs automatically when the user picks a different date.
        async partial void OnSelectedDateTimeChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(SelectedDate));
            OnPropertyChanged(nameof(SelectedDateLabel));
            SelectedSlot = null;
            await LoadTimeSlotsAsync();
        }

        // Runs automatically when the user picks a different time slot.
        partial void OnSelectedSlotChanged(AvailabilitySlot? value) => OnPropertyChanged(nameof(CanContinue));

        // Loads the available time slots from the server for the selected date.
        [RelayCommand]
        private async Task LoadTimeSlotsAsync()
        {
            if (string.IsNullOrEmpty(ServiceId) || IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;
            TimeSlots.Clear();

            try
            {
                // At this point, only the main service is known.
                // The Add-Ons screen comes next, so we don't know yet if the
                // user will add extras that make the appointment longer.
                //
                // That's okay — because when the booking is actually created,
                // the server re-checks the time slot against the FULL list of
                // services (main + add-ons). If the add-ons make it too long
                // to fit, the server rejects it and sends the user back to
                // pick a new time. This prevents double-booking.
                var slots = await _availabilityService.GetAvailableSlotsAsync(new[] { ServiceId }, SelectedDate);
                foreach (var slot in slots)
                {
                    TimeSlots.Add(new TimeSlotCell(slot));
                }

                if (TimeSlots.Count == 0)
                {
                    ErrorMessage = "No available times on this date — try another day.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load available times. Check your connection and try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Runs when the user taps a time slot.
        // Marks that one as selected and clears the others.
        [RelayCommand]
        private void SelectSlot(TimeSlotCell cell)
        {
            foreach (var slot in TimeSlots)
            {
                slot.IsSelected = false;
            }
            cell.IsSelected = true;
            SelectedSlot = cell.Slot;
        }
    }
}