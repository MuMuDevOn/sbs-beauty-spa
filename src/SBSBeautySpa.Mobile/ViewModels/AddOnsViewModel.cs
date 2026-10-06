using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NodaTime;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // One row on the Add-Ons screen.
    // Each row shows a service the client can add on.
    public partial class AddOnOption : ObservableObject
    {
        public Service Service { get; }

        // Tells us if this add-on is currently selected.
        [ObservableProperty]
        private bool isSelected;

        public AddOnOption(Service service) => Service = service;
    }

    // This class controls the "Add-Ons" screen.
    // It sits between "Select Date & Time" and "Review Booking".
    //
    // IMPORTANT: The EstimatedTotal shown here is just for the UI — it's
    // an estimate. The real total is calculated by the server when the
    // booking is created. The server re-checks every selected service
    // and re-totals everything. Nothing calculated in this class ends up
    // on the actual Booking record.
    public partial class AddOnsViewModel : BaseViewModel
    {
        private readonly IFirestoreService _firestoreService;

        public AddOnsViewModel(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            Title = "Add-Ons";
        }

        // Set by the previous screens before opening this one.

        // The main service the client picked.
        public string PrimaryServiceId { get; set; } = string.Empty;

        // The date and time the client picked.
        public LocalDate SelectedDate { get; set; }
        public LocalTime SelectedStartTime { get; set; }

        // The full details of the main service (loaded from the server).
        [ObservableProperty]
        private Service? primaryService;

        // The list of available add-ons the client can choose from.
        public ObservableCollection<AddOnOption> AddOnOptions { get; } = new();

        // The total price of all selected add-ons.
        public decimal EstimatedAddOnTotal => AddOnOptions.Where(o => o.IsSelected).Sum(o => o.Service.Price);

        // The total estimated price (main service + selected add-ons).
        public decimal EstimatedTotal => (PrimaryService?.Price ?? 0) + EstimatedAddOnTotal;

        // The full list of service IDs: the main service, plus any selected add-ons.
        // This gets passed to the booking when the client continues.
        public IReadOnlyList<string> SelectedServiceIds =>
            new[] { PrimaryServiceId }.Concat(AddOnOptions.Where(o => o.IsSelected).Select(o => o.Service.Id)).ToList();

        // Loads the main service and its available add-ons from the server.
        [RelayCommand]
        private async Task LoadAddOnsAsync()
        {
            if (string.IsNullOrEmpty(PrimaryServiceId) || IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;
            AddOnOptions.Clear();

            try
            {
                // Load the main service.
                PrimaryService = await _firestoreService.GetServiceAsync(PrimaryServiceId);

                // If the service has no add-ons, we're done — the screen
                // just shows the "Continue" button.
                if (PrimaryService is null || PrimaryService.AddOnServiceIds.Count == 0)
                {
                    return;
                }

                // Load all the add-on services in one call.
                var addOnServices = await _firestoreService.GetServicesByIdsAsync(PrimaryService.AddOnServiceIds);

                // Only show add-ons that are still active.
                foreach (var service in addOnServices.Where(s => s.IsActive))
                {
                    var option = new AddOnOption(service);

                    // When this option is toggled on or off, update the total.
                    option.PropertyChanged += (_, __) => OnPropertyChanged(nameof(EstimatedAddOnTotal));

                    AddOnOptions.Add(option);
                }

                // Tell the UI that the total has changed.
                OnPropertyChanged(nameof(EstimatedTotal));
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load add-ons. You can still continue without any.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // When the primary service changes, the total changes too.
        partial void OnPrimaryServiceChanged(Service? value) => OnPropertyChanged(nameof(EstimatedTotal));
    }
}