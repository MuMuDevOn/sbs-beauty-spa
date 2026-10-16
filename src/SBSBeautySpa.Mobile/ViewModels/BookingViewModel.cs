using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NodaTime;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
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

        public List<string> ServiceIds { get; set; } = new();
        public LocalDate SelectedDate { get; set; }
        public LocalTime SelectedStartTime { get; set; }

        [ObservableProperty]
        private Service? primaryService;

        public ObservableCollection<Service> AddOnServices { get; } = new();

        public decimal Subtotal => (PrimaryService?.Price ?? 0) + AddOnServices.Sum(s => s.Price);
        public decimal DepositDue => (PrimaryService?.DepositAmount ?? 0) + AddOnServices.Sum(s => s.DepositAmount);

        [ObservableProperty]
        private string? notes;

        [ObservableProperty]
        private bool agreedToCancellationPolicy;

        [ObservableProperty]
        private string? createdBookingId;

        public string FormattedDate => SelectedDate.ToString("dddd, d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        public string FormattedTime => SelectedStartTime.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

        [RelayCommand]
        private async Task LoadServiceAsync()
        {
            if (ServiceIds.Count == 0 || IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            AddOnServices.Clear();

            try
            {
                var services = await _firestoreService.GetServicesByIdsAsync(ServiceIds);
                PrimaryService = services.FirstOrDefault(s => s.Id == ServiceIds[0]);

                if (PrimaryService is null)
                {
                    ErrorMessage = "This service is no longer available.";
                    return;
                }

                foreach (var addOn in services.Where(s => s.Id != ServiceIds[0]))
                    AddOnServices.Add(addOn);

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

        private bool CanConfirm() => PrimaryService is not null && AgreedToCancellationPolicy && !IsBusy;

        [RelayCommand(CanExecute = nameof(CanConfirm))]
        private async Task ConfirmBookingAsync()
        {
            if (!CanConfirm()) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                CreatedBookingId = await _availabilityService.CreateBookingAsync(ServiceIds, SelectedDate, SelectedStartTime, Notes);
            }
            catch (Exception ex)
            {
                ErrorMessage = "That time is no longer available. Please choose another slot.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnAgreedToCancellationPolicyChanged(bool value) => ConfirmBookingCommand.NotifyCanExecuteChanged();
        partial void OnPrimaryServiceChanged(Service? value) => ConfirmBookingCommand.NotifyCanExecuteChanged();
    }
}