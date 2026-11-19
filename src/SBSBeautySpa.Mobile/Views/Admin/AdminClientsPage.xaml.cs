using SBSBeautySpa.Mobile.Services;
using SBSBeautySpa.Mobile.ViewModels.Admin;

namespace SBSBeautySpa.Mobile.Views.Admin;

public partial class AdminClientsPage : ContentPage
{
    private readonly AdminClientsViewModel _viewModel;

    public AdminClientsPage(AdminClientsViewModel viewModel)
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

    // Tap a client -> open their page (details + notes).
    private async void OnClientTapped(object sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is ClientSummary client)
        {
            await Shell.Current.GoToAsync(
                nameof(AdminClientDetailPage),
                new ShellNavigationQueryParameters { { "client", client } });
        }
    }

    // Back arrow: go back if this page was opened on top of another one,
    // otherwise (it is the Clients tab) jump to the Dashboard tab.
    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (Navigation.NavigationStack.Count > 1)
                await Shell.Current.GoToAsync("..");
            else
                await Shell.Current.GoToAsync("//AdminMain/AdminDashboard");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't go back", ex.Message, "OK");
        }
    }
}