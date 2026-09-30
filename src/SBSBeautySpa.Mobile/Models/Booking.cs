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



///<summary> 
/// Represents the "Bookings" CRC card. Date/StartTime/EndTime are NodaTime 
/// types (LocalDate / LocalTime) rather than System.DateTime - this
/// avoids the classic "was that UTC or local?" bug and matches the
/// values returned by the createBooking Cloud Function, which already
/// resolved everything against BUSINESS_TIMEZONE server-sides.
/// </summary>

public class Booking
{
   public string Id { get; set; } = string.Empty;
   public string ClientId { get; set; } = string.Empty;

   ///<summary> The primary service's id (service[0]) - kept flat for simple queries/back-compat. </summary>
   public string ServiceId { get; set; } = string.Empty;

   ///<summary> Primary service plus any add-ons, each a priced snapshot at booking time. </summary>
   public List<BookingLineItem> Services { get; set; } = new();

   public LocalDate Date { get; set; }
   public LocalTime StartTime {get; set; }
    public LocalTime EndTime { get; set; }

public BookingStatus Status { get; set; } = BookingStatus.Pending;



public decimal DepositAmount { get; set; } 
public decimal TotalAmount { get; set; } 
public decimal OutstandingBalance { get; set; } 

public decimal OutstandingBalance => TotalAmount - DepositAmount;

public string? Notes {get; set;} 
public string? CancellationReason {get; set;}

///<summary> Convencience field for admin screens - populated by IAdminDataService joining the User doc, NOT a raw field on the booking document itself. </summary>
public string? ClientDisplayName {get; set;}






}

}

