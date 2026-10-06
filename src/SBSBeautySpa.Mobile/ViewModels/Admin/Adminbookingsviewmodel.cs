using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    // One filter tab at the top of the screen.
    // Status = which booking status to show (null means "All")
    // Label = what the tab says ("All", "Pending", etc.)
    public record BookingStatusFilter(BookingStatus? Status, string Label);

    // This class controls the "Manage Bookings" screen for admins.
    //
    // It shows:
    //   - Every client's bookings
    //   - Filter tabs by status
    //   - Actions: confirm, complete, or cancel
    //
    // Every action goes through the server, which checks:
    //   - Is this a valid state change?
    //   - Has the booking already changed since the list was loaded?
    //
    // If a booking already moved on (like another admin cancelled it
    // a second ago), the server rejects the action. This class then
    // shows the real error instead of pretending the local list is right.
    public partial class AdminBookingsViewModel : BaseViewModel
    {
        private readonly IAdminDataService _adminDataService;
        private readonly IAdminBookingActionsService _actionsService;

        public AdminBookingsViewModel(IAdminDataService adminDataService, IAdminBookingActionsService actionsService)
        {
            _adminDataService = adminDataService;
            _actionsService = actionsService;
            Title = "Manage Bookings";

            // Set up the filter tabs.
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

        // The filter tabs shown at the top.
        public ObservableCollection<BookingStatusFilter> StatusFilters { get; }

        // The list of bookings shown below.
        public ObservableCollection<Booking> Bookings { get; } = new();

        // The currently selected filter tab.
        [ObservableProperty]
        private BookingStatusFilter selectedFilter;

        // When the user taps a different filter, reload the bookings.
        partial void OnSelectedFilterChanged(BookingStatusFilter value) => LoadBookingsCommand.Execute(null);

        // Loads bookings from the server based on the selected filter.
        [RelayCommand]
        private async Task LoadBookingsAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                // Ask the server for bookings matching the filter.
                var bookings = await _adminDataService.GetBookingsAsync(SelectedFilter.Status);

                // Sort by date, then by time.
                Bookings.Clear();
                foreach (var booking in bookings.OrderBy(b => b.Date).ThenBy(b => b.StartTime))
                {
                    Bookings.Add(booking);
                }

                // If no bookings match, show a friendly message.
                if (Bookings.Count == 0)
                {
                    ErrorMessage = $"No {SelectedFilter.Label.ToLowerInvariant()} bookings right now.";
                }
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

        // Confirms a booking (changes status to Confirmed).
        [RelayCommand]
        private Task ConfirmBookingAsync(Booking booking) => ChangeStatusAsync(booking, BookingStatus.Confirmed);

        // Completes a booking (changes status to Completed).
        [RelayCommand]
        private Task CompleteBookingAsync(Booking booking) => ChangeStatusAsync(booking, BookingStatus.Completed);

        // Cancels a booking (changes status to Cancelled).
        // The View passes (Booking, Reason) as a tuple.
        // The reason is optional but useful for the records.
        [RelayCommand]
        private Task CancelBookingAsync((Booking Booking, string? Reason) args) =>
            ChangeStatusAsync(args.Booking, BookingStatus.Cancelled, args.Reason);

        // The shared method all three actions use.
        // It talks to the server, then updates the local list.
        private async Task ChangeStatusAsync(Booking booking, BookingStatus newStatus, string? cancellationReason = null)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                // Ask the server to change the status.
                await _actionsService.UpdateBookingStatusAsync(booking.Id, newStatus, cancellationReason);

                // If we got here, the server agreed.
                // Update the booking in the local list so the UI matches.
                booking.Status = newStatus;
                if (newStatus == BookingStatus.Cancelled)
                {
                    booking.CancellationReason = cancellationReason;
                }

                // If the current filter no longer matches this booking's
                // new status (like filtering "Pending" but it just got
                // confirmed), remove it from the visible list.
                if (SelectedFilter.Status.HasValue && SelectedFilter.Status.Value != newStatus)
                {
                    Bookings.Remove(booking);
                }
            }
            catch (Exception ex)
            {
                // Show the server's real error message.
                // Example: "Invalid state transition"
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