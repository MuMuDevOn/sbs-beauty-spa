using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SBSBeautySpa.Mobile.Services
{
    // What kind of payment the app is asking for.
    public enum PaymentRequestType
    {
        Deposit,   // The upfront payment to confirm a booking
        Balance,   // The remaining amount owed after the deposit
        Full       // The full amount paid at once
    }

    // What the server sends back when starting a payment.
    // AuthorizationUrl = the payment page to open
    // Reference = our tracking code for this payment
    public record PaymentInitResult(string AuthorizationUrl, string Reference);

    // What the server sends back when verifying a payment.
    // Status = "success", "failed", or "pending"
    // BookingId = the booking this payment is for
    public record PaymentVerifyResult(string Status, string BookingId)
    {
        // Helper: true when the status is "success".
        public bool Succeeded => Status == "success";
    }

    // The contract for any payment service.
    public interface IPaymentService
    {
        Task<PaymentInitResult> InitializePaymentAsync(string bookingId, PaymentRequestType type, CancellationToken ct = default);
        Task<PaymentVerifyResult> VerifyPaymentAsync(string reference, CancellationToken ct = default);
    }

    // This class talks to the server about payments.
    //
    // It works the same way as AvailabilityService — a thin wrapper
    // around two Cloud Functions (see firebase/functions/payments.js).
    //
    // This class NEVER:
    //   - sees the Paystack secret key
    //   - calls api.paystack.co directly
    //   - writes to Firestore
    //
    // What it DOES:
    //   - Hands the app a payment URL to open
    //   - After the payment page closes, asks the server if the payment
    //     actually went through
    public class PaymentService : IPaymentService
    {
        private readonly HttpClient _http;
        private readonly IFirebaseAuthService _auth;

        // The base web address of your deployed Cloud Functions.
        // Same as the one used in AvailabilityService.
        // Example: https://us-central1-sbsbeautyspa.cloudfunctions.net
        // You must replace this with your real URL after deploying.
        private const string FunctionsBaseUrl = "https://<region>-<project-id>.cloudfunctions.net";

        public PaymentService(HttpClient http, IFirebaseAuthService auth)
        {
            _http = http;
            _auth = auth;
        }

        // Asks the server to start a payment.
        // Returns the URL to open plus a reference.
        public async Task<PaymentInitResult> InitializePaymentAsync(string bookingId, PaymentRequestType type, CancellationToken ct = default)
        {
            var payload = new
            {
                data = new
                {
                    bookingId,
                    type = type.ToString().ToLowerInvariant(),
                }
            };

            var response = await PostCallableAsync<InitEnvelope>("initializePayment", payload, ct);
            return new PaymentInitResult(response.Result.AuthorizationUrl, response.Result.Reference);
        }

        // Asks the server to check if a payment succeeded.
        // Called right after the payment page closes.
        public async Task<PaymentVerifyResult> VerifyPaymentAsync(string reference, CancellationToken ct = default)
        {
            var payload = new { data = new { reference } };
            var response = await PostCallableAsync<VerifyEnvelope>("verifyPayment", payload, ct);
            return new PaymentVerifyResult(response.Result.Status, response.Result.BookingId);
        }

        // This helper method sends a POST request to the server and reads the response.
        // Same pattern as AvailabilityService — one place to write the logic.
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

            // Send the request and wait for the response.
            using var response = await _http.SendAsync(request, ct);

            // Throw an error if the server returned a failure status code.
            response.EnsureSuccessStatusCode();

            // Read the JSON response and turn it into the expected object.
            var body = await response.Content.ReadFromJsonAsync<TEnvelope>(cancellationToken: ct);
            return body ?? throw new InvalidOperationException($"{functionName} returned an empty response.");
        }

        // These small classes match the shape of the JSON the server sends back.
        // They are called DTOs (Data Transfer Objects) — they just hold data.

        // The full response when starting a payment.
        private class InitEnvelope
        {
            [JsonPropertyName("result")]
            public InitPayload Result { get; set; } = new();
        }

        // The data inside that response.
        private class InitPayload
        {
            [JsonPropertyName("authorizationUrl")]
            public string AuthorizationUrl { get; set; } = string.Empty;

            [JsonPropertyName("reference")]
            public string Reference { get; set; } = string.Empty;
        }

        // The full response when verifying a payment.
        private class VerifyEnvelope
        {
            [JsonPropertyName("result")]
            public VerifyPayload Result { get; set; } = new();
        }

        // The data inside that response.
        private class VerifyPayload
        {
            [JsonPropertyName("status")]
            public string Status { get; set; } = string.Empty;

            [JsonPropertyName("bookingId")]
            public string BookingId { get; set; } = string.Empty;
        }
    }
}