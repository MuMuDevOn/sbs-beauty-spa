namespace SBSBeautySpa.Mobile.Services
{
    // One suggestion in the address search list.
    public class PlaceSuggestion
    {
        public string PlaceId { get; set; } = "";
        public string MainText { get; set; } = "";
        public string SecondaryText { get; set; } = "";
        public string Description { get; set; } = "";
    }

    // The full address behind a chosen suggestion, split into the form's boxes.
    public class PlaceAddress
    {
        public string StreetAddress { get; set; } = "";
        public string Suburb { get; set; } = "";
        public string City { get; set; } = "";
        public string Province { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public string FormattedAddress { get; set; } = "";
    }

    // Address search (Google Places) through our own server, so the Google key stays secret.
    public class PlacesService
    {
        private readonly CallableFunctionClient _client;

        public PlacesService(CallableFunctionClient client) => _client = client;

        public async Task<List<PlaceSuggestion>> AutocompleteAsync(string input, string sessionToken, CancellationToken ct = default)
        {
            var r = await _client.CallAsync<AutocompleteResult>("placesAutocomplete", new { input, sessionToken }, ct);
            return r.Suggestions;
        }

        public Task<PlaceAddress> DetailsAsync(string placeId, string sessionToken, CancellationToken ct = default) =>
            _client.CallAsync<PlaceAddress>("placesDetails", new { placeId, sessionToken }, ct);

        private class AutocompleteResult
        {
            public List<PlaceSuggestion> Suggestions { get; set; } = new();
        }
    }
}
