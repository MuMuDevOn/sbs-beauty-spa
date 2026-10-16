using System.Text.Json;

namespace SBSBeautySpa.Mobile.Services
{
    public static class TokenClaims
    {
        // Reads one value (e.g. "email", "name") from the login token.
        // Works for strings, numbers and booleans (booleans come back as "true"/"false").
        public static string? Read(string? token, string claim)
        {
            var value = GetClaim(token, claim);
            if (value is null) return null;

            return value.Value.ValueKind switch
            {
                JsonValueKind.String => value.Value.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => null,
                _ => value.Value.ToString()
            };
        }

        // True when the token carries the custom claim "admin": true
        public static bool IsAdmin(string? token)
        {
            var value = GetClaim(token, "admin");
            return value is { ValueKind: JsonValueKind.True };
        }

        private static JsonElement? GetClaim(string? token, string claim)
        {
            if (string.IsNullOrEmpty(token)) return null;
            var parts = token.Split('.');
            if (parts.Length < 2) return null;

            try
            {
                var payload = parts[1].Replace('-', '+').Replace('_', '/');
                payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
                return doc.RootElement.TryGetProperty(claim, out var value) ? value.Clone() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}