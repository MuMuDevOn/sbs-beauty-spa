using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views;

public partial class BookingHistoryPage : ContentPage
{
    public BookingHistoryPage()
    {
        InitializeComponent();
        BindingContext = new BookingHistoryViewModel();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");
}