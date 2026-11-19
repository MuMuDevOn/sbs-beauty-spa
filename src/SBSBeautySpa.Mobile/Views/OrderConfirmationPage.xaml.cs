using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class OrderConfirmationPage : ContentPage
    {
        public OrderConfirmationPage(OrderConfirmationViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        // The order is already paid, so "back" leaves checkout instead of stepping back into it.
        protected override bool OnBackButtonPressed()
        {
            _ = Shell.Current.GoToAsync("//ShopView");
            return true;
        }

        private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("//ShopView");

        private async void OnContinueShoppingClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//ShopView");

        private async void OnHomeClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//HomePage");
    }
}
