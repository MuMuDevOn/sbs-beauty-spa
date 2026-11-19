using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    // Code-behind for the "Select Date & Time" page.
    //
    // Needs (already done if the page opens):
    //   MauiProgram.cs:  AddTransient<CalendarViewModel>();  AddTransient<SelectDateTimeView>();
    //   ClientShell:     Routing.RegisterRoute(nameof(SelectDateTimeView), typeof(SelectDateTimeView));
    public partial class SelectDateTimeView : ContentPage
    {
        private readonly CalendarViewModel _viewModel;

        public SelectDateTimeView(CalendarViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // SfCalendar renders itself once SelectedDate is bound. The default
            // SelectedDateTime already triggers the first LoadTimeSlotsAsync, so this
            // is only a fallback in case that hasn't fired yet.
            _viewModel.LoadTimeSlotsCommand.Execute(null);
        }

        private async void OnBackTapped(object? sender, TappedEventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        // Continue -> Add-Ons page, carrying the service, date and time the client picked.
        private async void OnContinueClicked(object? sender, EventArgs e)
        {
            if (!_viewModel.CanContinue || _viewModel.SelectedSlot is null)
            {
                return;
            }

            // "AddOnsPage" is the route registered in ClientShell.RegisterRoutes().
            await Shell.Current.GoToAsync("AddOnsPage", new ShellNavigationQueryParameters
            {
                ["ServiceId"] = _viewModel.ServiceId,
                ["SelectedDate"] = _viewModel.SelectedDate,
                ["SelectedStartTime"] = _viewModel.SelectedSlot.StartTime,
            });
        }
    }
}