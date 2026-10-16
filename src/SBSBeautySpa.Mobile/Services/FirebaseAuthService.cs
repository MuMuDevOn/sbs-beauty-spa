using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Storage;

namespace SBSBeautySpa.Mobile.Services
{
   
    // Firebase Auth via REST API. Handles sign-in, sign-up, sign-out,
    // and token refresh.
    public class FirebaseAuthService : IFirebaseAuthService
    {
        private const string WebApiKey = FirebaseConfig.WebApiKey;
        private const string ProjectId = FirebaseConfig.ProjectId;

        private const string AccountsUrl = "https://identitytoolkit.googleapis.com/v1/accounts";
        private const string TokenUrl = "https://securetoken.googleapis.com/v1/token";

        private const string KeyIdToken = "sbs_id_token";
        private const string KeyRefreshToken = "sbs_refresh_token";
        private const string KeyUid = "sbs_uid";
        private const string KeyExpiresAt = "sbs_expires_at_utc_ticks";

        // Refresh a little early so a token never expires mid-request.
        private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(2);

        private readonly HttpClient _http;
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        private string? _idToken;
        private string? _refreshToken;
        private string? _uid;
        private DateTime _expiresAtUtc = DateTime.MinValue;

        public FirebaseAuthService(HttpClient http) => _http = http;

        public string? CurrentUserId => _uid;

        public bool IsSignedIn => !string.IsNullOrEmpty(_uid) && !string.IsNullOrEmpty(_refreshToken);

        // Restores the saved session on app start
        public async Task InitializeAsync()
        {
            try
            {
                _idToken = await SecureStorage.Default.GetAsync(KeyIdToken);
                _refreshToken = await SecureStorage.Default.GetAsync(KeyRefreshToken);
                _uid = await SecureStorage.Default.GetAsync(KeyUid);

                var ticks = await SecureStorage.Default.GetAsync(KeyExpiresAt);
                if (long.TryParse(ticks, out var parsed))
                {
                    _expiresAtUtc = new DateTime(parsed, DateTimeKind.Utc);
                }
            }
            catch (Exception ex)
            {
                // SecureStorage can fail if the device keystore is reset.
                Debug.WriteLine(ex);
                await ClearSessionAsync();
            }
        }

        // Returns a valid token, refreshing it if needed
        public async Task<string?> GetIdTokenAsync()
        {
            if (!IsSignedIn)
            {
                return null;
            }

            if (DateTime.UtcNow < _expiresAtUtc - RefreshMargin)
            {
                return _idToken;
            }

            await RefreshAsync();
            return IsSignedIn ? _idToken : null; // refresh may have signed us out
        }

        // Signs in an existing user
        public async Task<AuthResult> SignInAsync(string email, string password)
        {
            try
            {
                using var response = await _http.PostAsJsonAsync(
                    $"{AccountsUrl}:signInWithPassword?key={WebApiKey}",
                    new { email, password, returnSecureToken = true });

                if (!response.IsSuccessStatusCode)
                {
                    return new AuthResult(false, await ReadFriendlyErrorAsync(response));
                }

                var body = await response.Content.ReadFromJsonAsync<SessionResponse>();
                if (body is null)
                {
                    return new AuthResult(false, "Couldn't log in. Please try again.");
                }

                await StartSessionAsync(body.IdToken, body.RefreshToken, body.LocalId, body.ExpiresIn);
                return new AuthResult(true);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(ex);
                return new AuthResult(false, "Couldn't connect. Check your internet connection.");
            }
        }

        // Creates a new user account
        public async Task<AuthResult> SignUpAsync(string name, string email, string phone, string password)
        {
            try
            {
                using var response = await _http.PostAsJsonAsync(
                    $"{AccountsUrl}:signUp?key={WebApiKey}",
                    new { email, password, returnSecureToken = true });

                if (!response.IsSuccessStatusCode)
                {
                    return new AuthResult(false, await ReadFriendlyErrorAsync(response));
                }

                var body = await response.Content.ReadFromJsonAsync<SessionResponse>();
                if (body is null)
                {
                    return new AuthResult(false, "Couldn't create your account. Please try again.");
                }

                await StartSessionAsync(body.IdToken, body.RefreshToken, body.LocalId, body.ExpiresIn);

                // Best-effort follow-ups — account is created either way
                await TrySetDisplayNameAsync(body.IdToken, name);
                await TryCreateProfileDocAsync(body.LocalId, body.IdToken, name, email, phone);

                return new AuthResult(true);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(ex);
                return new AuthResult(false, "Couldn't connect. Check your internet connection.");
            }
        }

        // Local sign-out only (forgets the session on this device)
        public Task SignOutAsync() => ClearSessionAsync();

        // -----------------------------------------------------------------
        // Session handling
        // -----------------------------------------------------------------

