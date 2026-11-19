using SBSBeautySpa.Mobile.Views;

namespace SBSBeautySpa.Mobile.Services
{
    // Finishes a login or sign-up: loads the user's role, then swaps in the right app shell
    // (admin or client). Same steps the Login page used to do itself.
    public static class PostLoginNavigator
    {
        public static async Task EnterAppAsync()
        {
            var services = IPlatformApplication.Current?.Services;
            var claims = services?.GetService<IUserClaimsService>();
            if (claims is not null)
                await claims.RefreshAsync();

            var window = Application.Current?.Windows.FirstOrDefault();
            if (window is null) return;

            if (claims?.IsAdmin == true)
            {
                var adminShell = new AdminShell();
                window.Page = adminShell;
                await adminShell.GoToAsync("//AdminMain/AdminDashboard");
            }
            else
            {
                var clientShell = new ClientShell();
                window.Page = clientShell;
                await clientShell.GoToAsync("//Main/HomePage");
            }
        }
    }
}
