using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class OrderPaymentPage : ContentPage
    {
        private readonly OrderPaymentViewModel _viewModel;

        public OrderPaymentPage(OrderPaymentViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await _viewModel.LoadAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _viewModel.CancelPolling();
        }

        private async void OnBackTapped(object? sender, TappedEventArgs e)
        {
            _viewModel.CancelPolling();
            await Shell.Current.GoToAsync("..");
        }
    }
}
