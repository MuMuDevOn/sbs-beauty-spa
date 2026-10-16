using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // A category circle. It turns dark with a white icon when its filter is active.
    public partial class HomeCategory : ObservableObject
    {
        private readonly string _iconName;

        public HomeCategory(string id, string label, string iconName)
        {
            Id = id;
            Label = label;
            _iconName = iconName;
        }

        public string Id { get; }
        public string Label { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Background), nameof(Icon))]
        private bool isSelected;

        public Color Background => IsSelected ? Color.FromArgb("#111827") : Colors.White;
        public string Icon => IsSelected ? $"{_iconName}_active.png" : $"{_iconName}.png";
    }

    // Small display models so the XAML stays simple
    public record HomeServiceCard(string Id, string Name, string? ImageUrl,
                                  string PriceLabel, string PriceSubtitle, string DurationSubtitle);
    public record HomeProductCard(string Id, string Name, string? ImageUrl, string Subtitle);

    // One tappable option inside the filter panel (Show / Category / Price / Sort)
    public partial class FilterChip : ObservableObject
    {
        public FilterChip(string group, string key, string label, bool isSelected = false)
        {
            Group = group;
            Key = key;
            Label = label;
            this.isSelected = isSelected;
        }

        public string Group { get; }
        public string Key { get; }
        public string Label { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ChipBackground), nameof(ChipTextColor))]
        private bool isSelected;

        public Color ChipBackground => IsSelected ? Color.FromArgb("#111827") : Colors.White;
        public Color ChipTextColor => IsSelected ? Colors.White : Color.FromArgb("#1B2A4A");
    }

    public partial class HomeViewModel : BaseViewModel
    {
        private readonly IFirestoreService _firestore;

        // Everything loaded from Firestore (filters run over these full lists)
        private List<Service> _allServices = new();
        private List<Product> _allProducts = new();

        // category id -> name, and name -> all ids with that name (service + product categories)
        private readonly Dictionary<string, string> _categoryNames = new();
        private readonly Dictionary<string, HashSet<string>> _categoryIdsByName =
            new(StringComparer.OrdinalIgnoreCase);

        public HomeViewModel(IFirestoreService firestore)
        {
            _firestore = firestore;
            Title = "Home";
        }

        // ---------- Quick category circles ----------
        public ObservableCollection<HomeCategory> Categories { get; } = new()
        {
            new("nails",     "Nails",     "cat_nails"),
            new("lashes",    "Lashes",    "cat_lashes"),
            new("press-ons", "Press-Ons", "cat_pressons"),
            new("gel-x",     "Gel X",     "cat_gelx"),
            new("shop",      "Shop",      "cat_shop"),
        };

        // ---------- Default Home sections ----------
        public ObservableCollection<HomeServiceCard> TrendingServices { get; } = new();
        public ObservableCollection<HomeProductCard> NewArrivals { get; } = new();
        public ObservableCollection<HomeServiceCard> PopularServices { get; } = new();

        // ---------- Search / filter results (all matching items) ----------
        public ObservableCollection<HomeServiceCard> ResultServices { get; } = new();
        public ObservableCollection<HomeProductCard> ResultProducts { get; } = new();

        // ---------- Filter panel options ----------
        public ObservableCollection<FilterChip> TypeChips { get; } = new()
        {
            new("type", "all", "All", true),
            new("type", "services", "Services"),
            new("type", "products", "Products"),
        };

        // Filled in after load from your service + product categories
        public ObservableCollection<FilterChip> CategoryChips { get; } = new()
        {
            new("category", "", "All", true),
        };

        public ObservableCollection<FilterChip> PriceChips { get; } = new()
        {
            new("price", "any", "Any price", true),
            new("price", "u200", "Under R200"),
            new("price", "200-500", "R200 – R500"),
            new("price", "500+", "Over R500"),
        };

        public ObservableCollection<FilterChip> SortChips { get; } = new()
        {
            new("sort", "default", "Default", true),
            new("sort", "low", "Price: low to high"),
            new("sort", "high", "Price: high to low"),
        };

        // ---------- Bindable state ----------
        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private bool isFilterPanelOpen;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotFiltering))]
        private bool isFiltering;

        public bool IsNotFiltering => !IsFiltering;

        [ObservableProperty]
        private string filterButtonText = "Filters";

        [ObservableProperty]
        private string resultSummary = string.Empty;

        [ObservableProperty]
        private bool hasServiceResults;

        [ObservableProperty]
        private bool hasProductResults;

        [ObservableProperty]
        private bool hasNoResults;

        partial void OnSearchTextChanged(string value) => ApplyFilters();

        // ---------- Loading ----------
        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var servicesTask = _firestore.GetServicesAsync();
                var productsTask = _firestore.GetProductsAsync();
                var serviceCatsTask = _firestore.GetServiceCategoriesAsync();
                var productCatsTask = _firestore.GetProductCategoriesAsync();

                _allServices = (await servicesTask).Where(s => s.IsActive).ToList();
                _allProducts = (await productsTask).Where(p => p.IsActive).ToList();
                BuildCategoryLookups(await serviceCatsTask, await productCatsTask);

                Replace(TrendingServices, _allServices.Take(4).Select(ToServiceCard));
                Replace(PopularServices, _allServices.Take(3).Select(ToServiceCard));
                Replace(NewArrivals, _allProducts.Take(2).Select(p => ToProductCard(p, "New")));

                // Re-run in case the user already typed something while loading
                ApplyFilters();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load the latest content. Please try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ---------- Filter panel ----------
        [RelayCommand]
        private void ToggleFilterPanel() => IsFilterPanelOpen = !IsFilterPanelOpen;

        [RelayCommand]
        private void SelectFilterChip(FilterChip? chip)
        {
            if (chip is null) return;

            foreach (var c in GroupOf(chip.Group))
                c.IsSelected = c == chip;

            ApplyFilters();
        }

        [RelayCommand]
        private void ClearFilters()
        {
            ResetGroup(TypeChips);
            ResetGroup(CategoryChips);
            ResetGroup(PriceChips);
            ResetGroup(SortChips);

            SearchText = string.Empty; // also re-applies when it changes
            ApplyFilters();
        }

        // ---------- The filtering itself ----------
        private void ApplyFilters()
        {
            var type = SelectedKey(TypeChips);
            var categoryKey = SelectedKey(CategoryChips);
            var priceKey = SelectedKey(PriceChips);
            var sortKey = SelectedKey(SortChips);
            var query = SearchText?.Trim() ?? string.Empty;

            var activeFilters = 0;
            if (type != "all") activeFilters++;
            if (categoryKey.Length > 0) activeFilters++;
            if (priceKey != "any") activeFilters++;
            if (sortKey != "default") activeFilters++;

            FilterButtonText = activeFilters == 0 ? "Filters" : $"Filters ({activeFilters})";
            IsFiltering = query.Length > 0 || activeFilters > 0;
            UpdateCategoryCircles(categoryKey, query);

            if (!IsFiltering)
            {
                ResultServices.Clear();
                ResultProducts.Clear();
                HasServiceResults = HasProductResults = HasNoResults = false;
                ResultSummary = string.Empty;
                return;
            }

            IEnumerable<Service> services = type == "products" ? Enumerable.Empty<Service>() : _allServices;
            IEnumerable<Product> products = type == "services" ? Enumerable.Empty<Product>() : _allProducts;

            // Text search: name, description or category name
            if (query.Length > 0)
            {
                services = services.Where(s =>
                    Has(s.Name, query) || Has(s.Description, query) || Has(CategoryName(s.CategoryId, ""), query));
                products = products.Where(p =>
                    Has(p.Name, query) || Has(p.Description, query) || Has(CategoryName(p.CategoryId, ""), query));
            }

            // Category (matched by name across service + product categories)
            if (categoryKey.Length > 0)
            {
                _categoryIdsByName.TryGetValue(categoryKey, out var ids);
                ids ??= new HashSet<string>();
                services = services.Where(s => ids.Contains(s.CategoryId));
                products = products.Where(p => ids.Contains(p.CategoryId));
            }

            // Price range
            Func<decimal, bool> inRange = priceKey switch
            {
                "u200" => p => p < 200,
                "200-500" => p => p >= 200 && p <= 500,
                "500+" => p => p > 500,
                _ => _ => true
            };
            services = services.Where(s => inRange(s.Price));
            products = products.Where(p => inRange(p.Price));

            // Sort
            services = sortKey switch
            {
                "low" => services.OrderBy(s => s.Price),
                "high" => services.OrderByDescending(s => s.Price),
                _ => services
            };
            products = sortKey switch
            {
                "low" => products.OrderBy(p => p.Price),
                "high" => products.OrderByDescending(p => p.Price),
                _ => products
            };

            var serviceCards = services.Select(ToServiceCard).ToList();
            var productCards = products.Select(p => ToProductCard(p)).ToList();

            Replace(ResultServices, serviceCards);
            Replace(ResultProducts, productCards);

            HasServiceResults = serviceCards.Count > 0;
            HasProductResults = productCards.Count > 0;

            var total = serviceCards.Count + productCards.Count;
            HasNoResults = total == 0;
            ResultSummary = total == 1 ? "1 result" : $"{total} results";
        }

        // ---------- "View all" links ----------
        [RelayCommand]
        private Task ViewAllTrendingAsync() => Shell.Current.GoToAsync("ServicesPage");

        [RelayCommand]
        private Task ViewAllNewArrivalsAsync() => Shell.Current.GoToAsync("//ShopView");

        [RelayCommand]
        private Task ViewAllPopularAsync() => Shell.Current.GoToAsync("ServicesPage");

        // ---------- Other taps ----------
        [RelayCommand]
        private Task OpenCategoryAsync(string? categoryId)
        {
            // "Shop" is a tab, so it uses an absolute route
            if (categoryId == "shop")
                return Shell.Current.GoToAsync("//ShopView");

            var circle = Categories.FirstOrDefault(c => c.Id == categoryId);
            if (circle is null) return Task.CompletedTask;

            // Tapping the active circle again turns the filter off
            if (circle.IsSelected)
            {
                ClearFilters();
                return Task.CompletedTask;
            }

            ApplyQuickFilter(categoryId == "gel-x" ? "Gel" : circle.Label);
            return Task.CompletedTask;
        }

        [RelayCommand]
        private Task ShopNowAsync() => Shell.Current.GoToAsync("//ShopView");

        [RelayCommand]
        private Task OpenServiceAsync(string? serviceId)
        {
            if (string.IsNullOrEmpty(serviceId)) return Task.CompletedTask;
            return Shell.Current.GoToAsync($"SelectDateTimeView?serviceId={Uri.EscapeDataString(serviceId)}");
        }

        [RelayCommand]
        private Task OpenProductAsync() => Shell.Current.GoToAsync("//ShopView");

        [RelayCommand]
        private Task OpenNotificationsAsync() =>
            Shell.Current.DisplayAlertAsync("Notifications", "Notifications are coming soon.", "OK");

        // ---------- Helpers ----------

        // A circle tap: use the matching category chip if there is one, otherwise search for the word
        private void ApplyQuickFilter(string term)
        {
            var chip = CategoryChips.FirstOrDefault(c => c.Key.Length > 0 && Normalize(c.Key) == Normalize(term));

            if (chip is not null)
            {
                SearchText = string.Empty;
                SelectFilterChip(chip);
            }
            else
            {
                ResetGroup(CategoryChips);
                SearchText = term;
                ApplyFilters();
            }
        }

        // Highlights the circle that matches the active category filter or search word
        private void UpdateCategoryCircles(string categoryKey, string query)
        {
            foreach (var c in Categories)
            {
                var term = c.Id == "gel-x" ? "Gel" : c.Label;
                c.IsSelected = c.Id != "shop" &&
                    ((categoryKey.Length > 0 && Normalize(categoryKey) == Normalize(term)) ||
                     (query.Length > 0 && Normalize(query) == Normalize(term)));
            }
        }

        private void BuildCategoryLookups(IEnumerable<ServiceCategory> serviceCats, IEnumerable<ProductCategory> productCats)
        {
            _categoryNames.Clear();
            _categoryIdsByName.Clear();

            void Add(string id, string name)
            {
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return;
                _categoryNames[id] = name;
                if (!_categoryIdsByName.TryGetValue(name, out var ids))
                    _categoryIdsByName[name] = ids = new HashSet<string>();
                ids.Add(id);
            }

            foreach (var c in serviceCats) Add(c.Id, c.Name);
            foreach (var c in productCats) Add(c.Id, c.Name);

            // Rebuild the category chips, keeping the current selection if it still exists
            var selected = SelectedKey(CategoryChips);
            CategoryChips.Clear();
            CategoryChips.Add(new FilterChip("category", "", "All", selected.Length == 0));
            foreach (var name in _categoryIdsByName.Keys.OrderBy(n => n))
                CategoryChips.Add(new FilterChip("category", name, name,
                    name.Equals(selected, StringComparison.OrdinalIgnoreCase)));

            if (!CategoryChips.Any(c => c.IsSelected))
                CategoryChips[0].IsSelected = true;
        }

        private HomeServiceCard ToServiceCard(Service s)
        {
            var category = CategoryName(s.CategoryId, "Service");
            return new HomeServiceCard(
                s.Id,
                s.Name,
                s.ImageUrl,
                $"R{s.Price:0.00}",
                $"{category} • R{s.Price:0.00}",
                $"{category} • {s.DurationMinutes} min");
        }

        private HomeProductCard ToProductCard(Product p, string? prefix = null) =>
            new(p.Id, p.Name, p.ImageUrl, $"{prefix ?? CategoryName(p.CategoryId, "Product")} • R{p.Price:0.00}");

        private string CategoryName(string categoryId, string fallback) =>
            _categoryNames.TryGetValue(categoryId, out var name) ? name : fallback;

        private ObservableCollection<FilterChip> GroupOf(string group) => group switch
        {
            "type" => TypeChips,
            "category" => CategoryChips,
            "price" => PriceChips,
            _ => SortChips
        };

        private static string SelectedKey(IEnumerable<FilterChip> chips) =>
            chips.FirstOrDefault(c => c.IsSelected)?.Key ?? string.Empty;

        private static void ResetGroup(IList<FilterChip> chips)
        {
            for (var i = 0; i < chips.Count; i++)
                chips[i].IsSelected = i == 0;
        }

        private static bool Has(string? text, string query) =>
            text?.Contains(query, StringComparison.OrdinalIgnoreCase) == true;

        private static string Normalize(string s) =>
            s.Replace("-", "").Replace(" ", "").ToLowerInvariant();

        private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
        {
            target.Clear();
            foreach (var item in items) target.Add(item);
        }
    }
}