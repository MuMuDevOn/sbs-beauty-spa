using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // Represents one tab at the top of the Services screen.
    // Id = the category ID (null means "All")
    // Label = what the tab says (like "Nails" or "Lashes")
    public record ServiceCategoryTab(string? Id, string Label);

    // This class controls the "Services" screen.
    // It handles:
    //   - The category tabs at the top
    //   - The search box
    //   - The list of services shown below
    //   - Tapping a service to view its details
    public partial class ServicesViewModel : BaseViewModel
    {
        private readonly IFirestoreService _firestoreService;

        // We keep all loaded services in memory so we can filter them
        // without asking the server every time.
        private List<Service> _allLoadedServices = new();

        public ServicesViewModel(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            Title = "Services";

            // Set up the category tabs.
            // Update these to match your actual service categories.
            Categories = new ObservableCollection<ServiceCategoryTab>
            {
                new(null, "All"),
                new("nails", "Nails"),
                new("lashes", "Lashes"),
                new("press-ons", "Press-Ons"),
            };
            selectedCategory = Categories[0];
        }

        // The list of tabs shown at the top.
        public ObservableCollection<ServiceCategoryTab> Categories { get; }

        // The list of services shown in the list below.
        public ObservableCollection<Service> Services { get; } = new();

        // The currently selected tab.
        [ObservableProperty]
        private ServiceCategoryTab selectedCategory;

        // What the user typed in the search box.
        [ObservableProperty]
        private string searchText = string.Empty;

        // When the user taps a different tab, re-filter the list.
        partial void OnSelectedCategoryChanged(ServiceCategoryTab value) => ApplyFilter();

        // When the user types in the search box, re-filter the list.
        partial void OnSearchTextChanged(string value) => ApplyFilter();

        // Loads all active services from the server.
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
                // Load the full catalog once, then filter on the device.
                // This is faster than asking the server every time the user
                // taps a tab or types a letter in the search box.
                _allLoadedServices = await _firestoreService.GetServicesAsync();
                ApplyFilter();
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

        // Filters the loaded services based on the selected tab and search text.
        private void ApplyFilter()
        {
            // Start with only active services.
            var filtered = _allLoadedServices.Where(s => s.IsActive);

            // If a specific category is selected (not "All"), filter by it.
            if (SelectedCategory?.Id is not null)
            {
                filtered = filtered.Where(s => s.CategoryId == SelectedCategory.Id);
            }

            // If the search box has text, filter by name or description.
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var query = SearchText.Trim();
                filtered = filtered.Where(s =>
                    s.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    s.Description.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            // Replace the visible list with the filtered results.
            Services.Clear();
            foreach (var service in filtered)
            {
                Services.Add(service);
            }
        }
    }
}