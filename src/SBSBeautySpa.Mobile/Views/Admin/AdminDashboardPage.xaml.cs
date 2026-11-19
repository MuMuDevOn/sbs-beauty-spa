using SBSBeautySpa.Mobile.Services;
using SBSBeautySpa.Mobile.ViewModels.Admin;

namespace SBSBeautySpa.Mobile.Views.Admin;

public partial class AdminDashboardPage : ContentPage
{
    private readonly AdminDashboardViewModel _viewModel;

    public AdminDashboardPage(AdminDashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadDashboardCommand.Execute(null);
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnAnalyticsTapped(object sender, TappedEventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(AdminAnalyticsPage));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't open Analytics", ex.GetType().Name + ": " + ex.Message, "OK");
        }
    }

    private async void OnMessagesTapped(object sender, TappedEventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(AdminMessagesPage));
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't open Messages", ex.GetType().Name + ": " + ex.Message, "OK");
        }
    }

    private async void OnManageBookingsTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AdminBookingsPage));

    private async void OnManageServicesTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AdminServicesPage));

    private async void OnManageProductsTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AdminProductPage));

    private async void OnBusinessHoursTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AdminHoursPage));

    // Shows the real reason on screen if the page can't open (instead of doing nothing).
    private async void OnUploadGalleryTapped(object sender, TappedEventArgs e)
    {
        try
        {
            await Shell.Current.GoToAsync(nameof(AdminUploadGalleryPage));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            var inner = ex.InnerException is null ? "" : "\n\nInner: " + ex.InnerException.GetType().Name + ": " + ex.InnerException.Message;
            await DisplayAlertAsync("Couldn't open Upload to Gallery", ex.GetType().Name + ": " + ex.Message + inner, "OK");
        }
    }

    private async void OnManageClientsTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AdminClientsPage));

    private async void OnManageStaffTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AdminStaffPage));

    // Same sign-out steps the client Profile page uses
    private async void OnLogoutTapped(object sender, TappedEventArgs e)
    {
        bool confirm = await DisplayAlertAsync("Log out", "Are you sure you want to log out?", "Log out", "Cancel");
        if (!confirm) return;

        var services = IPlatformApplication.Current?.Services;

        var auth = services?.GetService<IFirebaseAuthService>();
        if (auth is not null) await auth.SignOutAsync();

        var claims = services?.GetService<IUserClaimsService>();
        if (claims is not null) await claims.RefreshAsync();

        var window = Application.Current?.Windows.FirstOrDefault();
        if (window is not null)
            window.Page = new AuthShell();
    }
}