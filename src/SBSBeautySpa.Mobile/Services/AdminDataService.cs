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
            // Service money = what clients ACTUALLY paid online this month (deposit or full amount).
            // If the payments can't be read, fall back to the old method (deposit amounts only).
            var serviceRevenue = await GetServiceRevenueSinceAsync(startOfMonth, ct)
                ?? allBookings
                    .Where(b => b.CreatedAt.HasValue && b.CreatedAt.Value >= startOfMonth)
                    .Where(b => b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed)
                    .Sum(b => b.DepositAmount);

            // Shop sales this month: paid product orders (the products only, not delivery).
            var productRevenue = await GetProductRevenueSinceAsync(startOfMonth, ct);

            var revenue = serviceRevenue + productRevenue;

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
            var all = new List<Booking>();
            string? pageToken = null;

            // Firestore returns documents in pages (20 by default), so keep asking for the next page.
            do
            {
                var url = $"{BaseUrl}/{DocumentsPath}/bookings?pageSize=300";
                if (pageToken is not null) url += $"&pageToken={Uri.EscapeDataString(pageToken)}";

                var request = await BuildRequestAsync(url, ct);
                var response = await _http.SendAsync(request, ct);
                var json = await response.Content.ReadAsStringAsync(ct);

                // A failed request must NOT look like "no bookings", so say what went wrong.
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException($"Firestore said {(int)response.StatusCode}: {json}");

                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("documents", out var documents))
                    all.AddRange(documents.EnumerateArray().Select(MapBooking));

                pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
            }
            while (pageToken is not null);

            return all;
        }

        // Money clients paid for BOOKINGS since "since": every completed online payment
        // (a deposit, a full payment or a balance), so a client who paid in full counts in full.
        // The balance a client pays in the spa on the day is not recorded in the app, so it isn't here.
        // Returns null if the payments couldn't be read.
        private async Task<decimal?> GetServiceRevenueSinceAsync(DateTime since, CancellationToken ct)
        {
            try
            {
                decimal total = 0;
                string? pageToken = null;

                do
                {
                    var url = $"{BaseUrl}/{DocumentsPath}/payments?pageSize=300";
                    if (pageToken is not null) url += $"&pageToken={Uri.EscapeDataString(pageToken)}";

                    var request = await BuildRequestAsync(url, ct);
                    var response = await _http.SendAsync(request, ct);
                    var json = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException($"Firestore said {(int)response.StatusCode}: {json}");

                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("documents", out var documents))
                    {
                        foreach (var d in documents.EnumerateArray())
                        {
                            if (!d.TryGetProperty("fields", out var fields)) continue;

                            if (GetString(fields, "status") != "completed") continue;
                            if (GetString(fields, "type") == "order") continue;            // shop orders are counted separately
                            if (string.IsNullOrEmpty(GetString(fields, "bookingId"))) continue;

                            var paidAt = GetTimestamp(fields, "paidAt");
                            if (paidAt is null || paidAt.Value < since) continue;

                            total += GetDecimal(fields, "amount");
                        }
                    }

                    pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
                }
                while (pageToken is not null);

                return total;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return null;
            }
        }

        // Money from shop orders: orders that were PAID since "since", not cancelled.
        // Adds up the products only (subtotal) - the delivery fee goes to the courier.
        // A problem reading orders must not break the whole dashboard, so it counts as 0.
        private async Task<decimal> GetProductRevenueSinceAsync(DateTime since, CancellationToken ct)
        {
            try
            {
                decimal total = 0;
                string? pageToken = null;

                do
                {
                    var url = $"{BaseUrl}/{DocumentsPath}/orders?pageSize=300";
                    if (pageToken is not null) url += $"&pageToken={Uri.EscapeDataString(pageToken)}";

                    var request = await BuildRequestAsync(url, ct);
                    var response = await _http.SendAsync(request, ct);
                    var json = await response.Content.ReadAsStringAsync(ct);

                    if (!response.IsSuccessStatusCode)
                        throw new InvalidOperationException($"Firestore said {(int)response.StatusCode}: {json}");

                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.TryGetProperty("documents", out var documents))
                    {
                        foreach (var d in documents.EnumerateArray())
                        {
                            if (!d.TryGetProperty("fields", out var fields)) continue;

                            if (GetString(fields, "paymentStatus") != "paid") continue;
                            if (GetString(fields, "status") == "cancelled") continue;

                            var paidAt = GetTimestamp(fields, "paidAt");
                            if (paidAt is null || paidAt.Value < since) continue;

                            total += GetDecimal(fields, "subtotal");
                        }
                    }

                    pageToken = doc.RootElement.TryGetProperty("nextPageToken", out var next) ? next.GetString() : null;
                }
                while (pageToken is not null);

                return total;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                return 0;
            }
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