namespace SBSBeautySpa.Mobile.Models
{
    ///<summary>
    /// Represents the "Services" CRC card: name, description, price, deposit,
    /// duration, category link, active flag.
    /// Duration/Buffer here are the same values 
    /// the getAvailableSlots Cloud Function reads server-side - which keeps this model;s field names in sync with the Firestore document
    /// Note the shape (which is the mapping is the database role thus it will not be found in this file (This is a notice to mihle the fullstakes and to albertina the database))
    /// </summary>
    public class Service
    {
        public string Id { get; set; } = string.Empty;

         public string CategoryId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public decimal DepositAmount { get; set; }
        public int DurationMinutes { get; set; } 

/// <summary>
/// Optional buffer (minutes) kept free before/after this service - cleanup, setup,etc.
/// </summary>
    public int BufferMinutes {get; set; }

        public bool IsActive { get;set; } = true;
        public string? ImageUrl { get; set; } 
/// <summary>
/// Ids of other services that can be added onto this current service (e.g "Nail Art" as an add on to "Gel Estentions") . Powers the Add-Ons screen.
/// </summary>
        public List<string> AddOnServiceIds { get; set;} = new();
    }
}   