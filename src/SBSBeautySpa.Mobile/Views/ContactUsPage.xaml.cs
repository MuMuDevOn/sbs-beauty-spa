namespace SBSBeautySpa.Mobile.Views;

public partial class ContactUsPage : ContentPage
{
    public ContactUsPage() => InitializeComponent();

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

        // TODO: send to IContactMessagesService.SubmitContactMessageAsync
        await DisplayAlertAsync("Message sent", "Thanks! We'll get back to you soon.", "OK");

        TopicPicker.SelectedIndex = -1;
        MessageEditor.Text = string.Empty;
    }
}