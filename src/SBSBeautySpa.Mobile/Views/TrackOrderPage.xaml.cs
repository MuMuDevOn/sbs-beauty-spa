using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class TrackOrderPage : ContentPage
    {
        public TrackOrderPage(TrackOrderViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is TrackOrderViewModel vm)
            {
                await vm.LoadCommand.ExecuteAsync(null);
            }
        }

        private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
    }
}
