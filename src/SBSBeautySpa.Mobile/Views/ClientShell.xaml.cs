namespace SBSBeautySpa.Mobile.Views;

public partial class ClientShell : Shell
{
    public ClientShell()
    {
        InitializeComponent();
        RegisterRoutes();
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
    }
}