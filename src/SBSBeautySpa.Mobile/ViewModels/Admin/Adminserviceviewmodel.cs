using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    // This class controls the "Update Services" screen for admins.
    //
    // It lets the salon owner:
    //   - Add a new service
    //   - Edit an existing one
    //   - Turn a service on or off
    //   - Delete a service
    //
    // Every change goes to the server, which re-checks everything
    // (like: is the duration positive? is the deposit less than the price?).
    // This class doesn't duplicate those rules — it just shows whatever
    // error the server sends back.
    //
    // This screen has TWO parts in one:
    //   1. The LIST of services (Services)
    //   2. The EDITOR form (the DraftXxx properties)
    // The editor is either empty (for a new service) or pre-filled
    // (when the admin taps Edit on an existing one).
    public partial class AdminServicesViewModel : BaseViewModel
    {
        private readonly IFirestoreService _firestoreService;
        private readonly IAdminServicesService _adminServicesService;

        public AdminServicesViewModel(IFirestoreService firestoreService, IAdminServicesService adminServicesService)
        {
            _firestoreService = firestoreService;
            _adminServicesService = adminServicesService;
            Title = "Update Services";
            ResetDraft();
        }

        // The list of services shown in the list view.
        public ObservableCollection<Service> Services { get; } = new();

        // The service being edited (null means we're creating a new one).
        [ObservableProperty]
        private Service? editingService;

        // The draft values in the editor form.
        [ObservableProperty]
        private string draftName = string.Empty;

        [ObservableProperty]
        private string draftDescription = string.Empty;

        [ObservableProperty]
        private string draftCategoryId = string.Empty;

        [ObservableProperty]
        private decimal draftPrice;

        [ObservableProperty]
        private decimal draftDepositAmount;

        [ObservableProperty]
        private int draftDurationMinutes = 30;

        [ObservableProperty]
        private int draftBufferMinutes;

        [ObservableProperty]
        private string? draftImageUrl;

        // True when editing an existing service.
        public bool IsEditing => EditingService is not null;

        // The title shown above the editor form.
        public string EditorTitle => IsEditing ? $"Edit {EditingService!.Name}" : "New Service";

        // Loads all services (including inactive ones) from the server.
        [RelayCommand]
        private async Task LoadServicesAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                // Admin sees the FULL catalog, including inactive services.
                // (The client-facing ServicesViewModel filters out inactive
                // services on its own.)
                var services = await _firestoreService.GetServicesAsync();

                Services.Clear();
                foreach (var service in services.OrderBy(s => s.CategoryId).ThenBy(s => s.Name))
                {
                    Services.Add(service);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load services. Pull to refresh to try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Fills the editor form with an existing service's details.
        [RelayCommand]
        private void SelectForEdit(Service service)
        {
            EditingService = service;
            DraftName = service.Name;
            DraftDescription = service.Description;
            DraftCategoryId = service.CategoryId;
            DraftPrice = service.Price;
            DraftDepositAmount = service.DepositAmount;
            DraftDurationMinutes = service.DurationMinutes;
            DraftBufferMinutes = service.BufferMinutes;
            DraftImageUrl = service.ImageUrl;
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(EditorTitle));
        }

        // Clears the editor form to start a new service.
        [RelayCommand]
        private void StartNewService() => ResetDraft();

        // Empties the editor form (used for new services and after saving).
        private void ResetDraft()
        {
            EditingService = null;
            DraftName = string.Empty;
            DraftDescription = string.Empty;
            DraftCategoryId = string.Empty;
            DraftPrice = 0;
            DraftDepositAmount = 0;
            DraftDurationMinutes = 30;
            DraftBufferMinutes = 0;
            DraftImageUrl = null;
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(EditorTitle));
        }

        // Saves the service — either creates a new one or updates the existing one.
        [RelayCommand]
        private async Task SaveAsync()
        {
            if (IsBusy)
            {
                return;
            }

            // Quick client-side check for required fields.
            if (string.IsNullOrWhiteSpace(DraftName) || string.IsNullOrWhiteSpace(DraftCategoryId))
            {
                ErrorMessage = "Name and category are required.";
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            // Build the input from the draft values.
            var input = new ServiceInput(
                DraftName.Trim(),
                DraftDescription.Trim(),
                DraftCategoryId.Trim(),
                DraftPrice,
                DraftDepositAmount,
                DraftDurationMinutes,
                DraftBufferMinutes,
                string.IsNullOrWhiteSpace(DraftImageUrl) ? null : DraftImageUrl,
                EditingService?.AddOnServiceIds ?? new List<string>()
            );

            try
            {
                if (IsEditing)
                {
                    // Update an existing service.
                    await _adminServicesService.UpdateServiceAsync(EditingService!.Id, input);
                }
                else
                {
                    // Create a new service.
                    await _adminServicesService.CreateServiceAsync(input);
                }

                // Clear the form and reload the list from the server.
                // We reload instead of guessing at the merged result —
                // the server might have normalised something.
                ResetDraft();
                await LoadServicesAsync();
            }
            catch (Exception ex)
            {
                // Show the server's real validation message.
                // Example: "depositAmount cannot exceed price"
                ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Turns a service on or off.
        [RelayCommand]
        private async Task ToggleActiveAsync(Service service)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;
            var newValue = !service.IsActive;

            try
            {
                // Ask the server to change the service's active state.
                await _adminServicesService.SetServiceActiveAsync(service.Id, newValue);

                // Update the local copy.
                // The Service model is observable (IsActive uses [ObservableProperty]),
                // so the UI updates instantly without a full reload.
                service.IsActive = newValue;
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

        // Deletes a service.
        [RelayCommand]
        private async Task DeleteAsync(Service service)
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                await _adminServicesService.DeleteServiceAsync(service.Id);
                Services.Remove(service);
            }
            catch (Exception ex)
            {
                // The most common error: "This service has upcoming bookings —
                // deactivate it instead of deleting." (This comes from
                // adminServices.js on the server.)
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