using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.Views;

public partial class ContactUsPage : ContentPage
{
    public ContactUsPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var auth = IPlatformApplication.Current?.Services.GetService<IFirebaseAuthService>();
            var token = auth is null ? null : await auth.GetIdTokenAsync();

            var email = TokenClaims.Read(token, "email");
            var name = TokenClaims.Read(token, "name");

            if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email))
            {
                var local = email.Split('@')[0];
                name = local.Length > 0 ? char.ToUpper(local[0]) + local[1..] : "";
            }

            if (string.IsNullOrWhiteSpace(NameEntry.Text)) NameEntry.Text = name ?? "";
            if (string.IsNullOrWhiteSpace(EmailEntry.Text)) EmailEntry.Text = email ?? "";
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void OnChevronTapped(object sender, TappedEventArgs e)
        => TopicPicker.Focus();

    private async void OnSendClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameEntry.Text) ||
            string.IsNullOrWhiteSpace(EmailEntry.Text))
        {
            await DisplayAlertAsync("Missing details", "Please enter your name and email", "OK");
            return;
        }

        if (TopicPicker.SelectedIndex < 0)
        {
            await DisplayAlertAsync("Choose a topic", "Please select a topic", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(MessageEditor.Text))
        {
            await DisplayAlertAsync("Empty message", "Please describe how we can help", "OK");
            return;
        }

        var contact = IPlatformApplication.Current?.Services.GetService<IContactMessagesService>();
        if (contact is null)
        {
            await DisplayAlertAsync("Not available", "The contact service isn't registered in MauiProgram.cs.", "OK");
            return;
        }

        var button = sender as Button;
        if (button is not null) button.IsEnabled = false;
        try
        {
            await contact.SubmitContactMessageAsync(
                NameEntry.Text.Trim(),
                EmailEntry.Text.Trim(),
                phone: null,
                subject: TopicPicker.SelectedItem?.ToString(),
                message: MessageEditor.Text.Trim());

            await DisplayAlertAsync("Message sent", "Thanks! We'll get back to you soon.", "OK");
            TopicPicker.SelectedIndex = -1;
            MessageEditor.Text = string.Empty;
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't send", ex.Message, "OK");
        }
        finally
        {
            if (button is not null) button.IsEnabled = true;
        }
    }
}