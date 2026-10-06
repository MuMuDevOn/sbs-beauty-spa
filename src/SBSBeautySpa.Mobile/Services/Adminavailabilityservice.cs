using System.Linq;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using NodaTime;
using NodaTime.Text;

namespace SBSBeautySpa.Mobile.Services
{
    // Represents one day's opening hours.
    // Weekday = 0 for Sunday, 1 for Monday, ... 6 for Saturday.
    // IsClosed = true means the salon is closed that day.
    // OpenTime/CloseTime are null when the salon is closed.
    public record BusinessHoursDay(int Weekday, bool IsClosed, LocalTime? OpenTime, LocalTime? CloseTime);

    // Represents a period when the salon is blocked off (holiday, break, etc.).
    public record BlockedPeriod(string Id, Instant Start, Instant End, string Reason);

    // The contract for any admin availability service.
    public interface IAdminAvailabilityService
    {
        Task<List<BusinessHoursDay>> GetBusinessHoursAsync(CancellationToken ct = default);
        Task SetBusinessHoursAsync(IEnumerable<BusinessHoursDay> hours, CancellationToken ct = default);
        Task<List<BlockedPeriod>> ListBlockedTimeAsync(LocalDate? fromDate = null, LocalDate? toDate = null, CancellationToken ct = default);
        Task<string> AddBlockedTimeAsync(Instant start, Instant end, string reason, CancellationToken ct = default);
        Task DeleteBlockedTimeAsync(string blockedTimeId, CancellationToken ct = default);
    }

    // This class talks to the server about opening hours and blocked times.
    // It's the client-side partner of firebase/functions/adminAvailability.js.
    //
    // The admin dashboard uses this to:
    //   - Set which days and hours the salon is open
    //   - Block off dates (holidays, breaks, etc.)
    //
    // Same rules as every other service in this app:
    //   - No local caching of "the" schedule
    //   - No talking to Firestore directly
    //   - Every call reflects whatever the server currently has
    //   - Every write is re-checked on the server
    public class AdminAvailabilityService : IAdminAvailabilityService
    {
        private readonly HttpClient _http;
        private readonly IFirebaseAuthService _auth;

        // Used to read and write times like "09:30".
        private static readonly LocalTimePattern TimePattern = LocalTimePattern.CreateWithInvariantCulture("HH:mm");

        // The base web address of your deployed Cloud Functions.
        // Replace this with your real URL after deploying.
        private const string FunctionsBaseUrl = "https://<region>-<project-id>.cloudfunctions.net";

        public AdminAvailabilityService(HttpClient http, IFirebaseAuthService auth)
        {
            _http = http;
            _auth = auth;
        }

        // Gets the salon's opening hours for each day of the week.
        public async Task<List<BusinessHoursDay>> GetBusinessHoursAsync(CancellationToken ct = default)
        {
            var response = await PostCallableAsync<HoursEnvelope>("getBusinessHours", new { data = new { } }, ct);
            return response.Result.Hours.Select(MapHoursDto).ToList();
        }

        // Saves new opening hours to the server.
        public async Task SetBusinessHoursAsync(IEnumerable<BusinessHoursDay> hours, CancellationToken ct = default)
        {
            var payload = new
            {
                data = new
                {
                    hours = hours.Select(h => new
                    {
                        weekday = h.Weekday,
                        isClosed = h.IsClosed,
                        openTime = h.OpenTime.HasValue ? TimePattern.Format(h.OpenTime.Value) : null,
                        closeTime = h.CloseTime.HasValue ? TimePattern.Format(h.CloseTime.Value) : null,
                    })
                }
            };

            await PostCallableAsync<object>("setBusinessHours", payload, ct);
        }

        // Gets a list of blocked time periods within a date range.
        public async Task<List<BlockedPeriod>> ListBlockedTimeAsync(LocalDate? fromDate = null, LocalDate? toDate = null, CancellationToken ct = default)
        {
            var payload = new
            {
                data = new
                {
                    fromDate = fromDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    toDate = toDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                }
            };

            var response = await PostCallableAsync<BlocksEnvelope>("listBlockedTime", payload, ct);
            return response.Result.Blocks
                .Select(b => new BlockedPeriod(b.Id, InstantPattern.ExtendedIso.Parse(b.Start).Value, InstantPattern.ExtendedIso.Parse(b.End).Value, b.Reason))
                .ToList();
        }

        // Adds a new blocked time period (holiday, break, etc.).
        public async Task<string> AddBlockedTimeAsync(Instant start, Instant end, string reason, CancellationToken ct = default)
        {
            var payload = new
            {
                data = new
                {
                    start = InstantPattern.ExtendedIso.Format(start),
                    end = InstantPattern.ExtendedIso.Format(end),
                    reason,
                }
            };

            var response = await PostCallableAsync<AddBlockEnvelope>("setBlockedTime", payload, ct);
            return response.Result.BlockedTimeId;
        }

        // Deletes a blocked time period.
        public Task DeleteBlockedTimeAsync(string blockedTimeId, CancellationToken ct = default) =>
            PostCallableAsync<object>("deleteBlockedTime", new { data = new { blockedTimeId } }, ct);

        // Converts the server's DTO into our BusinessHoursDay record.
        private static BusinessHoursDay MapHoursDto(HoursDto dto) => new(
            dto.Weekday,
            dto.IsClosed,
            dto.IsClosed || dto.OpenTime is null ? null : TimePattern.Parse(dto.OpenTime).Value,
            dto.IsClosed || dto.CloseTime is null ? null : TimePattern.Parse(dto.CloseTime).Value
        );

        // This helper method sends a POST request to the server and reads the response.
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

        // The full response when getting opening hours.
        private class HoursEnvelope
        {
            [JsonPropertyName("result")]
            public HoursPayload Result { get; set; } = new();
        }

        // The list of hours inside that response.
        private class HoursPayload
        {
            [JsonPropertyName("hours")]
            public List<HoursDto> Hours { get; set; } = new();
        }

        // One day's opening hours.
        private class HoursDto
        {
            [JsonPropertyName("weekday")]
            public int Weekday { get; set; }

            [JsonPropertyName("isClosed")]
            public bool IsClosed { get; set; }

            [JsonPropertyName("openTime")]
            public string? OpenTime { get; set; }

            [JsonPropertyName("closeTime")]
            public string? CloseTime { get; set; }
        }

        // The full response when getting blocked periods.
        private class BlocksEnvelope
        {
            [JsonPropertyName("result")]
            public BlocksPayload Result { get; set; } = new();
        }

        // The list of blocked periods inside that response.
        private class BlocksPayload
        {
            [JsonPropertyName("blocks")]
            public List<BlockDto> Blocks { get; set; } = new();
        }

        // One blocked period.
        private class BlockDto
        {
            [JsonPropertyName("id")]
            public string Id { get; set; } = string.Empty;

            [JsonPropertyName("start")]
            public string Start { get; set; } = string.Empty;

            [JsonPropertyName("end")]
            public string End { get; set; } = string.Empty;

            [JsonPropertyName("reason")]
            public string Reason { get; set; } = string.Empty;
        }

        // The full response when adding a blocked period.
        private class AddBlockEnvelope
        {
            [JsonPropertyName("result")]
            public AddBlockPayload Result { get; set; } = new();
        }

        // The new blocked period's ID inside that response.
        private class AddBlockPayload
        {
            [JsonPropertyName("blockedTimeId")]
            public string BlockedTimeId { get; set; } = string.Empty;
        }
    }
}