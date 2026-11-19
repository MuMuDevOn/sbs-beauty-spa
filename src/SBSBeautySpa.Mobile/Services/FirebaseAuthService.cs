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
        private DateTime _expiresAtUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

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
                return;
            }

            // Sessions that never went through the emailed-code step (e.g. from before
            // it existed) are signed out, so the person logs in again with a code.
            if (IsSignedIn && !HasMfaClaim(_idToken))
            {
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

                // Password is right - but the session only starts once the emailed code is entered.
                _pending = new PendingSession(body.IdToken, body.RefreshToken, body.LocalId, body.ExpiresIn, email);
                return new AuthResult(true, null, "code");
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

                // The session only starts once the email address is verified.
                _pending = new PendingSession(body.IdToken, body.RefreshToken, body.LocalId, body.ExpiresIn, email);

                // Best-effort follow-ups — account is created either way
                await TrySetDisplayNameAsync(body.IdToken, name);
                await TryCreateProfileDocAsync(body.LocalId, body.IdToken, name, email, phone);

                // Send the verification link to their inbox (they can resend from the next screen)
                await SendVerificationEmailAsync();

                return new AuthResult(true, null, "verify-email");
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(ex);
                return new AuthResult(false, "Couldn't connect. Check your internet connection.");
            }
        }

        // Emails a password-reset link. Firebase sends the email itself.
        public async Task<AuthResult> SendPasswordResetAsync(string email)
        {
            try
            {
                using var response = await _http.PostAsJsonAsync(
                    $"{AccountsUrl}:sendOobCode?key={WebApiKey}",
                    new { requestType = "PASSWORD_RESET", email });

                if (!response.IsSuccessStatusCode)
                {
                    return new AuthResult(false, await ReadFriendlyErrorAsync(response));
                }

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

        // Changes the password. Re-checks the current one first.
        public async Task<AuthResult> ChangePasswordAsync(string currentPassword, string newPassword)
        {
            try
            {
                var email = TokenClaims.Read(await GetIdTokenAsync(), "email");
                if (string.IsNullOrEmpty(email))
                {
                    return new AuthResult(false, "Please sign in again.");
                }

                // 1. Confirm the current password is right
                string checkedToken;
                using (var check = await _http.PostAsJsonAsync(
                    $"{AccountsUrl}:signInWithPassword?key={WebApiKey}",
                    new { email, password = currentPassword, returnSecureToken = true }))
                {
                    if (!check.IsSuccessStatusCode)
                    {
                        var message = await ReadFriendlyErrorAsync(check);
                        return new AuthResult(false, message == "Incorrect email or password."
                            ? "Your current password is incorrect."
                            : message);
                    }

                    var checkBody = await check.Content.ReadFromJsonAsync<SessionResponse>();
                    if (checkBody is null)
                    {
                        return new AuthResult(false, "Something went wrong. Please try again.");
                    }
                    checkedToken = checkBody.IdToken;
                }

                // 2. Set the new password (returns fresh tokens, so the user stays signed in)
                using var response = await _http.PostAsJsonAsync(
                    $"{AccountsUrl}:update?key={WebApiKey}",
                    new { idToken = checkedToken, password = newPassword, returnSecureToken = true });

                if (!response.IsSuccessStatusCode)
                {
                    return new AuthResult(false, await ReadFriendlyErrorAsync(response));
                }

                // A new password ends every old login, so the person logs in again
                // (with the emailed code) using the new password.
                await ClearSessionAsync();

                return new AuthResult(true);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(ex);
                return new AuthResult(false, "Couldn't connect. Check your internet connection.");
            }
        }

        // Updates the display name in Firebase Auth.
        // Returns fresh tokens, so the new name shows up in the login token straight away.
        public async Task<AuthResult> UpdateDisplayNameAsync(string name)
        {
            try
            {
                var idToken = await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken))
                {
                    return new AuthResult(false, "Please sign in again.");
                }

                using var response = await _http.PostAsJsonAsync(
                    $"{AccountsUrl}:update?key={WebApiKey}",
                    new { idToken, displayName = name, returnSecureToken = true });

                if (!response.IsSuccessStatusCode)
                {
                    return new AuthResult(false, await ReadFriendlyErrorAsync(response));
                }

                var body = await response.Content.ReadFromJsonAsync<SessionResponse>();
                if (body is not null && HasMfaClaim(body.IdToken))
                {
                    await StartSessionAsync(body.IdToken, body.RefreshToken, body.LocalId, body.ExpiresIn);
                }
                else
                {
                    // Keep the current session; the new name shows up after the next token refresh.
                    await ForceRefreshAsync();
                }

                return new AuthResult(true);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(ex);
                return new AuthResult(false, "Couldn't connect. Check your internet connection.");
            }
        }

        // -----------------------------------------------------------------
        // Two-step sign-in + sign-up email verification
        // -----------------------------------------------------------------

        private record PendingSession(string IdToken, string RefreshToken, string Uid, string ExpiresIn, string Email);

        private PendingSession? _pending;

        public string? PendingEmail => _pending?.Email;

        public bool HasPendingSession => _pending is not null && !IsSignedIn;

        public void CancelPending() => _pending = null;

        public async Task<AuthResult> SendLoginCodeAsync()
        {
            if (_pending is null) return new AuthResult(false, "Please log in again.");
            return await CallFunctionAsync("sendLoginCode", new { }, _pending.IdToken);
        }

        public async Task<AuthResult> VerifyLoginCodeAsync(string code)
        {
            var pending = _pending;
            if (pending is null) return new AuthResult(false, "Please log in again.");

            var result = await CallFunctionAsync("verifyLoginCode", new { code }, pending.IdToken);
            if (!result.Success) return result;

            await StartSessionAsync(pending.IdToken, pending.RefreshToken, pending.Uid, pending.ExpiresIn);

            // Get a fresh token that carries the new "mfaAt" stamp.
            // Only forget the pending login once it fully worked, so a retry is possible.
            var finished = await FinishWithFreshTokenAsync();
            if (finished.Success) _pending = null;
            return finished;
        }

        public async Task<AuthResult> SendVerificationEmailAsync()
        {
            try
            {
                var idToken = _pending?.IdToken ?? await GetIdTokenAsync();
                if (string.IsNullOrEmpty(idToken)) return new AuthResult(false, "Please log in again.");

                using var response = await _http.PostAsJsonAsync(
                    $"{AccountsUrl}:sendOobCode?key={WebApiKey}",
                    new { requestType = "VERIFY_EMAIL", idToken });

                if (!response.IsSuccessStatusCode)
                    return new AuthResult(false, await ReadFriendlyErrorAsync(response));

                return new AuthResult(true);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(ex);
                return new AuthResult(false, "Couldn't connect. Check your internet connection.");
            }
        }

        public async Task<AuthResult> CompleteEmailVerificationAsync()
        {
            var pending = _pending;
            if (pending is null) return new AuthResult(false, "Please log in again.");

            try
            {
                using var response = await _http.PostAsJsonAsync(
                    $"{AccountsUrl}:lookup?key={WebApiKey}",
                    new { idToken = pending.IdToken });

                if (!response.IsSuccessStatusCode)
                    return new AuthResult(false, await ReadFriendlyErrorAsync(response));

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var verified =
                    doc.RootElement.TryGetProperty("users", out var users) &&
                    users.GetArrayLength() > 0 &&
                    users[0].TryGetProperty("emailVerified", out var flag) &&
                    flag.ValueKind == JsonValueKind.True;

                if (!verified)
                    return new AuthResult(false, "We can't see the verification yet. Open the link in your email first, then tap again.");

                // The server confirms it and stamps the account (no login code needed right after sign-up).
                var granted = await CallFunctionAsync("completeEmailSignup", new { }, pending.IdToken);
                if (!granted.Success) return granted;

                await StartSessionAsync(pending.IdToken, pending.RefreshToken, pending.Uid, pending.ExpiresIn);
                var finished = await FinishWithFreshTokenAsync();
                if (finished.Success) _pending = null;
                else Debug.WriteLine("[completeEmailSignup] finish failed: " + finished.ErrorMessage);
                return finished;
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(ex);
                return new AuthResult(false, "Couldn't connect. Check your internet connection.");
            }
        }

        // Refreshes the token right away and checks it carries the emailed-code stamp.
        private async Task<AuthResult> FinishWithFreshTokenAsync()
        {
            await ForceRefreshAsync();

            if (!IsSignedIn || !HasMfaClaim(_idToken))
            {
                await ClearSessionAsync();
                return new AuthResult(false, "We couldn't finish logging you in. Please try again.");
            }

            return new AuthResult(true);
        }

        private async Task ForceRefreshAsync()
        {
            _expiresAtUtc = DateTime.UtcNow.AddDays(-1);
            await RefreshAsync();
        }

        // True when the token has "mfaAt" at least as new as the sign-in time ("auth_time").
        private static bool HasMfaClaim(string? idToken)
        {
            try
            {
                if (string.IsNullOrEmpty(idToken)) return false;

                var parts = idToken.Split('.');
                if (parts.Length < 2) return false;

                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

                using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
                var root = doc.RootElement;

                return root.TryGetProperty("mfaAt", out var mfa) && mfa.TryGetInt64(out var mfaAt)
                    && root.TryGetProperty("auth_time", out var auth) && auth.TryGetInt64(out var authTime)
                    && mfaAt >= authTime;
            }
            catch
            {
                return false;
            }
        }

        // Calls one of our Cloud Functions with an explicit token (the person isn't signed in yet).
        private async Task<AuthResult> CallFunctionAsync(string functionName, object data, string idToken)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"{CallableFunctionClient.FunctionsBaseUrl}/{functionName}")
                {
                    Content = JsonContent.Create(new { data }),
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", idToken);

                using var response = await _http.SendAsync(request);
                if (response.IsSuccessStatusCode) return new AuthResult(true);

                var raw = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[{functionName}] HTTP {(int)response.StatusCode}: {raw}");
                var friendly = ReadFunctionMessage(raw);
                return new AuthResult(false, friendly == "Something went wrong. Please try again."
                    ? $"Something went wrong (code {(int)response.StatusCode}). Please try again."
                    : friendly);
            }
            catch (HttpRequestException ex)
            {
                Debug.WriteLine(ex);
                // TEMPORARY detail while debugging
                return new AuthResult(false, $"Couldn't connect ({functionName}): {ex.Message}");
            }
        }

        private static string ReadFunctionMessage(string raw)
        {
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.TryGetProperty("error", out var error) &&
                    error.TryGetProperty("message", out var message))
                {
                    var text = message.GetString();
                    if (!string.IsNullOrWhiteSpace(text)) return text!;
                }
            }
            catch (JsonException)
            {
            }
            return "Something went wrong. Please try again.";
        }

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
            _expiresAtUtc = DateTime.UtcNow.AddDays(-1);

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