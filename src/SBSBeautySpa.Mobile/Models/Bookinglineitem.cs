namespace SBSBeautySpa.Mobile.Models
{
  
    public class BookingLineItem
    {
        public string ServiceId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal DepositAmount { get; set; }
        public int DurationMinutes { get; set; }
        public bool IsAddOn { get; set; }
    }
}