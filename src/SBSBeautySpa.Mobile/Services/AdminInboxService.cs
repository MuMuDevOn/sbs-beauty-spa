namespace SBSBeautySpa.Mobile.Services
{
    // One "Contact Us" message as the admin sees it.
    public class InboxMessage
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Message { get; set; } = "";
        public string Status { get; set; } = "new";
        public DateTime? CreatedAt { get; set; }
        public string Reply { get; set; } = "";
        public string RepliedByName { get; set; } = "";
        public DateTime? RepliedAt { get; set; }

        public bool IsAnswered => !string.IsNullOrWhiteSpace(Reply);
    }

    // Admin-only: reads Contact Us messages and sends replies (through the server).
    public class AdminInboxService
    {
        private readonly CallableFunctionClient _client;

        public AdminInboxService(CallableFunctionClient client) => _client = client;

        public async Task<List<InboxMessage>> ListAsync(CancellationToken ct = default)
        {
            var result = await _client.CallAsync<ListResult>("listContactMessages", new { }, ct);
            return result.Messages;
        }

        // Sends the reply. The server records which admin wrote it and tells the client.
        public async Task<string> ReplyAsync(string messageId, string reply, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<ReplyResult>("replyToContactMessage", new { messageId, reply }, ct);
            return result.RepliedByName;
        }

        private class ListResult { public List<InboxMessage> Messages { get; set; } = new(); }
        private class ReplyResult { public string RepliedByName { get; set; } = ""; }
    }
}
