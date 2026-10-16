using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // A category chip. It knows whether it is selected so the chip can change colour.
    public partial class ServiceCategoryTab : ObservableObject
    {
        public ServiceCategoryTab(string? id, string label, bool isSelected = false)
        {
            Id = id;
            Label = label;
            this.isSelected = isSelected;
        }

        public string? Id { get; }
        public string Label { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ChipBackground))]
        [NotifyPropertyChangedFor(nameof(ChipTextColor))]
        private bool isSelected;

        public Color ChipBackground => IsSelected ? Color.FromArgb("#111827") : Colors.White;
        public Color ChipTextColor => IsSelected ? Colors.White : Color.FromArgb("#1B2A4A");
    }

    // What one row of the list shows
    public record ServiceListItem(string Id, string Name, string Description,
                                  string? ImageUrl, string PriceText, string DurationText);

    [QueryProperty(nameof(CategoryQuery), "category")]
    [QueryProperty(nameof(SearchQuery), "search")]
    public partial class ServicesViewModel : BaseViewModel
    {
        private readonly IFirestoreService _firestoreService;
        private List<Service> _allLoadedServices = new();

        public ServicesViewModel(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            Title = "Services";

            Categories = new ObservableCollection<ServiceCategoryTab>
            {
                new(null, "All", isSelected: true),
                new("nails", "Nails"),
                new("lashes", "Lashes"),
                new("press-ons", "Press-Ons"),
            };
            selectedCategory = Categories[0];
        }

        // Filled in by Shell when another page navigates here with ?category=... / ?search=...
        public string CategoryQuery
        {
            get => SelectedCategory?.Id ?? string.Empty;
            set => SelectedCategory = Categories.FirstOrDefault(c => c.Id == value) ?? Categories[0];
        }

        public string SearchQuery
        {
            get => SearchText;
            set => SearchText = value ?? string.Empty;
        }

        public ObservableCollection<ServiceCategoryTab> Categories { get; }
        public ObservableCollection<ServiceListItem> Services { get; } = new();

        [ObservableProperty]
        private ServiceCategoryTab selectedCategory;

        [ObservableProperty]
        private string searchText = string.Empty;

        partial void OnSelectedCategoryChanged(ServiceCategoryTab value)
        {
            foreach (var tab in Categories)
                tab.IsSelected = tab == value;

            ApplyFilter();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        [RelayCommand]
        private void SelectCategory(ServiceCategoryTab? tab)
        {
            if (tab is not null)
                SelectedCategory = tab;
        }

        [RelayCommand]
        private Task SelectServiceAsync(string? serviceId)
        {
            if (string.IsNullOrEmpty(serviceId)) return Task.CompletedTask;
            return Shell.Current.GoToAsync($"SelectDateTimeView?serviceId={Uri.EscapeDataString(serviceId)}");
        }

        [RelayCommand]
        private async Task LoadServicesAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                _allLoadedServices = await _firestoreService.GetServicesAsync();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load services. Please try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilter()
        {
            var filtered = _allLoadedServices.Where(s => s.IsActive);

            if (SelectedCategory?.Id is not null)
                filtered = filtered.Where(s => s.CategoryId == SelectedCategory.Id);

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var query = SearchText.Trim();
                filtered = filtered.Where(s =>
                    s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    s.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            Services.Clear();
            foreach (var s in filtered)
            {
                Services.Add(new ServiceListItem(
                    s.Id,
                    s.Name,
                    s.Description,
                    s.ImageUrl,
                    $"R{s.Price:0.00}",
                    FormatDuration(s.DurationMinutes)));
            }
        }

        private static string FormatDuration(int minutes)
        {
            if (minutes < 60) return $"{minutes} min";
            var h = minutes / 60;
            var m = minutes % 60;
            return m == 0 ? $"{h} hr" : $"{h} hr {m} min";
        }
    }
}