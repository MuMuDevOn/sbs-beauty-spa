using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
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

        public ObservableCollection<Service> Services { get; } = new();

        [ObservableProperty]
        private Service? editingService;

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

        public bool IsEditing => EditingService is not null;
        public string EditorTitle => IsEditing ? $"Edit {EditingService!.Name}" : "New Service";

        [RelayCommand]
        private async Task LoadServicesAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var services = await _firestoreService.GetServicesAsync();

                Services.Clear();
                foreach (var service in services.OrderBy(s => s.CategoryId).ThenBy(s => s.Name))
                    Services.Add(service);
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

        [RelayCommand]
        private void StartNewService() => ResetDraft();

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

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(DraftName) || string.IsNullOrWhiteSpace(DraftCategoryId))
            {
                ErrorMessage = "Name and category are required.";
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

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
                    await _adminServicesService.UpdateServiceAsync(EditingService!.Id, input);
                else
                    await _adminServicesService.CreateServiceAsync(input);

                ResetDraft();
                await LoadServicesAsync();
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

        [RelayCommand]
        private async Task ToggleActiveAsync(Service service)
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            var newValue = !service.IsActive;

            try
            {
                await _adminServicesService.SetServiceActiveAsync(service.Id, newValue);
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

        [RelayCommand]
        private async Task DeleteAsync(Service service)
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                await _adminServicesService.DeleteServiceAsync(service.Id);
                Services.Remove(service);
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