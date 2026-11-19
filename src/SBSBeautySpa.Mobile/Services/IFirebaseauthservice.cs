using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{

    // Result of a sign-in / sign-up attempt
    // NextStep is set after a correct password / sign-up: "code" = the login code
    // email is next, "verify-email" = the sign-up verification link is next.
    public record AuthResult(bool Success, string? ErrorMessage = null, string? NextStep = null);

    // Contract for FirebaseAuthService.
    // ViewModels and Services depend on this interface, not the real class.
    public interface IFirebaseAuthService
    {
        // The signed-in user's ID (null if nobody is signed in)
        string? CurrentUserId { get; }

        // Is anyone signed in?
        bool IsSignedIn { get; }

        // Gets the current user's ID token (used on every Cloud Function call)
        Task<string?> GetIdTokenAsync();

        // Signs in an existing user
        Task<AuthResult> SignInAsync(string email, string password);

        // Creates a new user account
        Task<AuthResult> SignUpAsync(string name, string email, string phone, string password);

        // Verifies the current password, then sets a new one
        Task<AuthResult> ChangePasswordAsync(string currentPassword, string newPassword);

        // Updates the display name in Firebase Auth
        Task<AuthResult> UpdateDisplayNameAsync(string name);

        // Emails a password-reset link to the address (Firebase sends the email)
        Task<AuthResult> SendPasswordResetAsync(string email);

        // ---- Two-step sign-in (email code) and sign-up email verification ----

        // Email of the person who has passed the password step but not the email step yet
        string? PendingEmail { get; }

        // True between "password OK" and "email step done"
        bool HasPendingSession { get; }

        // Forgets a half-finished sign-in (e.g. the person pressed Back)
        void CancelPending();

        // Emails a 6-digit login code to the pending user
        Task<AuthResult> SendLoginCodeAsync();

        // Checks the 6-digit code; on success the sign-in is completed
        Task<AuthResult> VerifyLoginCodeAsync(string code);

        // Emails the sign-up verification link to the pending user
        Task<AuthResult> SendVerificationEmailAsync();

        // Checks whether the verification link was tapped; on success the sign-up is completed
        Task<AuthResult> CompleteEmailVerificationAsync();

        // Signs the current user out
        Task SignOutAsync();
    }
}