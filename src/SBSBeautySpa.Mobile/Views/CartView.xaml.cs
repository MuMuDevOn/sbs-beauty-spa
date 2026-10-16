using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    /// <summary>
    /// Needs app-level wiring that isn't in this file:
    ///   MauiProgram.cs:  AddTransient&lt;CartViewModel&gt;(); AddTransient&lt;CartView&gt;();
    ///   AppShell:        Routing.RegisterRoute(nameof(CartView), typeof(CartView));
    /// </summary>
    public partial class CartView : ContentPage
    {
        private readonly CartViewModel _viewModel;

        public CartView(CartViewModel viewModel)
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

        private async void OnBackTapped(object? sender, TappedEventArgs e) => await GoBackAsync();

        private async void OnContinueShoppingClicked(object? sender, EventArgs e) => await GoBackAsync();

        private async void OnCheckoutClicked(object? sender, EventArgs e)
        {
            await _viewModel.CheckoutCommand.ExecuteAsync(null);

            if (_viewModel.OrderPlaced)
            {
                await DisplayAlert("Order placed", $"Your order has been placed. Total: {_viewModel.PlacedOrderTotalLabel}", "OK");
                await GoBackAsync();
            }
        }

        private async Task GoBackAsync()
        {
            if (Navigation.NavigationStack.Count > 1)
            {
                await Shell.Current.GoToAsync("..");
            }
        }
    }
}
