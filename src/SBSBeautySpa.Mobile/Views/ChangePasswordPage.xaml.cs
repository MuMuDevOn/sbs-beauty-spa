using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.Views;

public partial class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage() => InitializeComponent();

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void OnToggleTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is Entry entry)
            entry.IsPassword = !entry.IsPassword;
    }

    private async void OnSavePasswordClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CurrentPasswordEntry.Text) ||
            string.IsNullOrWhiteSpace(NewPasswordEntry.Text) ||
            string.IsNullOrWhiteSpace(ConfirmPasswordEntry.Text))
        {
            await DisplayAlertAsync("Missing details", "Please fill in all fields", "OK");
            return;
        }

        var problem = PasswordRules.Validate(NewPasswordEntry.Text);
        if (problem is not null)
        {
            await DisplayAlertAsync("Choose a stronger password", problem, "OK");
            return;
        }

        if (NewPasswordEntry.Text != ConfirmPasswordEntry.Text)
        {
            await DisplayAlertAsync("No match", "The new passwords don't match", "OK");
            return;
        }

        var auth = IPlatformApplication.Current?.Services.GetService<IFirebaseAuthService>();
        if (auth is null) return;

        var button = sender as Button;
        if (button is not null) button.IsEnabled = false;
        try
        {
            var result = await auth.ChangePasswordAsync(CurrentPasswordEntry.Text, NewPasswordEntry.Text);
            if (result.Success)
            {
                CurrentPasswordEntry.Text = string.Empty;
                NewPasswordEntry.Text = string.Empty;
                ConfirmPasswordEntry.Text = string.Empty;
                await DisplayAlertAsync("Saved", "Your password has been changed. Please log in again with your new password.", "OK");

                // Changing the password ends every old login, so go back to the Log In screen.
                var window = Application.Current?.Windows.FirstOrDefault();
                if (window is not null)
                {
                    var authShell = new AuthShell();
                    window.Page = authShell;
                    await authShell.GoToAsync("//LoginPage");
                }
            }
            else
            {
                await DisplayAlertAsync("Couldn't change password", result.ErrorMessage ?? "Please try again.", "OK");
            }
        }
        finally
        {
            if (button is not null) button.IsEnabled = true;
        }
    }
}