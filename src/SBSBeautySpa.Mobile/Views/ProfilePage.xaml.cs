namespace SBSBeautySpa.Mobile.Views;

public partial class ProfilePage : ContentPage
{
    public ProfilePage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//HomePage");

    private async void OnRowTapped(object sender, TappedEventArgs e)
    {
        switch (e.Parameter?.ToString())
        {
            case "Edit Profile":
                await Shell.Current.GoToAsync(nameof(EditProfilePage)); break;
            case "Change Password":
                await Shell.Current.GoToAsync(nameof(ChangePasswordPage)); break;
            case "Payment Methods":
                await Shell.Current.GoToAsync(nameof(PaymentMethodsPage)); break;
            case "Notifications":
                await Shell.Current.GoToAsync(nameof(NotificationsPage)); break;
            case "Contact Us":
                await Shell.Current.GoToAsync(nameof(ContactUsPage)); break;
            case "About":
                await Shell.Current.GoToAsync(nameof(AboutPage)); break;
            default: break;
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//GetStartedPage");
    }
}