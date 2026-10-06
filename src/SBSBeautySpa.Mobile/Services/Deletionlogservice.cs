using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    // The contract for the deletion log service.
    public interface IDeletionLogService
    {
        // Lists the deletion audit log (admin only).
        // Optionally filtered by entity type.
        Task<List<DeletionLogEntry>> ListDeletionLogAsync(string? entityType = null, CancellationToken ct = default);
    }

    // This class talks to the server about the deletion log.
    public class DeletionLogService : IDeletionLogService
    {
        private readonly CallableFunctionClient _client;

        public DeletionLogService(CallableFunctionClient client) => _client = client;

        // Gets the deletion log from the server.
        // Optionally filtered by entity type (like "service" or "product").
        public async Task<List<DeletionLogEntry>> ListDeletionLogAsync(string? entityType = null, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<EntriesResult>("listDeletionLog", new { entityType }, ct);
            return result.Entries;
        }

        // Small helper class to match the server's response shape.
        private class EntriesResult { public List<DeletionLogEntry> Entries { get; set; } = new(); }
    }
}