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

        // TODO: verify CurrentPasswordEntry.Text and update the password
        await DisplayAlertAsync("Saved", "Your password has been changed", "OK");
    }
}