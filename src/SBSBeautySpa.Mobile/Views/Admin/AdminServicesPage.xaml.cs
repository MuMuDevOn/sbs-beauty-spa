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

    // Back arrow: go back if this page was opened on top of another page,
    // otherwise (when it's a tab) switch to the Dashboard tab.
    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        var tabBar = Shell.Current.CurrentItem;
        if (tabBar?.Items.Count > 0)
            tabBar.CurrentItem = tabBar.Items[0];
    }
}