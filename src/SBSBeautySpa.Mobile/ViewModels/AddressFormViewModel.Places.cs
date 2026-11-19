using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // The address search part of the address form (kept in its own file).
    public partial class AddressFormViewModel
    {
        private PlacesService? _places;
        private CancellationTokenSource? _searchCts;
        private bool _suppressSearch;
        private string _sessionToken = NewToken();

        private static string NewToken() => Guid.NewGuid().ToString("N");

        private PlacesService? Places =>
            _places ??= IPlatformApplication.Current?.Services.GetService<PlacesService>();

        public ObservableCollection<PlaceSuggestion> Suggestions { get; } = new();

        [ObservableProperty] private string addressSearch = string.Empty;
        [ObservableProperty] private bool hasSuggestions;
        [ObservableProperty] private string? searchMessage;

        // Runs every time the client types in the search box.
        partial void OnAddressSearchChanged(string value)
        {
            if (_suppressSearch) return;
            _ = SearchAsync(value);
        }

        private async Task SearchAsync(string text)
        {
            _searchCts?.Cancel();
            var cts = new CancellationTokenSource();
            _searchCts = cts;

            text = (text ?? string.Empty).Trim();
            SearchMessage = null;

            if (text.Length < 3)
            {
                Suggestions.Clear();
                HasSuggestions = false;
                return;
            }

            try
            {
                await Task.Delay(350, cts.Token);   // wait until they pause typing

                var places = Places;
                if (places is null) return;

                var found = await places.AutocompleteAsync(text, _sessionToken, cts.Token);
                if (cts.IsCancellationRequested) return;

                Suggestions.Clear();
                foreach (var s in found) Suggestions.Add(s);
                HasSuggestions = Suggestions.Count > 0;
                if (!HasSuggestions) SearchMessage = "No matching addresses. You can type it in below.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (cts.IsCancellationRequested) return;
                Suggestions.Clear();
                HasSuggestions = false;
                SearchMessage = "Address search isn't available right now. Please type the address below.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        // The client tapped one of the suggestions: fill the boxes below.
        [RelayCommand]
        private async Task SelectSuggestionAsync(PlaceSuggestion? suggestion)
        {
            var places = Places;
            if (suggestion is null || places is null) return;

            _searchCts?.Cancel();
            ErrorMessage = null;
            SearchMessage = null;
            IsBusy = true;

            try
            {
                var a = await places.DetailsAsync(suggestion.PlaceId, _sessionToken);

                if (!string.IsNullOrWhiteSpace(a.StreetAddress)) StreetAddress = a.StreetAddress;
                if (!string.IsNullOrWhiteSpace(a.Suburb)) Suburb = a.Suburb;
                if (!string.IsNullOrWhiteSpace(a.City)) City = a.City;
                if (!string.IsNullOrWhiteSpace(a.PostalCode)) PostalCode = a.PostalCode;

                var province = Provinces.FirstOrDefault(p => string.Equals(p, a.Province, StringComparison.OrdinalIgnoreCase));
                if (province is not null) SelectedProvince = province;

                _suppressSearch = true;
                AddressSearch = string.IsNullOrWhiteSpace(a.FormattedAddress) ? suggestion.Description : a.FormattedAddress;
                _suppressSearch = false;

                Suggestions.Clear();
                HasSuggestions = false;

                // A finished search is one Google "session"; the next search starts a new one.
                _sessionToken = NewToken();

                if (string.IsNullOrWhiteSpace(a.StreetAddress))
                    SearchMessage = "Please add the street number and street name below.";
            }
            catch (Exception ex)
            {
                _suppressSearch = false;
                SearchMessage = "Couldn't load that address. Please try another or type it below.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
