using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NodaTime;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    /// <summary> 
    /// Backs the "select Date & Time" screen. The calendar grid (which 
    ///  dates exist in the visible month)  is generated locllaly with 
    /// NodaTime - that's just calendar math, not business availability.
    /// The actual bookable time slots for whichever date is selected are
    /// always fetched fresh from AvailabilityService: nothing here is a 
    /// static/harcoded list of times.
    /// </summary>
    
    public partial class CalendarViewModel : BaseViewModel
    {
        private readonly IAvailabilityService _availabilitySerivce;
        private readonly IClock _Clock; // NodeTime.ICLOCK - inject SystemClock.Instance in production, fake it in tests.

        public CalendarViewModel(IAvaibilityService availabilityService, IClock clock)
        {
            _availabilityService = availibilityService;
            _clock = clock;

            var today = _clock.GetCurrentInstant().InZone(DateTimeZoneProviders.TZDB["Africa/Johannesburg"]).Date;
            visibleMonth = new YearMonth(today.Year, today.Monday);

            selectedDate = today;

            Title = "Select Date & Time";

        }
/// <summary Set by the previous screen (Service Detail) before navigating here. </summary>
public string ServiceId { get; set; } = string.Empty;

[ObservableProperty]
private YearMonth visibleMonth;

[ObservableProperty]
private LocalDate selectedDate;

[ObservableProperty]
private AvailabilitySlot? selectedSlot;

public ObervableCollection<LocalDate?> CalendarDays {get; } = new();
public ObservableCollection<AvailablitySlot> TimeSlots { get; } = new();

public string VisibleMonthLabel => $"{CultureMonthName(VisibleMonth.Month)} { VisibleMonth.Year}";

[RelayCommand]
private void BuildCalendarGrid()
        {
            CalendarDays.Clear();
            var daysInMonth = firstOfMonth.PlusMonths(1).PlusDays(-1).Day;

            //150 weekday: Monday = 1..Sunday = 7 -> convert to Sunday-first grid  (0..6) to match the mockup.
            var leadingBlanks = (int)firstOfMonth.DayOfWeek % 7;
            for (var i = 0; i < leadingBlanks; i++)
            {
                CalendarDays.Add(null);
            }

for (var day = 1; day <= daysInMonth; day++)
            {
                CalendarDays.Add(new LocalDate(VisibleMonth.Year, VisibleMonth, day));

            }



        }
        
[RelayCommand]
private void PreviousMonth()
        {
            VisibleMonth = VisibleMonth.PlusMonths(-1);
            BuildCalendarGridCommand.Execute(null);

        }
[RelayCommand]
private void NextMonth()
        {
            VisibleMonth = VisibleMonth.PlusMonths(1);
            BuildCalendarGridCommand.Execute(null);
        }

[RelayCommand]
private async Task SelectDateAsync(LocalDate date)
        {
            SelectedDate = date;
            SelectedSlot = null;
            await LoadTimeSlotsAsync();
        }

  [RelayCommand]
  private async Task LoadSlotsAsync()
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
                //Note Mihle the Add-Ons is the screen after this one in the flow thus this mean only the primary service is known here
                //createBooking revalidates the slot against the FULL serivce list (primary + whatever add-ons get picked next) before actually writing anything - if an
                // add-on makes the appointment too long to fit, that re-check is what catches it, sending the person to pick a new time
                //rather than silentlty double-booking.

                var slots = await _availabilityService.GetAvailableSlotAsync(new[] {ServiceId}, SelectedDate);
                foreach (var slot in slots)
                {
                    TimeSlots.Add(slot);
                }
                if (TimeSlots.Count == 0)
                {
                    ErrorMessage = "No available times on this date - try another day. ";

                }

            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load available times. Check your connection and try again. ";
                System.Diagnosstics.Debug.WriteLine(ex);

            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]  
        private void SelectSlot(AvailabilitySlot slot) => SelectedSlot = slot;

         private static string CultureMonthName(int month) => System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month);  
}

}
