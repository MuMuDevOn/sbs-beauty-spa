using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{

    // Admin service for the Notification Log.
    // Lists notifications and lets admins resend failed ones.

    public interface INotificationsAdminService
    {
        Task<List<Notification>> ListNotificationLogAsync(
            NotificationStatus? status = null,
            NotificationChannel? channel = null,
            CancellationToken ct = default);

        Task<NotificationStatus> ResendNotificationAsync(
            string notificationId,
            CancellationToken ct = default);
    }

    public class NotificationsAdminService : INotificationsAdminService
    {
        private readonly CallableFunctionClient _client;

        public NotificationsAdminService(CallableFunctionClient client) => _client = client;

        // Lists notifications, optionally filtered
        public async Task<List<Notification>> ListNotificationLogAsync(
            NotificationStatus? status = null,
            NotificationChannel? channel = null,
            CancellationToken ct = default)
        {
            var result = await _client.CallAsync<LogResult>("listNotificationLog", new
            {
                status = status?.ToString().ToLowerInvariant(),
                // Must be "inApp" (camelCase) — server matches exactly
                channel = channel == NotificationChannel.InApp ? "inApp" : channel?.ToString().ToLowerInvariant(),
            }, ct);

            return result.Notifications;
        }

        // Resends a failed Email/Push notification.
        // Server rejects InApp and already-sent ones.
        public async Task<NotificationStatus> ResendNotificationAsync(
            string notificationId,
            CancellationToken ct = default)
        {
            var result = await _client.CallAsync<ResendResult>("resendNotification", new { notificationId }, ct);
            return result.Status;
        }

        // Response shapes

        private class LogResult { public List<Notification> Notifications { get; set; } = new(); }
        private class ResendResult { public NotificationStatus Status { get; set; } }
    }
}