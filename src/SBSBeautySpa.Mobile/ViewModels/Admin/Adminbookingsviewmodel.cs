using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    public record BookingStatusFilter(BookingStatus? Status, string Label);

    public partial class AdminBookingsViewModel : BaseViewModel
    {
        private readonly IAdminDataService _adminDataService;
        private readonly IAdminBookingActionsService _actionsService;

        public AdminBookingsViewModel(IAdminDataService adminDataService, IAdminBookingActionsService actionsService)
        {
            _adminDataService = adminDataService;
            _actionsService = actionsService;
            Title = "Manage Bookings";

            StatusFilters = new ObservableCollection<BookingStatusFilter>
            {
                new(null, "All"),
                new(BookingStatus.Pending, "Pending"),
                new(BookingStatus.Confirmed, "Confirmed"),
                new(BookingStatus.Completed, "Completed"),
                new(BookingStatus.Cancelled, "Cancelled"),
            };
            selectedFilter = StatusFilters[0];
        }

        public ObservableCollection<BookingStatusFilter> StatusFilters { get; }
        public ObservableCollection<Booking> Bookings { get; } = new();

        [ObservableProperty]
        private BookingStatusFilter selectedFilter;

        partial void OnSelectedFilterChanged(BookingStatusFilter value) => LoadBookingsCommand.Execute(null);

        [RelayCommand]
        private async Task LoadBookingsAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var bookings = await _adminDataService.GetBookingsAsync(SelectedFilter.Status);

                Bookings.Clear();
                foreach (var booking in bookings.OrderBy(b => b.Date).ThenBy(b => b.StartTime))
                    Bookings.Add(booking);

                if (Bookings.Count == 0)
                    ErrorMessage = $"No {SelectedFilter.Label.ToLowerInvariant()} bookings right now.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load bookings. Pull to refresh to try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private Task ConfirmBookingAsync(Booking booking) => ChangeStatusAsync(booking, BookingStatus.Confirmed);

        [RelayCommand]
        private Task CompleteBookingAsync(Booking booking) => ChangeStatusAsync(booking, BookingStatus.Completed);

        [RelayCommand]
        private Task CancelBookingAsync((Booking Booking, string? Reason) args) =>
            ChangeStatusAsync(args.Booking, BookingStatus.Cancelled, args.Reason);

        private async Task ChangeStatusAsync(Booking booking, BookingStatus newStatus, string? cancellationReason = null)
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                await _actionsService.UpdateBookingStatusAsync(booking.Id, newStatus, cancellationReason);

                booking.Status = newStatus;
                if (newStatus == BookingStatus.Cancelled)
                    booking.CancellationReason = cancellationReason;

                if (SelectedFilter.Status.HasValue && SelectedFilter.Status.Value != newStatus)
                    Bookings.Remove(booking);
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}