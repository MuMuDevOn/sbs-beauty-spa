namespace SBSBeautySpa.Mobile.Views;

public partial class AboutPage : ContentPage
{
    public AboutPage() => InitializeComponent();

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");
}