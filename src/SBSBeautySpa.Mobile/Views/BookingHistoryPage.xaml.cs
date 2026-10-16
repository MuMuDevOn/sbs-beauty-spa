using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views;

public partial class BookingHistoryPage : ContentPage
{
    private readonly BookingHistoryViewModel _viewModel;

    public BookingHistoryPage(BookingHistoryViewModel viewModel)
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

    private async void OnBackTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void OnTabTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is BookingHistoryTab tab)
            _viewModel.SelectTabCommand.Execute(tab);
    }

    private void OnRescheduleClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is BookingHistoryItem item)
            _viewModel.RescheduleCommand.Execute(item);
    }

    private void OnViewAddressClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is BookingHistoryItem item)
            _viewModel.ViewAddressCommand.Execute(item);
    }

    private void OnPayDepositClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is BookingHistoryItem item)
            _viewModel.PayDepositCommand.Execute(item);
    }
}