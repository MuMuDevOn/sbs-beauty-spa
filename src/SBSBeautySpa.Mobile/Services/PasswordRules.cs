namespace SBSBeautySpa.Mobile.Services
{
    // The password rule used everywhere a password is chosen (sign-up, change password):
    // at least 8 characters, with an uppercase letter, a lowercase letter, a number
    // and a special character.
    public static class PasswordRules
    {
        public const int MinLength = 8;

        public const string Hint =
            "Use at least 8 characters, with a capital letter, a small letter, a number and a special character (like ! @ # $ %).";

        // Returns null when the password is fine, otherwise a friendly message.
        public static string? Validate(string? password)
        {
            password ??= string.Empty;

            if (password.Length < MinLength)
                return "Password must be at least 8 characters.";
            if (!password.Any(char.IsUpper))
                return "Password needs at least one capital letter (A-Z).";
            if (!password.Any(char.IsLower))
                return "Password needs at least one small letter (a-z).";
            if (!password.Any(char.IsDigit))
                return "Password needs at least one number (0-9).";
            if (!password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)))
                return "Password needs at least one special character (like ! @ # $ %).";

            return null;
        }
    }
}
