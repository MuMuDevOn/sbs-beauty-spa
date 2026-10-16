using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{

    // Result of a sign-in / sign-up attempt
    public record AuthResult(bool Success, string? ErrorMessage = null);

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

        // Signs the current user out
        Task SignOutAsync();
    }
}