using SBSBeautySpa.Mobile.ViewModels.Admin;

namespace SBSBeautySpa.Mobile.Views.Admin;

public partial class AdminHoursPage : ContentPage
{
    private readonly AdminHoursViewModel _viewModel;

    public AdminHoursPage(AdminHoursViewModel viewModel)
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

    // Back arrow: always lands on the Dashboard tab.
    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        // If this page was opened on top of another, close it first.
        if (Navigation.NavigationStack.Count > 1)
            await Navigation.PopToRootAsync(false);

        // Then make sure the first tab (Dashboard) is the one showing.
        var tabBar = Shell.Current.CurrentItem;
        if (tabBar?.Items.Count > 0)
            tabBar.CurrentItem = tabBar.Items[0];
    }
}