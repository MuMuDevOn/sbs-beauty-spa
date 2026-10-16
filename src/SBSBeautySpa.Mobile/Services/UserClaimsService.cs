using System.Diagnostics;

namespace SBSBeautySpa.Mobile.Services
{
    public class UserClaimsService : IUserClaimsService
    {
        private readonly CallableFunctionClient _client;
        private readonly IFirebaseAuthService _auth;

        public UserClaimsService(CallableFunctionClient client, IFirebaseAuthService auth)
        {
            _client = client;
            _auth = auth;
        }

        public bool IsAdmin { get; private set; }
        public bool IsSuperAdmin { get; private set; }

        public async Task RefreshAsync()
        {
            IsAdmin = false;
            IsSuperAdmin = false;

            // 1) Read the claims straight from the signed-in token (no network call)
            try
            {
                var token = await _auth.GetIdTokenAsync();
                IsAdmin = TokenClaims.IsAdmin(token);
                IsSuperAdmin = TokenClaims.Read(token, "superAdmin") == "true";
                Debug.WriteLine($"[Claims] from token -> admin={IsAdmin}, superAdmin={IsSuperAdmin}, tokenPresent={!string.IsNullOrEmpty(token)}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Claims] token read FAILED: {ex}");
            }

            if (IsAdmin || IsSuperAdmin) return;

            // 2) Backup: ask the backend
            try
            {
                var result = await _client.CallAsync<ClaimsResult>("getMyClaims", new { });
                IsAdmin = result.Admin;
                IsSuperAdmin = result.SuperAdmin;
                Debug.WriteLine($"[Claims] getMyClaims -> admin={IsAdmin}, superAdmin={IsSuperAdmin}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Claims] getMyClaims FAILED: {ex}");
            }
        }

        private class ClaimsResult
        {
            public bool Admin { get; set; }
            public bool SuperAdmin { get; set; }
        }
    }
}