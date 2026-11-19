using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    public partial class AdminServicesViewModel : BaseViewModel
    {
        // Same bucket as the products page (Firebase Console -> Storage shows it as gs://...).
        private const string StorageBucket = "sbs-beauty-spa.firebasestorage.app";

        private static readonly HttpClient Http = new();

        private readonly IFirestoreService _firestoreService;
        private readonly IAdminServicesService _adminServicesService;
        private readonly IFirebaseAuthService _auth;

        public AdminServicesViewModel(
            IFirestoreService firestoreService,
            IAdminServicesService adminServicesService,
            IFirebaseAuthService auth)
        {
            _firestoreService = firestoreService;
            _adminServicesService = adminServicesService;
            _auth = auth;
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

        [ObservableProperty]
        private bool isUploading;

        [ObservableProperty]
        private string? infoMessage;

        // The form is hidden until the admin taps "+ Add New Service" (or Edit on a service).
        [ObservableProperty]
        private bool isEditorOpen;

        public bool IsEditing => EditingService is not null;
        public string EditorTitle => IsEditing ? $"Edit {EditingService!.Name}" : "New Service";

        public string AddButtonText => IsEditorOpen ? "Close form" : "+ Add New Service";

        public bool HasImage => !string.IsNullOrWhiteSpace(DraftImageUrl);
        public bool HasNoImage => !HasImage;

        partial void OnIsEditorOpenChanged(bool value) => OnPropertyChanged(nameof(AddButtonText));

        partial void OnDraftImageUrlChanged(string? value)
        {
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(HasNoImage));
        }

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
                ErrorMessage = "Couldn't load services: " + ex.Message;
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
            InfoMessage = null;
            ErrorMessage = null;
            IsEditorOpen = true;
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(EditorTitle));
        }

        // "+ Add New Service" button: opens a blank form, or closes it if already open.
        [RelayCommand]
        private void ToggleNewService()
        {
            if (IsEditorOpen && !IsEditing)
            {
                IsEditorOpen = false;
                return;
            }

            ResetDraft();
            ErrorMessage = null;
            InfoMessage = null;
            IsEditorOpen = true;
        }

        // Cancel button: clear the form and close it.
        [RelayCommand]
        private void StartNewService()
        {
            ResetDraft();
            ErrorMessage = null;
            IsEditorOpen = false;
        }

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

        // ---------- Photo from the device ----------

        [RelayCommand]
        private async Task PickPhotoAsync()
        {
            if (IsUploading || IsBusy) return;

            ErrorMessage = null;
            InfoMessage = null;

            try
            {
                FileResult? photo = await MediaPicker.Default.PickPhotoAsync();
                if (photo is null) return;   // admin cancelled

                IsUploading = true;
                DraftImageUrl = await UploadPhotoAsync(photo);
                InfoMessage = "Photo uploaded.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't upload the photo: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsUploading = false;
            }
        }

        [RelayCommand]
        private void RemovePhoto()
        {
            DraftImageUrl = null;
            InfoMessage = null;
        }

        // Uploads the chosen photo to Firebase Storage (services/...) and returns its link.
        private async Task<string> UploadPhotoAsync(FileResult photo)
        {
            var idToken = await _auth.GetIdTokenAsync();
            if (string.IsNullOrEmpty(idToken))
                throw new InvalidOperationException("Please sign in again.");

            var ext = Path.GetExtension(photo.FileName)?.ToLowerInvariant() ?? ".jpg";
            var contentType = ext switch
            {
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg",
            };

            byte[] bytes;
            using (var stream = await photo.OpenReadAsync())
            using (var ms = new MemoryStream())
            {
                await stream.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            if (bytes.Length > 5 * 1024 * 1024)
                throw new InvalidOperationException("That photo is over 5 MB. Please choose a smaller one.");

            var objectName = $"services/{Guid.NewGuid():N}{ext}";
            var uploadUrl =
                $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o?name={Uri.EscapeDataString(objectName)}";

            using var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Firebase", idToken);

            using var response = await Http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                var hint = (int)response.StatusCode switch
                {
                    401 or 403 => "Storage refused the upload. Check the Storage rules allow admins to write to services/.",
                    404 => "Storage bucket not found. Check Storage is enabled and the bucket name in AdminServicesViewModel.",
                    _ => body,
                };
                throw new InvalidOperationException(hint);
            }

            using var doc = JsonDocument.Parse(body);
            var name = doc.RootElement.GetProperty("name").GetString() ?? objectName;
            var token = doc.RootElement.TryGetProperty("downloadTokens", out var t) ? t.GetString() : null;

            return $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o/{Uri.EscapeDataString(name)}?alt=media"
                   + (string.IsNullOrEmpty(token) ? "" : $"&token={token}");
        }

        // ---------- Save / toggle / delete ----------

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (IsBusy || IsUploading) return;

            if (string.IsNullOrWhiteSpace(DraftName) || string.IsNullOrWhiteSpace(DraftCategoryId))
            {
                ErrorMessage = "Name and category are required.";
                return;
            }

            IsBusy = true;
            ErrorMessage = null;
            InfoMessage = null;
            bool saved = false;

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
                IsEditorOpen = false;
                saved = true;
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

            // Reload AFTER IsBusy is cleared (LoadServicesAsync does nothing while busy).
            if (saved)
            {
                await LoadServicesAsync();
                InfoMessage = "Service saved.";
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