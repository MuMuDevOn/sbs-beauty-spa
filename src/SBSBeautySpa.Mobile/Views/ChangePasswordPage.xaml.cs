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

        if (NewPasswordEntry.Text.Length < 8)
        {
            await DisplayAlertAsync("Too short", "Use at least 8 characters", "OK");
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
                await DisplayAlertAsync("Saved", "Your password has been changed", "OK");
                await Shell.Current.GoToAsync("..");
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