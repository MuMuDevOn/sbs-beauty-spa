using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
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
            // Pushed on top of something -> go back; as a tab root -> jump to the dashboard (first tab).
            if (Navigation.NavigationStack.Count > 1)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                var item = Shell.Current.CurrentItem;
                item.CurrentItem = item.Items[0];
            }
        }

        private async void OnHistoryTapped(object? sender, TappedEventArgs e)
        {
            await Shell.Current.GoToAsync("PurchaseHistoryPage");
        }

        private async void OnCartTapped(object? sender, TappedEventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(CartView));
        }
    }
}