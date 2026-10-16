using SBSBeautySpa.Mobile.Views;

namespace SBSBeautySpa.Mobile.Views;

public partial class BookingPage : ContentPage
{
    public BookingPage() => InitializeComponent();

    private async void OnServicesTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(ServicesPage));

    private async void OnHistoryTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(BookingHistoryPage));
}