using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class InvoicePage : ContentPage
    {
        public InvoicePage(InvoiceViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

        private async void OnTrackClicked(object? sender, EventArgs e)
        {
            var orderId = (BindingContext as InvoiceViewModel)?.Order?.Id;
            if (string.IsNullOrWhiteSpace(orderId))
            {
                await DisplayAlert("Track order", "We couldn't find this order.", "OK");
                return;
            }

            try
            {
                await Shell.Current.GoToAsync($"TrackOrderPage?orderId={Uri.EscapeDataString(orderId)}");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Track order", "Couldn't open tracking: " + ex.Message, "OK");
            }
        }
    }
}