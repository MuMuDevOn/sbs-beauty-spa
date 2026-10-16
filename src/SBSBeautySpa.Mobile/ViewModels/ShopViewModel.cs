using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    /// <summary>
    /// Backs the Shop screen: category tabs, optional search, product grid,
    /// Add to Cart. Tabs are built from the real productCategories data
    /// (plus a leading "All"), not hardcoded. The whole active catalog is
    /// loaded once and filtered locally — tab taps and keystrokes cost
    /// nothing.
    ///
    /// Add to Cart only writes a cart line. Stock is NOT reserved by it —
    /// two people can both have the last item in their carts; createOrder
    /// (server-side, transactional) is what actually decides who gets it.
    /// </summary>
    public partial class ShopViewModel : BaseViewModel
    {
        private readonly IFirestoreService _firestore;
        private List<Product> _allProducts = new();

        public ShopViewModel(IFirestoreService firestore)
        {
            _firestore = firestore;
            Title = "Shop";
        }

        public ObservableCollection<SelectableTab> Categories { get; } = new();
        public ObservableCollection<Product> Products { get; } = new();

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private bool isSearchVisible;

        [ObservableProperty]
        private bool isEmpty;

        /// <summary>Short-lived confirmation line ("X added to cart") — clears itself after a moment.</summary>
        [ObservableProperty]
        private string? toastMessage;

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var categoriesTask = _firestore.GetProductCategoriesAsync();
                var productsTask = _firestore.GetProductsAsync();
                await Task.WhenAll(categoriesTask, productsTask);

                _allProducts = productsTask.Result;

                Categories.Clear();
                Categories.Add(new SelectableTab(null, "All", isSelected: true));
                foreach (var category in categoriesTask.Result.Where(c => c.IsActive).OrderBy(c => c.Name))
                {
                    Categories.Add(new SelectableTab(category.Id, category.Name));
                }

                ApplyFilter();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load the shop. Check your connection and try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void SelectCategory(SelectableTab tab)
        {
            foreach (var category in Categories)
            {
                category.IsSelected = false;
            }
            tab.IsSelected = true;
            ApplyFilter();
        }

        [RelayCommand]
        private void ToggleSearch()
        {
            IsSearchVisible = !IsSearchVisible;
            if (!IsSearchVisible)
            {
                SearchText = string.Empty; // closing search also clears the filter, otherwise results stay mysteriously narrowed
            }
        }

        [RelayCommand]
        private async Task AddToCartAsync(Product product)
        {
            if (!product.IsInStock)
            {
                _ = ShowToastAsync("Sorry, that's out of stock.");
                return;
            }

            try
            {
                await _firestore.AddToCartAsync(product.Id, 1);
                _ = ShowToastAsync($"{product.Name} added to cart");
            }
            catch (Exception ex)
            {
                _ = ShowToastAsync("Couldn't add that to your cart.");
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        private void ApplyFilter()
        {
            var selectedId = Categories.FirstOrDefault(c => c.IsSelected)?.Id;
            var query = SearchText.Trim();

            var filtered = _allProducts.Where(p => p.IsActive);

            if (selectedId is not null)
            {
                filtered = filtered.Where(p => p.CategoryId == selectedId);
            }

            if (query.Length > 0)
            {
                filtered = filtered.Where(p =>
                    p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            Products.Clear();
            foreach (var product in filtered.OrderBy(p => p.Name))
            {
                Products.Add(product);
            }

            IsEmpty = Products.Count == 0;
        }

        private async Task ShowToastAsync(string message)
        {
            ToastMessage = message;
            await Task.Delay(2000);
            if (ToastMessage == message)
            {
                ToastMessage = null; // only clear our own message, not a newer one that replaced it
            }
        }
    }
}
