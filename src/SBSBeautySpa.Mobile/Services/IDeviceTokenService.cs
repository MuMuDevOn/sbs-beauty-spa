namespace SBSBeautySpa.Mobile.Services;

public interface IDeviceTokenService
{
    Task<string?> GetTokenAsync();
    void StartTokenRefreshListener();
    event EventHandler<string>? TokenRefreshed;
}