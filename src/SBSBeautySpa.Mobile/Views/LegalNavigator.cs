namespace SBSBeautySpa.Mobile.Views;

// Opens the shared legal page from anywhere (About, Sign Up, Booking review).
// doc: "terms", "privacy" or "cancellation".
public static class LegalNavigator
{
    private static bool _registered;

    public static async Task OpenAsync(string doc)
    {
        if (!_registered)
        {
            Routing.RegisterRoute("LegalPage", typeof(LegalPage));
            _registered = true;
        }
        await Shell.Current.GoToAsync($"LegalPage?doc={doc}");
    }
}
