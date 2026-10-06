using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    // The contract for the client profile service.
    public interface IClientProfileService
    {
        Task<ClientPreference> GetMyPreferencesAsync(CancellationToken ct = default);
        Task SetMyPreferencesAsync(ClientPreference preferences, CancellationToken ct = default);
        Task<string> AddClientNoteAsync(string clientId, string note, CancellationToken ct = default);
        Task<List<ClientNote>> ListClientNotesAsync(string clientId, CancellationToken ct = default);
    }

    // This class talks to the server about client preferences and notes.
    public class ClientProfileService : IClientProfileService
    {
        private readonly CallableFunctionClient _client;

        public ClientProfileService(CallableFunctionClient client) => _client = client;

        // Gets the current user's preferences.
        public Task<ClientPreference> GetMyPreferencesAsync(CancellationToken ct = default) =>
            _client.CallAsync<ClientPreference>("getMyPreferences", new { }, ct);

        // Saves the current user's preferences.
        public Task SetMyPreferencesAsync(ClientPreference preferences, CancellationToken ct = default) =>
            _client.CallAsync("setMyPreferences", new
            {
                preferredStaffId = preferences.PreferredStaffId,
                preferredContactMethod = preferences.PreferredContactMethod?.ToString().ToLowerInvariant(),
            }, ct);

        // Adds a note about a client (admin only).
        public async Task<string> AddClientNoteAsync(string clientId, string note, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<NoteIdResult>("addClientNote", new { clientId, note }, ct);
            return result.NoteId;
        }

        // Lists all notes for a client (admin only).
        public async Task<List<ClientNote>> ListClientNotesAsync(string clientId, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<NotesResult>("listClientNotes", new { clientId }, ct);
            return result.Notes;
        }

        // Small helper classes to match the server's response shape.
        private class NoteIdResult { public string NoteId { get; set; } = string.Empty; }
        private class NotesResult { public List<ClientNote> Notes { get; set; } = new(); }
    }
}