namespace SBSBeautySpa.Mobile.Services
{
    public class InAppNotification
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Message { get; set; } = "";
        public bool IsRead { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    // The signed-in client's own notifications (the bell on the home page).
    public class InAppNotificationsService
    {
        private readonly CallableFunctionClient _client;

        public InAppNotificationsService(CallableFunctionClient client) => _client = client;

        public async Task<(List<InAppNotification> Items, int Unread)> ListAsync(CancellationToken ct = default)
        {
            var r = await _client.CallAsync<ListResult>("listMyNotifications", new { }, ct);
            return (r.Notifications, r.UnreadCount);
        }

        public Task MarkAllReadAsync(CancellationToken ct = default) =>
            _client.CallAsync("markMyNotificationsRead", new { }, ct);

        private class ListResult
        {
            public List<InAppNotification> Notifications { get; set; } = new();
            public int UnreadCount { get; set; }
        }
    }
}
