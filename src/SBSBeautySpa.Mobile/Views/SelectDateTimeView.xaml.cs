using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    // This is the code-behind for the "Select Date & Time" page.
    //
    // Mihle and thumeka right now you need to two things that aren't in this file:
    //   1. In MauiProgram.cs:
    //        builder.Services.AddTransient<CalendarViewModel>();
    //        builder.Services.AddTransient<SelectDateTimeView>();
    //   2. In AppShell.xaml.cs:
    //        Routing.RegisterRoute(nameof(SelectDateTimeView), typeof(SelectDateTimeView));
    //
    // Without these, the page can't be created or navigated to.
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

            // The previous screen (Service Detail) should have already set
            // _viewModel.ServiceId before this page appears.
            // That hand-off isn't built yet — for now, this assumes
            // _viewModel.ServiceId is already filled in.
            //
            // No grid-building call is needed here — SfCalendar renders
            // itself once SelectedDate is bound. The constructor's default
            // SelectedDateTime already triggers the first LoadTimeSlotsAsync
            // via OnSelectedDateTimeChanged, so this is only a fallback
            // in case that hasn't fired yet by the time the page appears.
            _viewModel.LoadTimeSlotsCommand.Execute(null);
        }

        private async void OnBackTapped(object? sender, TappedEventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        private async void OnContinueClicked(object? sender, EventArgs e)
        {
            if (!_viewModel.CanContinue || _viewModel.SelectedSlot is null)
            {
                return;
            }

            // The Add-Ons page (the next screen in the booking flow) hasn't
            // been built yet. This call assumes a registered "AddOnsView"
            // route that doesn't exist.
            //
            // Once that page exists:
            //   - Register its route in AppShell.xaml.cs
            //   - Update the route name below if it's different
            //
            // The data is passed as a dictionary. The next page's ViewModel
            // reads these values to know which service, date, and time
            // the user picked.
            await Shell.Current.GoToAsync("AddOnsView", new Dictionary<string, object>
            {
                ["ServiceId"] = _viewModel.ServiceId,
                ["SelectedDate"] = _viewModel.SelectedDate,
                ["SelectedStartTime"] = _viewModel.SelectedSlot.StartTime,
            });
        }
    }
}