using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class PurchaseHistoryPage : ContentPage
    {
        private readonly PurchaseHistoryViewModel _viewModel;

        public PurchaseHistoryPage(PurchaseHistoryViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.LoadCommand.Execute(null);
        }

        private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

        // The button sits inside an order card, so its BindingContext is that order.
        private async void OnTrackClicked(object? sender, EventArgs e)
        {
            var orderId = ((sender as Button)?.BindingContext as PurchaseOrder)?.Id;
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