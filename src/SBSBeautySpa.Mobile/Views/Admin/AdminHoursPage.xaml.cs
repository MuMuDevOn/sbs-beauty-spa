namespace SBSBeautySpa.Mobile.Views.Admin;

public partial class AdminHoursPage : ContentPage
{
    public AdminHoursPage()
    {
        InitializeComponent();
        // TODO: inject and bind AdminHoursViewModel once it's built.
        // For now, the page renders but bindings ({Binding Days}, {Binding SaveCommand}, etc.)
        // have no source, so nothing happens on tap.
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");
}