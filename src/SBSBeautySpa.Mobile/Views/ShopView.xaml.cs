using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    /// <summary>
    /// Needs app-level wiring that isn't in this file:
    ///   MauiProgram.cs:  AddTransient&lt;ShopViewModel&gt;(); AddTransient&lt;ShopView&gt;();
    ///   AppShell:        this page as a tab, and for the cart icon to work,
    ///                    Routing.RegisterRoute(nameof(CartView), typeof(CartView));
    /// </summary>
    public partial class ShopView : ContentPage
    {
        private readonly ShopViewModel _viewModel;

        public ShopView(ShopViewModel viewModel)
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

        private async void OnBackTapped(object? sender, TappedEventArgs e)
        {
            // The wireframe draws a back arrow, but Shop is a tab root with
            // nothing behind it — only go back if there's somewhere to go.
            if (Navigation.NavigationStack.Count > 1)
            {
                await Shell.Current.GoToAsync("..");
            }
        }

        private async void OnCartTapped(object? sender, TappedEventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(CartView));
        }
    }
}
