using NodaTime;
namespace SBSBeautySpa.Mobile.Models
{
    ///<summary>
    /// A single bookable slot for one service on one date.
    /// </summary>
    public class AvailabilitySlot
    {
    
        public LocalDate Date { get; set; }
        public LocalTime StartTime { get; set; }
        public LocalTime EndTime { get; set; }

        ///<summary True while the slot is selectable in the UI (e.g. not the one currently being submitted), </summary>
        public bool isAvailable { get; set; } = true;

        ///<summary> Formatted for the time-slot grid, e.g. "10:00 AM". </summary>
        public string DisplayLabel => StartTime.ToString ("hh:mm tt", System.Globalization.CultureInfo.InvariantCulture);

        public  Duration Duration => Period.Between(StartTime, EndTime).ToDuration();
        
    }
}