using SBSBeautySpa.Mobile.Services;
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
        private readonly CheckoutSession _session;

        public CartView(CartViewModel viewModel, CheckoutSession session)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _session = session;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.LoadCommand.Execute(null);
        }

        private async void OnBackTapped(object? sender, TappedEventArgs e) => await GoBackAsync();

        private async void OnContinueShoppingClicked(object? sender, EventArgs e) => await GoBackAsync();

        // Proceed to Checkout -> Delivery Addresses -> Delivery Options -> Payment -> Confirmation.
        // The order itself is created on the Payment step, once a delivery option is chosen.
        private async void OnCheckoutClicked(object? sender, EventArgs e)
        {
            if (_viewModel.Lines.Count == 0)
            {
                return;
            }

            if (_viewModel.Lines.Any(l => !l.IsAvailable))
            {
                _viewModel.ErrorMessage = "Remove the unavailable items before checking out.";
                return;
            }

            _session.Reset();
            await Shell.Current.GoToAsync("DeliveryAddressesPage");
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