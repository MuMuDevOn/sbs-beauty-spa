using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    public partial class AdminProductsViewModel : BaseViewModel
    {
        private readonly IFirestoreService _firestoreService;
        private readonly IShopAdminService _shopAdminService;

        public AdminProductsViewModel(IFirestoreService firestoreService, IShopAdminService shopAdminService)
        {
            _firestoreService = firestoreService;
            _shopAdminService = shopAdminService;
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

        public bool IsEditing => EditingProduct is not null;
        public string EditorTitle => IsEditing ? $"Edit {EditingProduct!.Name}" : "New Product";

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
                ErrorMessage = "Couldn't load products. Pull to refresh to try again.";
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
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(EditorTitle));
        }

        [RelayCommand]
        private void StartNewProduct() => ResetDraft();

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
                await LoadAsync();
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