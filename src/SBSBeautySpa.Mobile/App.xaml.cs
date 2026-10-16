#if ANDROID || IOS
using Plugin.FirebasePushNotifications;
#endif
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new AppShell());

#if ANDROID || IOS
        window.Created += OnWindowCreated;
#endif

        return window;
    }

#if ANDROID || IOS
    private void OnWindowCreated(object? sender, EventArgs e)
    {
        var services = IPlatformApplication.Current?.Services;
        if (services is null) return;

        var fcm = services.GetService<IFirebasePushNotification>();
        var firestore = services.GetService<IFirestoreService>();
        if (fcm is null || firestore is null) return;

        // Whenever FCM issues a new token (first launch, reinstall, or refresh),
        // save it to users/{uid}.fcmToken so the backend can deliver pushes.
        fcm.TokenRefreshed += async (_, args) =>
        {
            if (!string.IsNullOrEmpty(args.Token))
                await firestore.SaveFcmTokenAsync(args.Token);
        };

        // Also save the current token if FCM already has one from a previous run.
        if (!string.IsNullOrEmpty(fcm.Token))
            _ = firestore.SaveFcmTokenAsync(fcm.Token);
    }
#endif
}