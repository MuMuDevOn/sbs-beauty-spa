using System.Linq;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;
using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    // This interface says what any availability service must be able to do.
    // Think of it like a contract: "If you say you're an IAvailabilityService,
    // you must have these two methods."
    public interface IAvailabilityService
    {
        // Asks the server which time slots are free on a given date.
        // serviceIds = list of services (first one is the main service, others are add-ons).
        // The total appointment length is the sum of all their durations.
        Task<List<AvailabilitySlot>> GetAvailableSlotsAsync(IEnumerable<string> serviceIds, LocalDate date, CancellationToken ct = default);

        // Asks the server to actually book a slot.
        // Same serviceIds rule: the first one is the main service.
        // Returns the new booking's ID.
        Task<string> CreateBookingAsync(IEnumerable<string> serviceIds, LocalDate date, LocalTime startTime, string? notes, CancellationToken ct = default);
    }

    // This class talks to the server about time slots.
    //
    // Important rule this class follows:
    // The SERVER is the only one who knows what's truly free right now.
    // This class NEVER:
    //   - keeps its own list of slots
    //   - guesses availability using the phone's clock
    //   - talks directly to the database
    // It only asks the server and converts the answer into objects the app can use.
    public class AvailabilityService : IAvailabilityService
    {
        private readonly HttpClient _http;
        private readonly IFirebaseAuthService _auth;

        // Used to read and write times like "09:30".
        private static readonly LocalTimePattern TimePattern = LocalTimePattern.CreateWithInvariantCulture("HH:mm");

        // The base web address of your deployed Cloud Functions.
        // Example: https://us-central1-sbsbeautyspa.cloudfunctions.net
        // You must replace this with your real URL after deploying.
        // Later, move this to a config file instead of hardcoding it.
        private const string FunctionsBaseUrl = "https://<region>-<project-id>.cloudfunctions.net";

        public AvailabilityService(HttpClient http, IFirebaseAuthService auth)
        {
            _http = http;
            _auth = auth;
        }

        // Asks the server: "What slots are free on this date for these services?"
        public async Task<List<AvailabilitySlot>> GetAvailableSlotsAsync(IEnumerable<string> serviceIds, LocalDate date, CancellationToken ct = default)
        {
            // Build the request body the server expects.
            var payload = new
            {
                data = new
                {
                    serviceIds = serviceIds.ToArray(),
                    date = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                }
            };

            // Send the request and get the response back.
            var response = await PostCallableAsync<GetSlotsResult>("getAvailableSlots", payload, ct);

            // Convert the server's response into a list of AvailabilitySlot objects.
            return response.Result.Slots
                .Select(s => new AvailabilitySlot
                {
                    Date = date,
                    StartTime = TimePattern.Parse(s.StartTime).Value,
                    EndTime = TimePattern.Parse(s.EndTime).Value,
                })
                .ToList();
        }

        // Asks the server: "Please book this time for me."
        // Returns the new booking's ID.
        public async Task<string> CreateBookingAsync(IEnumerable<string> serviceIds, LocalDate date, LocalTime startTime, string? notes, CancellationToken ct = default)
        {
            // Build the request body the server expects.
            var payload = new
            {
                data = new
                {
                    serviceIds = serviceIds.ToArray(),
                    date = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    startTime = TimePattern.Format(startTime),
                    notes,
                }
            };

            // Send the request and get the response back.
            var response = await PostCallableAsync<CreateBookingResult>("createBooking", payload, ct);
            return response.Result.BookingId;
        }

        // This helper method sends a POST request to the server and reads the response.
        // Both methods above use it, so we only write this logic once.
        private async Task<TEnvelope> PostCallableAsync<TEnvelope>(string functionName, object payload, CancellationToken ct)
        {
            // Get the user's login token so the server knows who is calling.
            var idToken = await _auth.GetIdTokenAsync();

            // Build the HTTP request.
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{FunctionsBaseUrl}/{functionName}")
            {
                Content = JsonContent.Create(payload),
            };

            // If we have a token, attach it to the request (proves who we are).
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

        // The full response when asking for slots.
        private class GetSlotsResult
        {
            [JsonPropertyName("result")]
            public SlotsPayload Result { get; set; } = new();
        }

        // The list of slots inside that response.
        private class SlotsPayload
        {
            [JsonPropertyName("slots")]
            public List<SlotDto> Slots { get; set; } = new();
        }

        // One single slot — start time and end time (as text like "09:00").
        private class SlotDto
        {
            [JsonPropertyName("startTime")]
            public string StartTime { get; set; } = string.Empty;

            [JsonPropertyName("endTime")]
            public string EndTime { get; set; } = string.Empty;
        }

        // The full response when creating a booking.
        private class CreateBookingResult
        {
            [JsonPropertyName("result")]
            public BookingIdPayload Result { get; set; } = new();
        }

        // The new booking's ID inside that response.
        private class BookingIdPayload
        {
            [JsonPropertyName("bookingId")]
            public string BookingId { get; set; } = string.Empty;
        }
    }

    // This file only needs ONE thing from the auth service:
    // a way to get the user's ID token.
    // It doesn't care about login, logout, or anything else.
    public interface IFirebaseAuthService
    {
        Task<string?> GetIdTokenAsync();
    }
}