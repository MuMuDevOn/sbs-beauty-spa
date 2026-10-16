namespace SBSBeautySpa.Mobile.Views;

public partial class EditProfilePage : ContentPage
{
    public EditProfilePage() => InitializeComponent();

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameEntry.Text) ||
            string.IsNullOrWhiteSpace(EmailEntry.Text))
        {
            await DisplayAlertAsync("Missing details", "Please enter your name and email", "OK");
            return;
        }

        // TODO: save NameEntry.Text, EmailEntry.Text, PhoneEntry.Text
        await DisplayAlertAsync("Saved", "Your changes have been saved", "OK");
    }
}