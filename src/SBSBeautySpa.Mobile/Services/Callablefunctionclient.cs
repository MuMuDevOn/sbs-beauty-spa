using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SBSBeautySpa.Mobile.Services
{
    // This class is the shared helper for calling Firebase Cloud Functions.
    //
    // What it does for every call:
    //   - Attaches the user's login token
    //   - Sends the { data: ... } envelope the server expects
    //   - Unwraps the { result: ... } on success
    //   - Extracts the real error message on failure (not just an HTTP code)
    //
    // NOTE: Some older services (like AvailabilityService and PaymentService)
    // have their own private copy of this same logic. They still work, but
    // they should eventually be moved to use this shared client.
    public class CallableFunctionClient
    {
        private readonly HttpClient _http;
        private readonly IFirebaseAuthService _auth;

        // The base web address of your deployed Cloud Functions.
        // Replace this with your real URL after deploying.
        // This is the ONE place it should live.
        public const string FunctionsBaseUrl = "https://<region>-<project-id>.cloudfunctions.net";

        // JSON settings used when reading the server's response.
        //
        // PropertyNameCaseInsensitive: means the server can send "noteId"
        // and C# will match it to "NoteId" automatically.
        //
        // JsonStringEnumConverter: handles converting string enums like
        // "pending" into C# enum values automatically.
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
        };

        public CallableFunctionClient(HttpClient http, IFirebaseAuthService auth)
        {
            _http = http;
            _auth = auth;
        }

        // Calls a Cloud Function and returns the result.
        // Use this when you need the response data.
        public async Task<TResult> CallAsync<TResult>(string functionName, object data, CancellationToken ct = default)
        {
            var envelope = await CallRawAsync<ResultEnvelope<TResult>>(functionName, data, ct);
            return envelope.Result;
        }

        // Calls a Cloud Function when you don't need the result.
        // Use this for "fire and forget" writes.
        public Task CallAsync(string functionName, object data, CancellationToken ct = default) =>
            CallRawAsync<object>(functionName, data, ct);

        // The actual work: sends the request, gets the response.
        private async Task<T> CallRawAsync<T>(string functionName, object data, CancellationToken ct)
        {
            // Get the user's login token.
            var idToken = await _auth.GetIdTokenAsync();

            // Build the HTTP request.
            // The { data } envelope is what Firebase callable functions expect.
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{FunctionsBaseUrl}/{functionName}")
            {
                Content = JsonContent.Create(new { data }),
            };

            // Attach the token if we have one.
            if (!string.IsNullOrEmpty(idToken))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", idToken);
            }

            // Send the request.
            using var response = await _http.SendAsync(request, ct);

            // If the server returned an error, extract the message and throw.
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"{functionName} failed: {ExtractErrorMessage(errorBody)}");
            }

            // Read the JSON response.
            var body = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
            return body ?? throw new InvalidOperationException($"{functionName} returned an empty response.");
        }

        // Pulls the readable error message out of Firebase's error response.
        // Firebase sends: { "error": { "message": "...", "status": "..." } }
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

        // The envelope Firebase wraps results in: { result: ... }
        private class ResultEnvelope<T>
        {
            public T Result { get; set; } = default!;
        }
    }
}