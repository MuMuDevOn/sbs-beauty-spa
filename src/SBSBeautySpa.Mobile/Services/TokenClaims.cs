using System.Text.Json;

namespace SBSBeautySpa.Mobile.Services
{
    public static class TokenClaims
    {
        // Reads one value (e.g. "email", "name") from the login token
        public static string? Read(string? token, string claim)
        {
            if (string.IsNullOrEmpty(token)) return null;
            var parts = token.Split('.');
            if (parts.Length < 2) return null;

            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
                return doc.RootElement.TryGetProperty(claim, out var value) ? value.GetString() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}