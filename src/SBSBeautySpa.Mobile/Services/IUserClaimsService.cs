namespace SBSBeautySpa.Mobile.Services
{
    public interface IUserClaimsService
    {
        bool IsAdmin { get; }
        bool IsSuperAdmin { get; }
        Task RefreshAsync();
    }
}