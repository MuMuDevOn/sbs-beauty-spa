namespace SBSBeautySpa.Mobile.Services
{
    public class UserClaimsService : IUserClaimsService
    {
        private readonly CallableFunctionClient _client;

        public UserClaimsService(CallableFunctionClient client) => _client = client;

        public bool IsAdmin { get; private set; }
        public bool IsSuperAdmin { get; private set; }

        public async Task RefreshAsync()
        {
            try
            {
                var result = await _client.CallAsync<ClaimsResult>("getMyClaims", new { });
                IsAdmin = result.Admin;
                IsSuperAdmin = result.SuperAdmin;
            }
            catch
            {
                IsAdmin = false;
                IsSuperAdmin = false;
            }
        }

        private class ClaimsResult
        {
            public bool Admin { get; set; }
            public bool SuperAdmin { get; set; }
        }
    }
}