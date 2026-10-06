using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    // The contract for the contact messages service.
    public interface IContactMessagesService
    {
        // Submits a new contact message from the public form.
        //
        // IMPORTANT: NO sign-in required. This is the public "get in touch"
        // form — anyone can use it, even before creating an account.
        Task<string> SubmitContactMessageAsync(string name, string email, string? phone, string? subject, string message, CancellationToken ct = default);

        // Lists all contact messages (admin only).
        // Optionally filtered by status.
        Task<List<ContactMessage>> ListContactMessagesAsync(ContactMessageStatus? status = null, CancellationToken ct = default);

        // Marks a contact message as handled (admin only).
        Task MarkContactMessageHandledAsync(string messageId, CancellationToken ct = default);
    }

    // This class talks to the server about contact messages.
    public class ContactMessagesService : IContactMessagesService
    {
        private readonly CallableFunctionClient _client;

        public ContactMessagesService(CallableFunctionClient client) => _client = client;

        // Sends a contact message to the server.
        // No login required.
        public async Task<string> SubmitContactMessageAsync(string name, string email, string? phone, string? subject, string message, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<MessageResult>("submitContactMessage", new { name, email, phone, subject, message }, ct);
            return result.MessageId;
        }

        // Gets all contact messages (admin only).
        // Optionally filtered by status.
        public async Task<List<ContactMessage>> ListContactMessagesAsync(ContactMessageStatus? status = null, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<MessagesResult>("listContactMessages", new { status = status?.ToString().ToLowerInvariant() }, ct);
            return result.Messages;
        }

        // Marks a contact message as handled (admin only).
        public Task MarkContactMessageHandledAsync(string messageId, CancellationToken ct = default) =>
            _client.CallAsync("markContactMessageHandled", new { messageId }, ct);

        // Small helper classes to match the server's response shape.
        private class MessageResult { public string MessageId { get; set; } = string.Empty; }
        private class MessagesResult { public List<ContactMessage> Messages { get; set; } = new(); }
    }
}