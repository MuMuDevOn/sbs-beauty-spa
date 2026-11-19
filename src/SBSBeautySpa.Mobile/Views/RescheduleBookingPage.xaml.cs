using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views;

public partial class RescheduleBookingPage : ContentPage
{
    private readonly RescheduleViewModel _viewModel;

    public RescheduleBookingPage(RescheduleViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_viewModel.Slots.Count == 0)
            _viewModel.LoadSlotsCommand.Execute(null);
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void OnSlotTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is RescheduleSlot slot)
            _viewModel.SelectSlotCommand.Execute(slot);
    }
}
