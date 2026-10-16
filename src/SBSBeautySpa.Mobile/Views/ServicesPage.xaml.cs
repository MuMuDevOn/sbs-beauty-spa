using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views;

public partial class ServicesPage : ContentPage
{
    private readonly ServicesViewModel _viewModel;

    public ServicesPage(ServicesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadServicesCommand.Execute(null);
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    // Chip tapped: the tapped chip is the sender's BindingContext
    private void OnCategoryTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ServiceCategoryTab tab)
            _viewModel.SelectCategoryCommand.Execute(tab);
    }

    // "Select" button on a service row
    private void OnSelectClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ServiceListItem item)
            _viewModel.SelectServiceCommand.Execute(item.Id);
    }
}