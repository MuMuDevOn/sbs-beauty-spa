using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class DeliveryAddressesPage : ContentPage
    {
        private readonly DeliveryAddressesViewModel _viewModel;

        public DeliveryAddressesPage(DeliveryAddressesViewModel viewModel)
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
    }
}
