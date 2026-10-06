using CommunityToolkit.Mvvm.ComponentModel;

namespace SBSBeautySpa.Mobile.Models
{
    // Represents one service the salon offers (like "Gel Extensions" or "Hybrid Lashes").
    //
    // This class matches the "Service" CRC card. It holds:
    //   - Name, description, price, deposit, duration
    //   - Which category it belongs to
    //   - Whether it's active or not
    //
    // IMPORTANT: The DurationMinutes and BufferMinutes values here are the
    // same values the server reads when it calculates available time slots.
    // So if you change field names here, you must also update the Firestore
    // document shape. (That's the database team's job to keep in sync.)
    //
    // Why is IsActive different? It uses [ObservableProperty] while the
    // others are plain properties. This is on purpose — when an admin
    // toggles a service on or off, we want the UI to update instantly
    // without reloading the whole list.
    public partial class Service : ObservableObject
    {
        // The unique ID of this service.
        public string Id { get; set; } = string.Empty;

        // Which category this service belongs to (nails, lashes, etc.).
        public string CategoryId { get; set; } = string.Empty;

        // The service name shown to clients.
        public string Name { get; set; } = string.Empty;

        // A short description of the service.
        public string Description { get; set; } = string.Empty;

        // The total price of the service.
        public decimal Price { get; set; }

        // How much deposit the client must pay to book this service.
        public decimal DepositAmount { get; set; }

        // How long the service takes (in minutes).
        public int DurationMinutes { get; set; }

        // Optional buffer time (in minutes) kept free before and after.
        // Used for cleanup, setup, or turning over the station.
        public int BufferMinutes { get; set; }

        // Whether this service is currently offered.
        // This is observable so the UI updates when it changes.
        [ObservableProperty]
        private bool isActive = true;

        // Optional image URL for the service.
        public string? ImageUrl { get; set; }

        // IDs of other services that can be added on top of this one.
        // Example: "Nail Art" as an add-on to "Gel Extensions".
        // This powers the Add-Ons screen.
        public List<string> AddOnServiceIds { get; set; } = new();
    }
}