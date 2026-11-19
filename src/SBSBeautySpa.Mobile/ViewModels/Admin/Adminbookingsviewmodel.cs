using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    // One filter chip ("All", "Pending", ...). IsSelected drives its colour.
    public partial class BookingStatusFilter : ObservableObject
    {
        public BookingStatusFilter(BookingStatus? status, string label, bool isSelected = false)
        {
            Status = status;
            Label = label;
            this.isSelected = isSelected;
        }

        public BookingStatus? Status { get; }
        public string Label { get; }

        [ObservableProperty]
        private bool isSelected;
    }

    public partial class AdminBookingsViewModel : BaseViewModel
    {
        private readonly IAdminDataService _adminDataService;
        private readonly IAdminBookingActionsService _actionsService;
        private readonly IFirestoreService _firestore;

        public AdminBookingsViewModel(
            IAdminDataService adminDataService,
            IAdminBookingActionsService actionsService,
            IFirestoreService firestore)
        {
            _adminDataService = adminDataService;
            _actionsService = actionsService;
            _firestore = firestore;
            Title = "Manage Bookings";

            StatusFilters = new ObservableCollection<BookingStatusFilter>
            {
                new(null, "All", isSelected: true),
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

        [ObservableProperty]
        private bool hasNoBookings;

        [RelayCommand]
        private async Task SelectFilterAsync(BookingStatusFilter? filter)
        {
            if (filter is null) return;

            foreach (var f in StatusFilters) f.IsSelected = f == filter;
            SelectedFilter = filter;
            await LoadBookingsAsync();
        }

        [RelayCommand]
        private async Task LoadBookingsAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var bookings = await _adminDataService.GetBookingsAsync(SelectedFilter.Status);

                // A booking only stores the service id, so look the names up.
                var names = new Dictionary<string, string>();
                try
                {
                    var services = await _firestore.GetServicesAsync();
                    names = services.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First().Name);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);
                }

                Bookings.Clear();
                foreach (var booking in bookings.OrderBy(b => b.Date).ThenBy(b => b.StartTime))
                {
                    booking.ServiceName = names.TryGetValue(booking.ServiceId, out var n) ? n : "Service";
                    Bookings.Add(booking);
                }
                HasNoBookings = Bookings.Count == 0;
            }
            catch (Exception ex)
            {
                // Show the real reason on screen (it used to be hidden, so a failed load
                // looked exactly like "no bookings").
                ErrorMessage = $"Couldn't load bookings: {ex.Message}";
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
        private async Task CancelBookingAsync(Booking booking)
        {
            if (booking is null) return;

            // Ask for an (optional) reason first. null means the admin backed out.
            var reason = await Shell.Current.DisplayPromptAsync(
                "Cancel booking", "Reason (optional):", "Cancel booking", "Keep it");
            if (reason is null) return;

            await ChangeStatusAsync(booking, BookingStatus.Cancelled, string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
        }

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
                HasNoBookings = Bookings.Count == 0;
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