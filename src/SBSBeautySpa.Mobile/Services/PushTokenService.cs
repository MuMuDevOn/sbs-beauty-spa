using Plugin.FirebasePushNotifications;

namespace SBSBeautySpa.Mobile.Services
{
    // Keeps this phone's push token saved on the server for the signed-in client.
    // Does nothing on Windows (no Firebase push there), so the Windows test build is unaffected.
    public class PushTokenService
    {
        private readonly CallableFunctionClient _client;
        private readonly IServiceProvider _services;
        private bool _subscribed;
        private string? _lastSent;

        // The Firebase push plugin only exists on phones, so it is looked up on demand (never on Windows).
        public PushTokenService(CallableFunctionClient client, IServiceProvider services)
        {
            _client = client;
            _services = services;
        }

        private IFirebasePushNotification? Push => _services.GetService(typeof(IFirebasePushNotification)) as IFirebasePushNotification;

        private static bool IsPhone =>
            DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS;

        // Call after a client logs in.
        public async Task StartAsync()
        {
            if (!IsPhone) return;
            try
            {
                var push = Push;
                if (push is null) return;

                if (!_subscribed)
                {
                    _subscribed = true;
                    // Firebase can rotate the token at any time; save the new one when it does.
                    push.TokenRefreshed += async (_, _) => await SendTokenAsync();
                }

                await RequestPermissionAsync();
                await push.RegisterForPushNotificationsAsync();
                await SendTokenAsync();
            }
            catch (Exception ex)
            {
                // Never block login because push couldn't start.
                System.Diagnostics.Debug.WriteLine("Push start failed: " + ex);
            }
        }

        // Call BEFORE signing out (needs the user's login to talk to the server).
        public async Task StopAsync()
        {
            if (!IsPhone) return;
            try
            {
                _lastSent = null;
                await _client.CallAsync<object>("clearPushToken", new { });
                var push = Push;
                if (push is not null) await push.UnregisterForPushNotificationsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Push stop failed: " + ex);
            }
        }

        public async Task<bool> GetEnabledAsync()
        {
            var r = await _client.CallAsync<PushPreference>("getPushPreference", new { });
            return r.Enabled;
        }

        public async Task SetEnabledAsync(bool enabled)
        {
            await _client.CallAsync<PushPreference>("setPushPreference", new { enabled });
            if (enabled) await StartAsync();
        }

        private async Task SendTokenAsync()
        {
            var token = Push?.Token;
            if (string.IsNullOrWhiteSpace(token) || token == _lastSent) return;
            await _client.CallAsync<object>("registerPushToken", new { token });
            _lastSent = token;
        }

        private static async Task RequestPermissionAsync()
        {
            // Android 13+ needs the user to allow notifications.
            if (DeviceInfo.Platform == DevicePlatform.Android && OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                if (status != PermissionStatus.Granted)
                    await Permissions.RequestAsync<Permissions.PostNotifications>();
            }
        }
    }

    public class PushPreference
    {
        public bool Enabled { get; set; }
    }
}