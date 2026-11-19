using SBSBeautySpa.Mobile.Views.Admin;

namespace SBSBeautySpa.Mobile.Views;

public partial class AdminShell : Shell
{
    public AdminShell()
    {
        InitializeComponent();
        RegisterRoutes();
    }

    private static void RegisterRoutes()
    {
        // Admin management pages
        Routing.RegisterRoute(nameof(AdminBookingsPage), typeof(AdminBookingsPage));
        Routing.RegisterRoute(nameof(AdminServicesPage), typeof(AdminServicesPage));
        Routing.RegisterRoute(nameof(AdminProductPage), typeof(AdminProductPage));
        Routing.RegisterRoute(nameof(AdminClientsPage), typeof(AdminClientsPage));
        Routing.RegisterRoute(nameof(AdminStaffPage), typeof(AdminStaffPage));
        Routing.RegisterRoute(nameof(AdminHoursPage), typeof(AdminHoursPage));
        Routing.RegisterRoute(nameof(AdminUploadGalleryPage), typeof(AdminUploadGalleryPage));
        Routing.RegisterRoute(nameof(AdminClientDetailPage), typeof(AdminClientDetailPage));   // tap a client: details + notes
        Routing.RegisterRoute(nameof(AdminMessagesPage), typeof(AdminMessagesPage));
        Routing.RegisterRoute(nameof(AdminAnalyticsPage), typeof(AdminAnalyticsPage));

        // Other pages
        Routing.RegisterRoute(nameof(ChangePasswordPage), typeof(ChangePasswordPage));
        Routing.RegisterRoute(nameof(AboutPage), typeof(AboutPage));
        Routing.RegisterRoute(nameof(ContactUsPage), typeof(ContactUsPage));
    }
}