using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class HomePage : ContentPage
    {
        private readonly HomeViewModel _viewModel;

        public HomePage(HomeViewModel viewModel)
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

        // Taps inside list templates: read the tapped item from the sender's BindingContext
        private void OnCategoryTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is HomeCategory category)
                _viewModel.OpenCategoryCommand.Execute(category.Id);
        }

        private void OnFilterChipTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is FilterChip chip)
                _viewModel.SelectFilterChipCommand.Execute(chip);
        }

        private void OnServiceTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is HomeServiceCard card)
                _viewModel.OpenServiceCommand.Execute(card.Id);
        }

        private void OnProductTapped(object? sender, TappedEventArgs e)
        {
            _viewModel.OpenProductCommand.Execute(null);
        }
    }
}