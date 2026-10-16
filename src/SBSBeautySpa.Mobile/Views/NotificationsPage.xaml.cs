namespace SBSBeautySpa.Mobile.Views;

public partial class NotificationsPage : ContentPage
{
    public NotificationsPage() => InitializeComponent();

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void OnSwitchToggled(object sender, ToggledEventArgs e)
    {
        // TODO: persist the changed preference (sender is the Switch, e.Value is the new state)
    }
}