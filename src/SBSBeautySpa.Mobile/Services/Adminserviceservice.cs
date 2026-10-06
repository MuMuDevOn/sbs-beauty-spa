using System.Linq;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SBSBeautySpa.Mobile.Services
{
    // Everything you can edit about a service.
    // Used when creating or updating a service.
    public record ServiceInput(
        string Name,
        string Description,
        string CategoryId,
        decimal Price,
        decimal DepositAmount,
        int DurationMinutes,
        int BufferMinutes,
        string? ImageUrl,
        List<string> AddOnServiceIds
    );

    // The contract for the admin services service.
    public interface IAdminServicesService
    {
        Task<string> CreateServiceAsync(ServiceInput input, CancellationToken ct = default);
        Task UpdateServiceAsync(string serviceId, ServiceInput input, CancellationToken ct = default);
        Task SetServiceActiveAsync(string serviceId, bool isActive, CancellationToken ct = default);
        Task DeleteServiceAsync(string serviceId, CancellationToken ct = default);

        Task<string> CreateServiceCategoryAsync(string name, string? description, CancellationToken ct = default);
        Task UpdateServiceCategoryAsync(string categoryId, string? name, string? description, CancellationToken ct = default);
        Task SetServiceCategoryActiveAsync(string categoryId, bool isActive, CancellationToken ct = default);
    }

    // This class talks to the server about managing services.
    // It's the client-side partner of firebase/functions/adminServices.js.
    //
    // IMPORTANT: All validation happens on the server:
    //   - Duration must be positive
    //   - Deposit cannot be more than the price
    //   - And so on
    //
    // This class does NOT duplicate those rules. If the server rejects
    // something, we just pass the error message back to the UI to show.
    public class AdminServicesService : IAdminServicesService
    {
        private readonly HttpClient _http;
        private readonly IFirebaseAuthService _auth;

        // The base web address of your deployed Cloud Functions.
        // Replace this with your real URL after deploying.
        private const string FunctionsBaseUrl = "https://<region>-<project-id>.cloudfunctions.net";

        public AdminServicesService(HttpClient http, IFirebaseAuthService auth)
        {
            _http = http;
            _auth = auth;
        }

        // Creates a new service.
        public async Task<string> CreateServiceAsync(ServiceInput input, CancellationToken ct = default)
        {
            var payload = new { data = ToPayload(input) };
            var response = await PostCallableAsync<CreateEnvelope>("createService", payload, ct);
            return response.Result.ServiceId;
        }

        // Updates an existing service.
        public Task UpdateServiceAsync(string serviceId, ServiceInput input, CancellationToken ct = default)
        {
            var payload = new { data = MergeServiceId(serviceId, ToPayload(input)) };
            return PostCallableAsync<object>("updateService", payload, ct);
        }

        // Turns a service on or off (without deleting it).
        public Task SetServiceActiveAsync(string serviceId, bool isActive, CancellationToken ct = default) =>
            PostCallableAsync<object>("setServiceActive", new { data = new { serviceId, isActive } }, ct);

        // Deletes a service.
        public Task DeleteServiceAsync(string serviceId, CancellationToken ct = default) =>
            PostCallableAsync<object>("deleteService", new { data = new { serviceId } }, ct);

        // Creates a new service category.
        public async Task<string> CreateServiceCategoryAsync(string name, string? description, CancellationToken ct = default)
        {
            var response = await PostCallableAsync<CategoryEnvelope>("createServiceCategory", new { data = new { name, description } }, ct);
            return response.Result.CategoryId;
        }

        // Updates a service category.
        public Task UpdateServiceCategoryAsync(string categoryId, string? name, string? description, CancellationToken ct = default) =>
            PostCallableAsync<object>("updateServiceCategory", new { data = new { categoryId, name, description } }, ct);

        // Turns a service category on or off.
        public Task SetServiceCategoryActiveAsync(string categoryId, bool isActive, CancellationToken ct = default) =>
            PostCallableAsync<object>("setServiceCategoryActive", new { data = new { categoryId, isActive } }, ct);

        // Converts the ServiceInput record into the JSON shape the server expects.
        private static object ToPayload(ServiceInput input) => new
        {
            name = input.Name,
            description = input.Description,
            categoryId = input.CategoryId,
            price = input.Price,
            depositAmount = input.DepositAmount,
            durationMinutes = input.DurationMinutes,
            bufferMinutes = input.BufferMinutes,
            imageUrl = input.ImageUrl,
            addOnServiceIds = input.AddOnServiceIds,
        };

        // Adds the serviceId to the payload for update calls.
        private static object MergeServiceId(string serviceId, object payload)
        {
            // Flatten {serviceId, ...payload} into one flat JSON object.
            var dict = System.Text.Json.JsonSerializer.SerializeToElement(payload)
                .EnumerateObject()
                .ToDictionary(p => p.Name, p => (object?)p.Value);
            dict["serviceId"] = serviceId;
            return dict;
        }

        // This helper method sends a POST request to the server.
        // Same pattern as every other service in this app.
        private async Task<TEnvelope> PostCallableAsync<TEnvelope>(string functionName, object payload, CancellationToken ct)
        {
            // Get the user's login token so the server knows who is calling.
            var idToken = await _auth.GetIdTokenAsync();

            // Build the HTTP request.
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{FunctionsBaseUrl}/{functionName}")
            {
                Content = JsonContent.Create(payload),
            };

            // If we have a token, attach it to the request.
            if (!string.IsNullOrEmpty(idToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", idToken);
            }

            // Send the request.
            using var response = await _http.SendAsync(request, ct);

            // If the server returned an error, extract a readable message
            // and throw it.
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"{functionName} failed: {ExtractErrorMessage(errorBody)}");
            }

            // Read the JSON response and turn it into the expected object.
            var body = await response.Content.ReadFromJsonAsync<TEnvelope>(cancellationToken: ct);
            return body ?? throw new InvalidOperationException($"{functionName} returned an empty response.");
        }

        // Pulls a readable error message out of the server's response.
        // Firebase sends errors like { "error": { "message": "...", "status": "..." } }.
        private static string ExtractErrorMessage(string rawBody)
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
                if (doc.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var message))
                {
                    return message.GetString() ?? rawBody;
                }
            }
            catch (System.Text.Json.JsonException)
            {
                // Not JSON, or not the shape we expected — fall through to raw body.
            }
            return rawBody;
        }

        // These small classes match the shape of the JSON the server sends back.

        // The response when creating a service.
        private class CreateEnvelope
        {
            [JsonPropertyName("result")]
            public CreatePayload Result { get; set; } = new();
        }

        // The new service's ID inside that response.
        private class CreatePayload
        {
            [JsonPropertyName("serviceId")]
            public string ServiceId { get; set; } = string.Empty;
        }

        // The response when creating a category.
        private class CategoryEnvelope
        {
            [JsonPropertyName("result")]
            public CategoryPayload Result { get; set; } = new();
        }

        // The new category's ID inside that response.
        private class CategoryPayload
        {
            [JsonPropertyName("categoryId")]
            public string CategoryId { get; set; } = string.Empty;
        }
    }
}