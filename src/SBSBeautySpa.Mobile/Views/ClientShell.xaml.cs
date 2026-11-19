using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.Views;

public partial class ClientShell : Shell
{
    public ClientShell()
    {
        InitializeComponent();
        RegisterRoutes();
        StartPush();
    }

    // The client shell only opens for signed-in clients, so this is the moment to save this phone's push token.
    // Does nothing on Windows and never blocks the app if it fails.
    private void StartPush()
    {
        Dispatcher.Dispatch(async () =>
        {
            try
            {
                var push = IPlatformApplication.Current?.Services.GetService<PushTokenService>();
                if (push is not null) await push.StartAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Push start failed: " + ex);
            }
        });
    }

    private static void RegisterRoutes()
    {
        Routing.RegisterRoute(nameof(ServicesPage), typeof(ServicesPage));
        Routing.RegisterRoute(nameof(SelectDateTimeView), typeof(SelectDateTimeView));
        Routing.RegisterRoute(nameof(BookingHistoryPage), typeof(BookingHistoryPage));
        Routing.RegisterRoute(nameof(CartView), typeof(CartView));
        Routing.RegisterRoute(nameof(EditProfilePage), typeof(EditProfilePage));
        Routing.RegisterRoute(nameof(ChangePasswordPage), typeof(ChangePasswordPage));
        Routing.RegisterRoute(nameof(PaymentMethodsPage), typeof(PaymentMethodsPage));
        Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
        Routing.RegisterRoute(nameof(ContactUsPage), typeof(ContactUsPage));
        Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
        Routing.RegisterRoute("AddOnsPage", typeof(AddOnsPage));
        Routing.RegisterRoute("BookingReviewPage", typeof(BookingReviewPage));
        Routing.RegisterRoute("BookingConfirmationPage", typeof(BookingConfirmationPage));
        Routing.RegisterRoute("RescheduleBookingPage", typeof(RescheduleBookingPage));
        Routing.RegisterRoute("DeliveryAddressesPage", typeof(DeliveryAddressesPage));
        Routing.RegisterRoute("AddressFormPage", typeof(AddressFormPage));
        Routing.RegisterRoute("DeliveryOptionsPage", typeof(DeliveryOptionsPage));
        Routing.RegisterRoute("OrderPaymentPage", typeof(OrderPaymentPage));
        Routing.RegisterRoute("PurchaseHistoryPage", typeof(PurchaseHistoryPage));
        Routing.RegisterRoute("InvoicePage", typeof(InvoicePage));
        Routing.RegisterRoute("OrderConfirmationPage", typeof(OrderConfirmationPage));
        Routing.RegisterRoute("TrackOrderPage", typeof(TrackOrderPage));
    }
}