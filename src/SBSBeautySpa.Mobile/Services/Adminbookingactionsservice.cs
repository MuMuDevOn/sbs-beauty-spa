using System.Net.Http.Json;
using System.Text.Json.Serialization;
using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    // The contract for any service that changes booking statuses.
    public interface IAdminBookingActionsService
    {
        // Changes a booking's status (confirmed, completed, or cancelled).
        //
        // IMPORTANT: The server decides which changes are actually allowed.
        // (See adminBookings.js — it has a list of valid transitions.)
        //
        // This method does NOT check the rules on the client side first.
        // It just asks the server. If the change isn't allowed, the server
        // rejects it and we show the real error.
        //
        // Why? Because the local list might be out of date. The server
        // always knows the truth.
        Task UpdateBookingStatusAsync(string bookingId, BookingStatus newStatus, string? cancellationReason = null, CancellationToken ct = default);
    }

    // This class talks to the server to change booking statuses.
    public class AdminBookingActionsService : IAdminBookingActionsService
    {
        private readonly HttpClient _http;
        private readonly IFirebaseAuthService _auth;

        // The base web address of your deployed Cloud Functions.
        // Replace this with your real URL after deploying.
        private const string FunctionsBaseUrl = "https://<region>-<project-id>.cloudfunctions.net";

        public AdminBookingActionsService(HttpClient http, IFirebaseAuthService auth)
        {
            _http = http;
            _auth = auth;
        }

        // Asks the server to change a booking's status.
        public Task UpdateBookingStatusAsync(string bookingId, BookingStatus newStatus, string? cancellationReason = null, CancellationToken ct = default)
        {
            var payload = new
            {
                data = new
                {
                    bookingId,
                    newStatus = newStatus.ToString().ToLowerInvariant(),
                    cancellationReason,
                }
            };
            return PostCallableAsync<object>("updateBookingStatus", payload, ct);
        }

        // This helper method sends a POST request to the server.
        // Same pattern as every other service — one place to write the logic.
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
            // and throw it. This way the UI can show the real reason.
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"{functionName} failed: {ExtractErrorMessage(errorBody)}");
            }

            // Read the JSON response and turn it into the expected object.
            var body = await response.Content.ReadFromJsonAsync<TEnvelope>(cancellationToken: ct);
            return body ?? throw new InvalidOperationException($"{functionName} returned an empty response.");
        }

        // This method pulls a readable error message out of the server's response.
        //
        // Firebase sends errors like this:
        //   { "error": { "message": "...", "status": "..." } }
        //
        // If that shape isn't there, it just returns the raw response body.
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
                // Not JSON, or not the shape we expected — just use the raw body.
            }
            return rawBody;
        }
    }
}