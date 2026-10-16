using Microsoft.Extensions.Logging;
using NodaTime;
using Syncfusion.Maui.Core.Hosting;
using MauiIcons.Material;
using SBSBeautySpa.Mobile.Services;
using SBSBeautySpa.Mobile.ViewModels;
using SBSBeautySpa.Mobile.ViewModels.Admin;
using SBSBeautySpa.Mobile.Views;
using SBSBeautySpa.Mobile.Views.Admin;
using Plugin.FirebasePushNotifications;

namespace SBSBeautySpa.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(Secrets.SyncfusionLicenseKey);

            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .UseMaterialMauiIcons()
                .ConfigureSyncfusionCore()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });
            
            // ---- Infrastructure ----
            builder.Services.AddSingleton<HttpClient>();
            builder.Services.AddSingleton<CallableFunctionClient>();
            builder.Services.AddSingleton<IClock>(SystemClock.Instance);

            // ---- Services ----
            builder.Services.AddSingleton<IFirebaseAuthService, FirebaseAuthService>();
            builder.Services.AddSingleton<IFirestoreService, FirestoreService>();
            builder.Services.AddSingleton<IAvailabilityService, AvailabilityService>();
            builder.Services.AddSingleton<IPaymentService, PaymentService>();
            builder.Services.AddSingleton<IAdminDataService, AdminDataService>();
            builder.Services.AddSingleton<IAdminBookingActionsService, AdminBookingActionsService>();
            builder.Services.AddSingleton<IAdminServicesService, AdminServicesService>();
            builder.Services.AddSingleton<IAdminAvailabilityService, AdminAvailabilityService>();
            builder.Services.AddSingleton<IAdminUsersService, AdminUsersService>();
            builder.Services.AddSingleton<IClientProfileService, ClientProfileService>();
            builder.Services.AddSingleton<IBusinessSettingsService, BusinessSettingsService>();
            builder.Services.AddSingleton<IShopAdminService, ShopAdminService>();
            builder.Services.AddSingleton<IShopService, ShopService>();
            builder.Services.AddSingleton<IGalleryAdminService, GalleryAdminService>();
            builder.Services.AddSingleton<IReviewsService, ReviewsService>();
            builder.Services.AddSingleton<IContactMessagesService, ContactMessagesService>();
            builder.Services.AddSingleton<IDeletionLogService, DeletionLogService>();
            builder.UseFirebasePushNotifications();
            builder.Services.AddSingleton<IAdminClientsService, AdminClientsService>();

            // ---- Client ViewModels ----
            builder.Services.AddTransient<LoginViewModel>();
            builder.Services.AddTransient<RegisterViewModel>();
            builder.Services.AddTransient<GetStartedViewModel>();
            builder.Services.AddTransient<HomeViewModel>();
            builder.Services.AddTransient<ServicesViewModel>();
            builder.Services.AddTransient<CalendarViewModel>();
            builder.Services.AddTransient<AddOnsViewModel>();
            builder.Services.AddTransient<BookingViewModel>();
            builder.Services.AddTransient<PaymentViewModel>();
            builder.Services.AddTransient<BookingHistoryViewModel>();
            builder.Services.AddTransient<ShopViewModel>();
            builder.Services.AddTransient<CartViewModel>();
            builder.Services.AddTransient<GalleryViewModel>();
            builder.Services.AddTransient<ProfileViewModel>();
            builder.Services.AddTransient<EditProfileViewModel>();
            builder.Services.AddTransient<ChangePasswordViewModel>();
            builder.Services.AddTransient<PaymentMethodsViewModel>();
            builder.Services.AddTransient<NotificationsViewModel>();
            builder.Services.AddTransient<ContactUsViewModel>();

            // ---- Admin ViewModels ----
            builder.Services.AddTransient<AdminDashboardViewModel>();
            builder.Services.AddTransient<AdminBookingsViewModel>();
            builder.Services.AddTransient<AdminServicesViewModel>();
            builder.Services.AddTransient<AdminClientsViewModel>();
            builder.Services.AddTransient<AdminStaffViewModel>();
            builder.Services.AddTransient<AdminProductsViewModel>();

            // ---- Client pages ----
            builder.Services.AddTransient<GetStartedPage>();
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<RegisterPage>();
            builder.Services.AddTransient<HomePage>();
            builder.Services.AddTransient<ProfilePage>();
            builder.Services.AddTransient<ServicesPage>();
            builder.Services.AddTransient<BookingPage>();
            builder.Services.AddTransient<BookingHistoryPage>();
            builder.Services.AddTransient<SelectDateTimeView>();
            builder.Services.AddTransient<ShopView>();
            builder.Services.AddTransient<CartView>();
            builder.Services.AddTransient<GalleryView>();
            builder.Services.AddTransient<EditProfilePage>();
            builder.Services.AddTransient<ChangePasswordPage>();
            builder.Services.AddTransient<PaymentMethodsPage>();
            builder.Services.AddTransient<NotificationsPage>();
            builder.Services.AddTransient<ContactUsPage>();
            builder.Services.AddTransient<AboutPage>();

            // ---- Admin pages ----
            builder.Services.AddTransient<AdminDashboardPage>();
            builder.Services.AddTransient<AdminBookingsPage>();
            builder.Services.AddTransient<AdminServicesPage>();
            builder.Services.AddTransient<AdminProductPage>();
            builder.Services.AddTransient<AdminHoursPage>();
            builder.Services.AddTransient<AdminUploadGalleryPage>();
            builder.Services.AddTransient<AdminClientsPage>();
            builder.Services.AddTransient<AdminStaffPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}