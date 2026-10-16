using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.Views;

public partial class EditProfilePage : ContentPage
{
    public EditProfilePage()
    {
        InitializeComponent();
        EmailEntry.IsReadOnly = true; // email can't be changed from here
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var services = IPlatformApplication.Current?.Services;
            var auth = services?.GetService<IFirebaseAuthService>();
            if (auth is null) return;

            var token = await auth.GetIdTokenAsync();
            var email = TokenClaims.Read(token, "email");
            var name = TokenClaims.Read(token, "name");

            if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(email))
            {
                var local = email.Split('@')[0];
                name = local.Length > 0 ? char.ToUpper(local[0]) + local[1..] : "";
            }

            NameEntry.Text = name ?? "";
            EmailEntry.Text = email ?? "";

            // Saved profile (name + phone) wins when available
            var firestore = services?.GetService<IFirestoreService>();
            var profile = firestore is null ? null : await firestore.GetMyProfileAsync();
            if (profile is not null)
            {
                if (!string.IsNullOrWhiteSpace(profile.Name)) NameEntry.Text = profile.Name;
                PhoneEntry.Text = profile.Phone;
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameEntry.Text))
        {
            await DisplayAlertAsync("Missing details", "Please enter your name", "OK");
            return;
        }

        var services = IPlatformApplication.Current?.Services;
        var auth = services?.GetService<IFirebaseAuthService>();
        var firestore = services?.GetService<IFirestoreService>();
        if (auth is null || firestore is null) return;

        var name = NameEntry.Text.Trim();
        var phone = PhoneEntry.Text?.Trim() ?? "";

        var button = sender as Button;
        if (button is not null) button.IsEnabled = false;
        try
        {
            // 1. Name in Firebase Auth
            var result = await auth.UpdateDisplayNameAsync(name);
            if (!result.Success)
            {
                await DisplayAlertAsync("Couldn't save", result.ErrorMessage ?? "Please try again.", "OK");
                return;
            }

            // 2. Name + phone in users/{uid}
            await firestore.UpdateMyProfileAsync(name, phone);

            await DisplayAlertAsync("Saved", "Your changes have been saved", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't save", "We couldn't save your details. Check your connection and try again.", "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            if (button is not null) button.IsEnabled = true;
        }
    }
}