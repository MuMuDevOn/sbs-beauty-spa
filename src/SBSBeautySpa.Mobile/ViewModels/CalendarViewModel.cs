using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NodaTime;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class TimeSlotCell : ObservableObject
    {
        public AvailabilitySlot Slot { get; }
        public string Label => Slot.DisplayLabel;

        [ObservableProperty]
        private bool isSelected;

        public TimeSlotCell(AvailabilitySlot slot) => Slot = slot;
    }
    [QueryProperty(nameof(ServiceId), "serviceId")]
    public partial class CalendarViewModel : BaseViewModel
    {
        private readonly IAvailabilityService _availabilityService;
        private readonly IClock _clock;

        public CalendarViewModel(IAvailabilityService availabilityService, IClock clock)
        {
            _availabilityService = availabilityService;
            _clock = clock;

            var today = _clock.GetCurrentInstant().InZone(DateTimeZoneProviders.Tzdb["Africa/Johannesburg"]).Date;
            selectedDateTime = new DateTime(today.Year, today.Month, today.Day);

            Title = "Select Date & Time";
        }

        public string ServiceId { get; set; } = string.Empty;

        [ObservableProperty]
        private DateTime? selectedDateTime;

        [ObservableProperty]
        private AvailabilitySlot? selectedSlot;

        public ObservableCollection<TimeSlotCell> TimeSlots { get; } = new();

        public LocalDate SelectedDate
        {
            get
            {
                var d = SelectedDateTime ?? DateTime.Today;
                return new LocalDate(d.Year, d.Month, d.Day);
            }
        }

        public string SelectedDateLabel =>
            $"Selected: {SelectedDate.ToString("dddd, MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture)}";

        public bool CanContinue => SelectedSlot is not null;

        async partial void OnSelectedDateTimeChanged(DateTime? value)
        {
            OnPropertyChanged(nameof(SelectedDate));
            OnPropertyChanged(nameof(SelectedDateLabel));
            SelectedSlot = null;
            await LoadTimeSlotsAsync();
        }

        partial void OnSelectedSlotChanged(AvailabilitySlot? value) => OnPropertyChanged(nameof(CanContinue));

        [RelayCommand]
        private async Task LoadTimeSlotsAsync()
        {
            if (string.IsNullOrEmpty(ServiceId) || IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            TimeSlots.Clear();

            try
            {
                var slots = await _availabilityService.GetAvailableSlotsAsync(new[] { ServiceId }, SelectedDate);
                foreach (var slot in slots)
                    TimeSlots.Add(new TimeSlotCell(slot));

                if (TimeSlots.Count == 0)
                    ErrorMessage = "No available times on this date — try another day.";
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

        [RelayCommand]
        private void SelectSlot(TimeSlotCell cell)
        {
            foreach (var slot in TimeSlots)
                slot.IsSelected = false;
            cell.IsSelected = true;
            SelectedSlot = cell.Slot;
        }
    }
}