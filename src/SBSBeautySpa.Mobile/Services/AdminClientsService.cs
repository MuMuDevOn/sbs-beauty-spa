using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    public class AdminClientsService : IAdminClientsService
    {
        private readonly CallableFunctionClient _client;

        public AdminClientsService(CallableFunctionClient client) => _client = client;

        public async Task<List<ClientSummary>> ListClientsAsync(string? search = null, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<ClientsResult>("listClients", new { search }, ct);
            return result.Clients;
        }

        public async Task<ClientDetail> GetClientDetailsAsync(string clientId, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<ClientDetailDto>("getClientDetails", new { clientId }, ct);

            return new ClientDetail(
                new ClientSummary(
                    clientId,
                    result.Profile?.Name ?? "",
                    result.Profile?.Email ?? "",
                    result.Profile?.PhoneNumber ?? "",
                    true,
                    result.Profile?.CreatedAt ?? ""
                ),
                result.Bookings ?? new List<Booking>(),
                result.Notes ?? new List<ClientNoteEntry>()
            );
        }

        public Task SetClientDisabledAsync(string clientId, bool disabled, CancellationToken ct = default) =>
            _client.CallAsync("setClientDisabled", new { clientId, disabled }, ct);

        private class ClientsResult
        {
            public List<ClientSummary> Clients { get; set; } = new();
        }

        private class ClientDetailDto
        {
            public ClientProfileDto? Profile { get; set; }
            public List<Booking>? Bookings { get; set; }
            public List<ClientNoteEntry>? Notes { get; set; }
        }

        private class ClientProfileDto
        {
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? PhoneNumber { get; set; }
            public string? CreatedAt { get; set; }
        }
    }
}