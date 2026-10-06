using System.Linq;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SBSBeautySpa.Mobile.Services
{
    // Represents one admin user in the system.
    // SuperAdmin = can manage other admins
    // Admin = can manage the business but not other admins
    public record AdminUser(string Uid, string? Email, bool Admin, bool SuperAdmin);

    // The contract for any admin users service.
    public interface IAdminUsersService
    {
        Task<List<AdminUser>> ListAdminsAsync(CancellationToken ct = default);
        Task SetAdminAsync(string targetUid, bool makeAdmin, CancellationToken ct = default);
        Task SetSuperAdminAsync(string targetUid, bool makeSuperAdmin, CancellationToken ct = default);
    }

    // This class talks to the server about admin accounts.
    // It's the client-side partner of firebase/functions/adminClaims.js.
    //
    // Only a SuperAdmin can use these methods — and the server checks that
    // on every call. This class doesn't try to enforce permissions on the
    // client side; it just sends the request.
    //
    // IMPORTANT: After changing someone's admin status, their old ID token
    // still carries the old roles until they sign in again. If you need the
    // change to take effect immediately for someone who's already using the
    // app, they'll need to sign out and back in.
    public class AdminUsersService : IAdminUsersService
    {
        private readonly HttpClient _http;
        private readonly IFirebaseAuthService _auth;

        // The base web address of your deployed Cloud Functions.
        // Replace this with your real URL after deploying.
        private const string FunctionsBaseUrl = "https://<region>-<project-id>.cloudfunctions.net";

        public AdminUsersService(HttpClient http, IFirebaseAuthService auth)
        {
            _http = http;
            _auth = auth;
        }

        // Gets a list of all admin users.
        public async Task<List<AdminUser>> ListAdminsAsync(CancellationToken ct = default)
        {
            var response = await PostCallableAsync<ListEnvelope>("listAdmins", new { data = new { } }, ct);
            return response.Result.Admins
                .Select(a => new AdminUser(a.Uid, a.Email, a.Admin, a.SuperAdmin))
                .ToList();
        }

        // Gives or removes the "Admin" role for a user.
        public Task SetAdminAsync(string targetUid, bool makeAdmin, CancellationToken ct = default) =>
            PostCallableAsync<object>("setAdminClaim", new { data = new { targetUid, makeAdmin } }, ct);

        // Gives or removes the "SuperAdmin" role for a user.
        public Task SetSuperAdminAsync(string targetUid, bool makeSuperAdmin, CancellationToken ct = default) =>
            PostCallableAsync<object>("setSuperAdminClaim", new { data = new { targetUid, makeSuperAdmin } }, ct);

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

        // The full response when listing admins.
        private class ListEnvelope
        {
            [JsonPropertyName("result")]
            public ListPayload Result { get; set; } = new();
        }

        // The list of admins inside that response.
        private class ListPayload
        {
            [JsonPropertyName("admins")]
            public List<AdminDto> Admins { get; set; } = new();
        }

        // One admin in the list.
        private class AdminDto
        {
            [JsonPropertyName("uid")]
            public string Uid { get; set; } = string.Empty;

            [JsonPropertyName("email")]
            public string? Email { get; set; }

            [JsonPropertyName("admin")]
            public bool Admin { get; set; }

            [JsonPropertyName("superAdmin")]
            public bool SuperAdmin { get; set; }
        }
    }
}