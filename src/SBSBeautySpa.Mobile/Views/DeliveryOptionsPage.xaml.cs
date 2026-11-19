using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class DeliveryOptionsPage : ContentPage
    {
        private readonly DeliveryOptionsViewModel _viewModel;

        public DeliveryOptionsPage(DeliveryOptionsViewModel viewModel)
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
