using System.Net.Http.Json;
using System.Text.Json;
using NodaTime;
using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    // Admin-only service. Reads ALL bookings (not just the current user's).
    // Uses direct Firestore reads — the rules already allow admins to read
    // every booking.
    public class AdminDataService : IAdminDataService
    {
        private readonly HttpClient _http;
        private readonly IFirebaseAuthService _auth;

        private const string BaseUrl = "https://firestore.googleapis.com/v1";
        private static readonly string DocumentsPath =
            $"projects/{FirebaseConfig.ProjectId}/databases/(default)/documents";

        public AdminDataService(HttpClient http, IFirebaseAuthService auth)
        {
            _http = http;
            _auth = auth;
        }

        // Builds the dashboard summary: counts + revenue + recent bookings
        public async Task<AdminDashboardSummary> GetDashboardSummaryAsync(CancellationToken ct = default)
        {
            var allBookings = await GetAllBookingsAsync(ct);

            // Upcoming: still pending or confirmed
            var upcoming = allBookings.Count(b =>
                b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed);

            // Pending count
            var pending = allBookings.Count(b => b.Status == BookingStatus.Pending);

            // Revenue this month: only bookings CREATED this month
            var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var revenue = allBookings
                .Where(b => b.CreatedAt.HasValue && b.CreatedAt.Value >= startOfMonth)
                .Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed)
                .Sum(b => b.DepositAmount);

            // Recent bookings: 5 most recent by creation date
            var recent = allBookings
                .OrderByDescending(b => b.CreatedAt ?? DateTime.MinValue)
                .Take(5)
                .ToList();

            return new AdminDashboardSummary(
                UpcomingBookingsCount: upcoming,
                RevenueThisMonth: revenue,
                PendingCount: pending,
                RecentBookings: recent
            );
        }

        // Gets all bookings, optionally filtered by status and/or date range
        public async Task<List<Booking>> GetBookingsAsync(
            BookingStatus? status = null,
            LocalDate? from = null,
            LocalDate? to = null,
            CancellationToken ct = default)
        {
            var bookings = await GetAllBookingsAsync(ct);

            if (status is not null)
                bookings = bookings.Where(b => b.Status == status.Value).ToList();

            if (from is not null)
                bookings = bookings.Where(b => b.Date >= from.Value).ToList();

            if (to is not null)
                bookings = bookings.Where(b => b.Date <= to.Value).ToList();

            return bookings;
        }

       
        // Firestore reads
      

        private async Task<List<Booking>> GetAllBookingsAsync(CancellationToken ct)
        {
            var request = await BuildRequestAsync(
                $"{BaseUrl}/{DocumentsPath}/bookings", ct);

            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return new List<Booking>();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("documents", out var documents))
                return new List<Booking>();

            return documents.EnumerateArray().Select(MapBooking).ToList();
        }

        private async Task<HttpRequestMessage> BuildRequestAsync(string url, CancellationToken ct)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);

            var token = await _auth.GetIdTokenAsync();
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            return request;
        }

        
        // Firestore JSON mappers
       

        private static string GetString(JsonElement fields, string key)
        {
            if (fields.TryGetProperty(key, out var f) && f.TryGetProperty("stringValue", out var v))
                return v.GetString() ?? "";
            return "";
        }

        private static decimal GetDecimal(JsonElement fields, string key)
        {
            if (fields.TryGetProperty(key, out var f))
            {
                if (f.TryGetProperty("doubleValue", out var d)) return (decimal)d.GetDouble();
                if (f.TryGetProperty("integerValue", out var i)) return decimal.Parse(i.GetString() ?? "0");
            }
            return 0;
        }

        private static LocalDate GetLocalDate(JsonElement fields, string key)
        {
            var s = GetString(fields, key);
            return string.IsNullOrEmpty(s) ? default : LocalDate.FromDateOnly(DateOnly.Parse(s));
        }

        private static LocalTime GetLocalTime(JsonElement fields, string key)
        {
            var s = GetString(fields, key);
            return string.IsNullOrEmpty(s) ? default : LocalTime.FromTimeOnly(TimeOnly.Parse(s));
        }

        // Parses a Firestore timestamp like "2026-10-13T15:30:00Z"
        private static DateTime? GetTimestamp(JsonElement fields, string key)
        {
            if (fields.TryGetProperty(key, out var f) && f.TryGetProperty("timestampValue", out var v))
            {
                var s = v.GetString();
                if (DateTime.TryParse(s, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                    return dt;
            }
            return null;
        }

        private static string GetDocId(JsonElement doc)
        {
            if (doc.TryGetProperty("name", out var name))
            {
                var full = name.GetString() ?? "";
                return full.Substring(full.LastIndexOf('/') + 1);
            }
            return "";
        }

        private static BookingStatus GetStatus(JsonElement fields, string key)
        {
            var s = GetString(fields, key).ToLowerInvariant();
            return s switch
            {
                "pending" => BookingStatus.Pending,
                "confirmed" => BookingStatus.Confirmed,
                "completed" => BookingStatus.Completed,
                "cancelled" => BookingStatus.Cancelled,
                "in_progress" => BookingStatus.Confirmed,
                "no_show" => BookingStatus.Cancelled,
                _ => BookingStatus.Pending
            };
        }

        private static Booking MapBooking(JsonElement doc)
        {
            var fields = doc.GetProperty("fields");
            return new Booking
            {
                Id = GetDocId(doc),
                ClientId = GetString(fields, "clientId"),
                ServiceId = GetString(fields, "serviceId"),
                Date = GetLocalDate(fields, "date"),
                StartTime = GetLocalTime(fields, "startTime"),
                EndTime = GetLocalTime(fields, "endTime"),
                Status = GetStatus(fields, "status"),
                DepositAmount = GetDecimal(fields, "depositAmount"),
                TotalAmount = GetDecimal(fields, "totalAmount"),
                Notes = GetString(fields, "notes"),
                CreatedAt = GetTimestamp(fields, "createdAt")
            };
        }
    }
}