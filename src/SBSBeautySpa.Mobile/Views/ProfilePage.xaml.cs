using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.Views;

public partial class ProfilePage : ContentPage
{
    public ProfilePage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadUserAsync();
    }

    // Fills the header from the signed-in user's token
    private async Task LoadUserAsync()
    {
        try
        {
            var auth = IPlatformApplication.Current?.Services.GetService<IFirebaseAuthService>();
            var token = auth is null ? null : await auth.GetIdTokenAsync();

            var email = TokenClaims.Read(token, "email");
            var name = TokenClaims.Read(token, "name");
            var phone = TokenClaims.Read(token, "phone_number");

            if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email))
            {
                var local = email.Split('@')[0];
                name = local.Length > 0 ? char.ToUpper(local[0]) + local[1..] : "Welcome";
            }

            NameLabel.Text = string.IsNullOrWhiteSpace(name) ? "Welcome" : name;
            EmailLabel.Text = email ?? string.Empty;
            AvatarLabel.Text = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[0].ToString().ToUpper();

            PhoneLabel.Text = phone ?? string.Empty;
            PhoneLabel.IsVisible = !string.IsNullOrWhiteSpace(phone);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    // Opens a page and shows the real error if it can't be opened
    private async Task OpenAsync(string route)
    {
        try
        {
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            var inner = ex;
            while (inner.InnerException is not null) inner = inner.InnerException;
            await DisplayAlertAsync("Couldn't open this page", inner.Message, "OK");
        }
    }

    private async void OnRowTapped(object sender, TappedEventArgs e)
    {
        switch (e.Parameter?.ToString())
        {
            case "Edit Profile": await OpenAsync(nameof(EditProfilePage)); break;
            case "Change Password": await OpenAsync(nameof(ChangePasswordPage)); break;
            case "Payment Methods": await OpenAsync(nameof(PaymentMethodsPage)); break;
            case "Notifications": await OpenAsync(nameof(NotificationsPage)); break;
            case "Contact Us": await OpenAsync(nameof(ContactUsPage)); break;
            case "About": await OpenAsync(nameof(AboutPage)); break;
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
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