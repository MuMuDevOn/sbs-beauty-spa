using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Core.Hosting;
using SBSBeautySpa.Mobile.Services;
using SBSBeautySpa.Mobile.ViewModels;
using SBSBeautySpa.Mobile.ViewModels.Admin;
using SBSBeautySpa.Mobile.Views;
using SBSBeautySpa.Mobile.Views.Admin;

namespace SBSBeautySpa.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
           
            // Register Syncfusion license from Secrets.cs
            // (Secrets.cs is gitignored — it holds the license key)
            
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(Secrets.SyncfusionLicenseKey);

            var builder = MauiApp.CreateBuilder();

            
            // App configuration
            
            builder
                .UseMauiApp<App>()
                .ConfigureSyncfusionCore()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

          
            // HTTP client (shared by all services)
            
            builder.Services.AddSingleton<HttpClient>();

           
            // Services
           
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

            // Shared callable function client (used by newer services)
            builder.Services.AddSingleton<CallableFunctionClient>();

           
            // Client ViewModels
        
            builder.Services.AddTransient<ServicesViewModel>();
            builder.Services.AddTransient<CalendarViewModel>();
            builder.Services.AddTransient<AddOnsViewModel>();
            builder.Services.AddTransient<BookingViewModel>();
            builder.Services.AddTransient<PaymentViewModel>();

           
            // Admin ViewModels
           
            builder.Services.AddTransient<AdminDashboardViewModel>();
            builder.Services.AddTransient<AdminBookingsViewModel>();
            builder.Services.AddTransient<AdminServicesViewModel>();

            
            // Client Views (Pages)
            
            builder.Services.AddTransient<ServicesPage>();
            builder.Services.AddTransient<SelectDateTimeView>();
            builder.Services.AddTransient<AddOnsPage>();
            builder.Services.AddTransient<BookingPage>();
            builder.Services.AddTransient<PaymentPage>();

           
            // Admin Views (Pages)
    
            builder.Services.AddTransient<AdminDashboardPage>();
            builder.Services.AddTransient<AdminBookingsPage>();
            builder.Services.AddTransient<AdminServicesPage>();

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}