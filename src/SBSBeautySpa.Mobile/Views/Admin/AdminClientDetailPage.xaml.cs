using SBSBeautySpa.Mobile.Services;
using SBSBeautySpa.Mobile.ViewModels.Admin;

namespace SBSBeautySpa.Mobile.Views.Admin;

public partial class AdminClientDetailPage : ContentPage, IQueryAttributable
{
    private readonly AdminClientDetailViewModel _viewModel;

    public AdminClientDetailPage(AdminClientDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    // Receives the tapped client from the Clients list.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("client", out var value) && value is ClientSummary client)
            _viewModel.SetClient(client);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");
}
