using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NodaTime;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // This class controls the "Review Booking" screen.
    // It's the last step before payment.
    //
    // IMPORTANT: Everything shown on this screen (price, deposit) is just
    // for display. The real numbers are decided by the server when the
    // booking is created. The server re-checks:
    //   - That every selected service still exists
    //   - That the time slot is still free
    //   - What the actual price and deposit should be
    //
    // This prevents someone from sneaking in a wrong price or booking
    // a slot that was already taken a few seconds ago.
    public partial class BookingViewModel : BaseViewModel
    {
        private readonly IAvailabilityService _availabilityService;
        private readonly IFirestoreService _firestoreService;

        public BookingViewModel(IAvailabilityService availabilityService, IFirestoreService firestoreService)
        {
            _availabilityService = availabilityService;
            _firestoreService = firestoreService;
            Title = "Review Booking";
        }

        // Set by the Add-Ons screen before opening this one.
        // ServiceIds[0] is the main service, the rest are add-ons.
        public List<string> ServiceIds { get; set; } = new();

        // The date and time the client picked earlier.
        public LocalDate SelectedDate { get; set; }
        public LocalTime SelectedStartTime { get; set; }

        // The main service being booked.
        [ObservableProperty]
        private Service? primaryService;

        // Any add-on services the client selected.
        public ObservableCollection<Service> AddOnServices { get; } = new();

        // The total price (main service + all add-ons).
        public decimal Subtotal => (PrimaryService?.Price ?? 0) + AddOnServices.Sum(s => s.Price);

        // The deposit amount due (main service + all add-ons).
        public decimal DepositDue => (PrimaryService?.DepositAmount ?? 0) + AddOnServices.Sum(s => s.DepositAmount);

        // Any notes the client wants to add to the booking.
        [ObservableProperty]
        private string? notes;

        // True when the client has agreed to the cancellation policy.
        [ObservableProperty]
        private bool agreedToCancellationPolicy;

        // The ID of the booking after it's created.
        [ObservableProperty]
        private string? createdBookingId;

        // Shows the date in a readable format like "Wednesday, 15 October 2026".
        public string FormattedDate => SelectedDate.ToString("dddd, d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

        // Shows the time in a readable format like "9:00 AM".
        public string FormattedTime => SelectedStartTime.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

        // Loads the service details (main service + add-ons) from the server.
        [RelayCommand]
        private async Task LoadServiceAsync()
        {
            if (ServiceIds.Count == 0 || IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;
            AddOnServices.Clear();

            try
            {
                // Get all the services in one call.
                var services = await _firestoreService.GetServicesByIdsAsync(ServiceIds);

                // The first one is the main service.
                PrimaryService = services.FirstOrDefault(s => s.Id == ServiceIds[0]);

                if (PrimaryService is null)
                {
                    ErrorMessage = "This service is no longer available.";
                    return;
                }

                // The rest are add-ons.
                foreach (var addOn in services.Where(s => s.Id != ServiceIds[0]))
                {
                    AddOnServices.Add(addOn);
                }

                // Tell the UI that the totals have changed.
                OnPropertyChanged(nameof(Subtotal));
                OnPropertyChanged(nameof(DepositDue));
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load the service details.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // The "Confirm Booking" button can only be tapped when:
        //   - The main service is loaded
        //   - The client agreed to the cancellation policy
        //   - The app isn't already busy
        private bool CanConfirm() => PrimaryService is not null && AgreedToCancellationPolicy && !IsBusy;

        // Creates the actual booking on the server.
        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private async Task ConfirmBookingAsync()
        {
            if (!CanConfirm())
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                // Ask the server to create the booking.
                // The server re-checks everything before writing.
                CreatedBookingId = await _availabilityService.CreateBookingAsync(ServiceIds, SelectedDate, SelectedStartTime, Notes);

                // After this succeeds, the View should navigate to the
                // Payment screen, passing CreatedBookingId and
                // PaymentRequestType.Deposit.
            }
            catch (Exception ex)
            {
                // The server throws an error if the slot got taken between
                // picking the time and tapping Confirm. Send the user back
                // to pick a new time.
                ErrorMessage = "That time is no longer available. Please choose another slot.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Re-check if the button should be enabled when these change.
        partial void OnAgreedToCancellationPolicyChanged(bool value) => ConfirmBookingCommand.NotifyCanExecuteChanged();
        partial void OnPrimaryServiceChanged(Service? value) => ConfirmBookingCommand.NotifyCanExecuteChanged();
    }
}