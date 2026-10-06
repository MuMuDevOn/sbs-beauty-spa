namespace SBSBeautySpa.Mobile.Models
{
    // The ways a client can choose to be contacted.
    public enum ContactMethod
    {
        Email,   // Contact by email
        Sms,     // Contact by SMS text message
        Phone    // Contact by phone call
    }

    // Represents one client's preferences.
    // Each client has one of these.
    // The client can set these themselves via clientProfile.js.
    public class ClientPreference
    {
        // If the client has a preferred staff member.
        // (Optional — null means "no preference".)
        public string? PreferredStaffId { get; set; }

        // How the client prefers to be contacted.
        // (Optional — null means "no preference".)
        public ContactMethod? PreferredContactMethod { get; set; }
    }

    // A note written by an admin ABOUT a client.
    // Examples: "Allergic to gel", "Always late", "Prefers pink polish"
    //
    // IMPORTANT: These notes are NEVER shown to the client themselves.
    // They're staff-only.
    public class ClientNote
    {
        // The unique ID of this note.
        public string Id { get; set; } = string.Empty;

        // Which client this note is about.
        public string ClientId { get; set; } = string.Empty;

        // The actual note text.
        public string Note { get; set; } = string.Empty;

        // When the note was written.
        public DateTime? CreatedAt { get; set; }
    }
}