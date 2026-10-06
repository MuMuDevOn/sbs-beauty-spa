namespace SBSBeautySpa.Mobile.Models
{
    // Info about the business that ANYONE can see.
    // This includes people who haven't signed up yet.
    //
    // Used for: the "About Us" screen, contact info shown pre-login,
    // and the cancellation policy text shown before booking.
    public class BusinessSettingsPublic
    {
        // The business name.
        public string Name { get; set; } = string.Empty;

        // The business phone number.
        public string Phone { get; set; } = string.Empty;

        // The business email address.
        public string Email { get; set; } = string.Empty;

        // The business address (shown to clients after booking).
        public string Address { get; set; } = string.Empty;

        // Optional URL for the business logo.
        public string? LogoUrl { get; set; }

        // A short description of the business.
        public string Description { get; set; } = string.Empty;

        // The cancellation policy text.
        // Shown to clients before they book.
        public string CancellationPolicy { get; set; } = string.Empty;

        // The terms and conditions text.
        public string TermsAndConditions { get; set; } = string.Empty;
    }

    // Info about the business that ONLY admins can see.
    // Contains bank details — never sent to a regular client.
    public class BusinessSettingsPrivate
    {
        // The bank name (e.g., "FNB", "Standard Bank").
        public string BankName { get; set; } = string.Empty;

        // The account holder's name.
        public string AccountName { get; set; } = string.Empty;

        // The bank account number.
        public string AccountNumber { get; set; } = string.Empty;

        // Extra notes about payment (e.g., "Use your booking ID as reference").
        public string PaymentNotes { get; set; } = string.Empty;
    }
}