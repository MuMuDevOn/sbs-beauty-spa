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
    public partial class AdminProductsViewModel : BaseViewModel
    {
        // Your Firebase Storage bucket. Firebase Console -> Build -> Storage shows it
        // at the top ("gs://...."). Put the part after gs:// here.
        // New projects use "<project>.firebasestorage.app", older ones "<project>.appspot.com".
        private const string StorageBucket = "sbs-beauty-spa.firebasestorage.app";

        private static readonly HttpClient Http = new();

        private readonly IFirestoreService _firestoreService;
        private readonly IShopAdminService _shopAdminService;
        private readonly IFirebaseAuthService _auth;

        public AdminProductsViewModel(
            IFirestoreService firestoreService,
            IShopAdminService shopAdminService,
            IFirebaseAuthService auth)
        {
            _firestoreService = firestoreService;
            _shopAdminService = shopAdminService;
            _auth = auth;
            Title = "Manage Products";
            ResetDraft();
        }

        public ObservableCollection<Product> Products { get; } = new();

        [ObservableProperty]
        private Product? editingProduct;

        [ObservableProperty]
        private string draftName = string.Empty;

        [ObservableProperty]
        private string draftDescription = string.Empty;

        [ObservableProperty]
        private string draftCategoryId = string.Empty;

        [ObservableProperty]
        private decimal draftPrice;

        [ObservableProperty]
        private int draftStockQuantity;

        [ObservableProperty]
        private string? draftImageUrl;

        [ObservableProperty]
        private bool isUploading;

        [ObservableProperty]
        private string? infoMessage;

        // The form is hidden until the admin taps "+ Add New Product" (or Edit on a product).
        [ObservableProperty]
        private bool isEditorOpen;

        public string AddButtonText => IsEditorOpen ? "Close form" : "+ Add New Product";

        partial void OnIsEditorOpenChanged(bool value) => OnPropertyChanged(nameof(AddButtonText));

        public bool IsEditing => EditingProduct is not null;
        public string EditorTitle => IsEditing ? $"Edit {EditingProduct!.Name}" : "New Product";

        public bool HasImage => !string.IsNullOrWhiteSpace(DraftImageUrl);
        public bool HasNoImage => !HasImage;

        partial void OnDraftImageUrlChanged(string? value)
        {
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(HasNoImage));
        }

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var products = await _firestoreService.GetProductsAsync();

                Products.Clear();
                foreach (var product in products.OrderBy(p => p.CategoryId).ThenBy(p => p.Name))
                    Products.Add(product);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load products: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void SelectForEdit(Product product)
        {
            EditingProduct = product;
            DraftName = product.Name;
            DraftDescription = product.Description;
            DraftCategoryId = product.CategoryId;
            DraftPrice = product.Price;
            DraftStockQuantity = product.StockQuantity ?? 0;
            DraftImageUrl = product.ImageUrl;
            InfoMessage = null;
            ErrorMessage = null;
            IsEditorOpen = true;
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(EditorTitle));
        }

        // "+ Add New Product" button: opens a blank form, or closes it if already open.
        [RelayCommand]
        private void ToggleNewProduct()
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
        private void StartNewProduct()
        {
            ResetDraft();
            ErrorMessage = null;
            IsEditorOpen = false;
        }

        private void ResetDraft()
        {
            EditingProduct = null;
            DraftName = string.Empty;
            DraftDescription = string.Empty;
            DraftCategoryId = string.Empty;
            DraftPrice = 0;
            DraftStockQuantity = 0;
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

        // Uploads the chosen photo to Firebase Storage (products/...) and returns its link.
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

            var objectName = $"products/{Guid.NewGuid():N}{ext}";
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
                    401 or 403 => "Storage refused the upload. Check the Storage rules allow admins to write to products/.",
                    404 => "Storage bucket not found. Check Storage is enabled and the bucket name in AdminProductsViewModel.",
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

            var input = new ProductInput(
                DraftName.Trim(),
                DraftDescription.Trim(),
                DraftCategoryId.Trim(),
                DraftPrice,
                DraftStockQuantity == 0 ? null : DraftStockQuantity,
                string.IsNullOrWhiteSpace(DraftImageUrl) ? null : DraftImageUrl
            );

            try
            {
                if (IsEditing)
                    await _shopAdminService.UpdateProductAsync(EditingProduct!.Id, input);
                else
                    await _shopAdminService.CreateProductAsync(input);

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

            // Reload AFTER IsBusy is cleared (LoadAsync does nothing while busy).
            if (saved)
            {
                await LoadAsync();
                InfoMessage = "Product saved.";
            }
        }

        [RelayCommand]
        private async Task ToggleActiveAsync(Product product)
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            var newValue = !product.IsActive;

            try
            {
                await _shopAdminService.SetProductActiveAsync(product.Id, newValue);
                product.IsActive = newValue;
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
        private async Task DeleteAsync(Product product)
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                await _shopAdminService.DeleteProductAsync(product.Id);
                Products.Remove(product);
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