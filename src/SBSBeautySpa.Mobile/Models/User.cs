namespace SBSBeautySpa.Mobile.Models
{
    public enum UserRole
    {
        Client,
        Admin
    }
/// <summary>
/// Represents the "Users" CRC card. Authentication which contains the email/password, sign in/out is owned and used by FirebaseAuthService = this is just the profile shape used
/// once a user is signed in.No password/credentials will be stored here. 
/// </summary>
    public class User
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        public string PhoneNumber { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Client;

        public string? AvatarUrl { get; set; }

    }
}