using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    // The contract for the business settings service.
    public interface IBusinessSettingsService
    {
        Task<BusinessSettingsPublic?> GetPublicSettingsAsync(CancellationToken ct = default);
        Task<BusinessSettingsPrivate?> GetPrivateSettingsAsync(CancellationToken ct = default);
        Task UpdatePublicSettingsAsync(BusinessSettingsPublic settings, CancellationToken ct = default);
        Task UpdatePrivateSettingsAsync(BusinessSettingsPrivate settings, CancellationToken ct = default);
    }

    // This class talks to the server about business settings.
    public class BusinessSettingsService : IBusinessSettingsService
    {
        private readonly CallableFunctionClient _client;

        public BusinessSettingsService(CallableFunctionClient client) => _client = client;

        // Gets the public settings (anyone can call).
        public async Task<BusinessSettingsPublic?> GetPublicSettingsAsync(CancellationToken ct = default)
        {
            var result = await _client.CallAsync<SettingsResult<BusinessSettingsPublic>>("getBusinessSettingsPublic", new { }, ct);
            return result.Settings;
        }

        // Gets the private settings (admin only).
        public async Task<BusinessSettingsPrivate?> GetPrivateSettingsAsync(CancellationToken ct = default)
        {
            var result = await _client.CallAsync<SettingsResult<BusinessSettingsPrivate>>("getBusinessSettingsPrivate", new { }, ct);
            return result.Settings;
        }

        // Updates the public settings (admin only).
        public Task UpdatePublicSettingsAsync(BusinessSettingsPublic settings, CancellationToken ct = default) =>
            _client.CallAsync("updateBusinessSettingsPublic", new
            {
                name = settings.Name,
                phone = settings.Phone,
                email = settings.Email,
                address = settings.Address,
                logoUrl = settings.LogoUrl,
                description = settings.Description,
                cancellationPolicy = settings.CancellationPolicy,
                termsAndConditions = settings.TermsAndConditions,
            }, ct);

        // Updates the private settings (admin only).
        public Task UpdatePrivateSettingsAsync(BusinessSettingsPrivate settings, CancellationToken ct = default) =>
            _client.CallAsync("updateBusinessSettingsPrivate", new
            {
                bankName = settings.BankName,
                accountName = settings.AccountName,
                accountNumber = settings.AccountNumber,
                paymentNotes = settings.PaymentNotes,
            }, ct);

        // Generic wrapper — the server wraps settings in a { settings: ... } object.
        private class SettingsResult<T> { public T? Settings { get; set; } }
    }
}