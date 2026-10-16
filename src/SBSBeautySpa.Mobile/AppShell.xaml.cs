namespace SBSBeautySpa.Mobile
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Pages reached via navigation (not tabs)
            Routing.RegisterRoute(nameof(Views.LoginPage), typeof(Views.LoginPage));
            Routing.RegisterRoute(nameof(Views.RegisterPage), typeof(Views.RegisterPage));
            Routing.RegisterRoute(nameof(Views.ServicesPage), typeof(Views.ServicesPage));
            Routing.RegisterRoute(nameof(Views.BookingPage), typeof(Views.BookingPage));
            Routing.RegisterRoute(nameof(Views.BookingHistoryPage), typeof(Views.BookingHistoryPage));
            Routing.RegisterRoute(nameof(Views.SelectDateTimeView), typeof(Views.SelectDateTimeView));
            Routing.RegisterRoute(nameof(Views.CartView), typeof(Views.CartView));

            // Profile sub-pages
            Routing.RegisterRoute(nameof(Views.EditProfilePage), typeof(Views.EditProfilePage));
            Routing.RegisterRoute(nameof(Views.ChangePasswordPage), typeof(Views.ChangePasswordPage));
            Routing.RegisterRoute(nameof(Views.PaymentMethodsPage), typeof(Views.PaymentMethodsPage));
            Routing.RegisterRoute(nameof(Views.NotificationsPage), typeof(Views.NotificationsPage));
            Routing.RegisterRoute(nameof(Views.ContactUsPage), typeof(Views.ContactUsPage));
            Routing.RegisterRoute(nameof(Views.AboutPage), typeof(Views.AboutPage));

            // Admin pages
            Routing.RegisterRoute(nameof(Views.Admin.AdminDashboardPage), typeof(Views.Admin.AdminDashboardPage));
            Routing.RegisterRoute(nameof(Views.Admin.AdminBookingsPage), typeof(Views.Admin.AdminBookingsPage));
            Routing.RegisterRoute(nameof(Views.Admin.AdminServicesPage), typeof(Views.Admin.AdminServicesPage));
            Routing.RegisterRoute(nameof(Views.Admin.AdminProductPage), typeof(Views.Admin.AdminProductPage));
            Routing.RegisterRoute(nameof(Views.Admin.AdminHoursPage), typeof(Views.Admin.AdminHoursPage));
            Routing.RegisterRoute(nameof(Views.Admin.AdminUploadGalleryPage), typeof(Views.Admin.AdminUploadGalleryPage));
            Routing.RegisterRoute(nameof(Views.Admin.AdminClientsPage), typeof(Views.Admin.AdminClientsPage));
            Routing.RegisterRoute(nameof(Views.Admin.AdminStaffPage), typeof(Views.Admin.AdminStaffPage));
            Routing.RegisterRoute(nameof(Views.Admin.AdminProductPage), typeof(Views.Admin.AdminProductPage));
        }
    }
}