        private async Task StartSessionAsync(string idToken, string refreshToken, string uid, string expiresInSeconds)
        {
            _idToken = idToken;
            _refreshToken = refreshToken;
            _uid = uid;
            _expiresAtUtc = DateTime.UtcNow.AddSeconds(int.TryParse(expiresInSeconds, out var s) ? s : 3600);

            try
            {
                await SecureStorage.Default.SetAsync(KeyIdToken, idToken);
                await SecureStorage.Default.SetAsync(KeyRefreshToken, refreshToken);
                await SecureStorage.Default.SetAsync(KeyUid, uid);
                await SecureStorage.Default.SetAsync(KeyExpiresAt, _expiresAtUtc.Ticks.ToString());
            }
            catch (Exception ex)
            {
                // Couldn't persist — session still works for this run
                Debug.WriteLine(ex);
            }
        }

        private async Task ClearSessionAsync()
        {
            _idToken = null;
            _refreshToken = null;
            _uid = null;
            _expiresAtUtc = DateTime.MinValue;

            try
            {
                SecureStorage.Default.Remove(KeyIdToken);
                SecureStorage.Default.Remove(KeyRefreshToken);
                SecureStorage.Default.Remove(KeyUid);
                SecureStorage.Default.Remove(KeyExpiresAt);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            await Task.CompletedTask;
        }

        private async Task RefreshAsync()
        {
            await _refreshLock.WaitAsync();
            try
            {
                // Another caller may have refreshed while we waited
                if (DateTime.UtcNow < _expiresAtUtc - RefreshMargin || string.IsNullOrEmpty(_refreshToken))
                {
                    return;
                }

                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = _refreshToken!,
                });

                using var response = await _http.PostAsync($"{TokenUrl}?key={WebApiKey}", content);

                if (!response.IsSuccessStatusCode)
                {
                    // 4xx = token revoked → sign out. 5xx = keep session.
                    if ((int)response.StatusCode is >= 400 and < 500)
                    {
                        await ClearSessionAsync();
                    }
                    return;
                }

                var body = await response.Content.ReadFromJsonAsync<RefreshResponse>();
                if (body is not null)
                {
                    await StartSessionAsync(body.IdToken, body.RefreshToken, body.UserId, body.ExpiresIn);
                }
            }
            catch (HttpRequestException ex)
            {
                // Offline: keep the session
                Debug.WriteLine(ex);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        // -----------------------------------------------------------------
        // Sign-up follow-ups
        // -----------------------------------------------------------------

        private async Task TrySetDisplayNameAsync(string idToken, string name)
        {
            try
            {
                using var response = await _http.PostAsJsonAsync(
                    $"{AccountsUrl}:update?key={WebApiKey}",
                    new { idToken, displayName = name, returnSecureToken = false });
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        // Writes the users/{uid} profile doc in Firestore.
        // No "role" field — admin status lives only in Auth custom claims.
        private async Task TryCreateProfileDocAsync(string uid, string idToken, string name, string email, string phone)
        {
            try
            {
                var url = $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents/users/{uid}";
                var doc = new
                {
                    fields = new
                    {
                        name = new { stringValue = name },
                        email = new { stringValue = email },
                        phoneNumber = new { stringValue = phone },
                        createdAt = new { timestampValue = DateTime.UtcNow.ToString("o") },
                    }
                };

                using var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(doc) };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", idToken);

                using var response = await _http.SendAsync(request);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        // -----------------------------------------------------------------
        // Errors
        // -----------------------------------------------------------------

        // Converts Firebase error codes into friendly messages
        private static async Task<string> ReadFriendlyErrorAsync(HttpResponseMessage response)
        {
            string code = string.Empty;
            try
            {
                var raw = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var message))
                {
                    code = message.GetString() ?? string.Empty;
                }
            }
            catch (JsonException)
            {
            }

            // Wrong password and unknown email share one message on purpose
            if (code.StartsWith("WEAK_PASSWORD", StringComparison.Ordinal))
            {
                return "Password must be at least 6 characters.";
            }

            return code switch
            {
                "EMAIL_EXISTS" => "An account with this email already exists.",
                "INVALID_LOGIN_CREDENTIALS" or "INVALID_PASSWORD" or "EMAIL_NOT_FOUND" => "Incorrect email or password.",
                "INVALID_EMAIL" => "That email address isn't valid.",
                "USER_DISABLED" => "This account has been disabled.",
                "TOO_MANY_ATTEMPTS_TRY_LATER" => "Too many attempts. Please try again later.",
                _ => "Something went wrong. Please try again.",
            };
        }

        // Response DTOs (different endpoints use different casing)

        private class SessionResponse
        {
            [JsonPropertyName("idToken")] public string IdToken { get; set; } = string.Empty;
            [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = string.Empty;
            [JsonPropertyName("localId")] public string LocalId { get; set; } = string.Empty;
            [JsonPropertyName("expiresIn")] public string ExpiresIn { get; set; } = "3600";
        }

        private class RefreshResponse
        {
            [JsonPropertyName("id_token")] public string IdToken { get; set; } = string.Empty;
            [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
            [JsonPropertyName("user_id")] public string UserId { get; set; } = string.Empty;
            [JsonPropertyName("expires_in")] public string ExpiresIn { get; set; } = "3600";
        }
    }
}