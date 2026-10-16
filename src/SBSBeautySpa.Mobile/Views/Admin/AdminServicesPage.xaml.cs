using SBSBeautySpa.Mobile.ViewModels.Admin;

namespace SBSBeautySpa.Mobile.Views.Admin;

public partial class AdminServicesPage : ContentPage
{
    private readonly AdminServicesViewModel _viewModel;

    public AdminServicesPage(AdminServicesViewModel viewModel)
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

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void OnAddServiceClicked(object sender, EventArgs e)
        => _viewModel.StartNewServiceCommand.Execute(null);
}