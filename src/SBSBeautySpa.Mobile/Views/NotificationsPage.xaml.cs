namespace SBSBeautySpa.Mobile.Views;

public partial class NotificationsPage : ContentPage
{
    // True while the saved settings are being loaded, so loading isn't saved back
    private bool _loading = true;

    public NotificationsPage()
    {
        InitializeComponent();

        BookingConfirmationsSwitch.IsToggled = Preferences.Default.Get("BookingConfirmations", true);
        BookingChangesSwitch.IsToggled = Preferences.Default.Get("BookingChanges", true);
        AppointmentRemindersSwitch.IsToggled = Preferences.Default.Get("AppointmentReminders", true);

        _loading = false;
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void OnSwitchToggled(object sender, ToggledEventArgs e)
    {
        if (_loading || sender is not Switch sw || string.IsNullOrEmpty(sw.AutomationId)) return;
        Preferences.Default.Set(sw.AutomationId, e.Value);
    }
}