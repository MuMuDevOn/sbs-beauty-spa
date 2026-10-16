using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class ProfileViewModel : BaseViewModel
    {
        private readonly IFirebaseAuthService _auth;

        public ProfileViewModel(IFirebaseAuthService auth)
        {
            _auth = auth;
            Title = "Profile";
        }

        [ObservableProperty] private string name = string.Empty;
        [ObservableProperty] private string email = string.Empty;
        [ObservableProperty] private string phoneNumber = string.Empty;

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = null;

            try
            {
                // TODO: load from Firestore once IFirestoreService has GetMyProfileAsync
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load your profile.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand] private Task OpenEditProfileAsync() => Shell.Current.GoToAsync(nameof(Views.EditProfilePage));
        [RelayCommand] private Task OpenPaymentMethodsAsync() => Shell.Current.GoToAsync(nameof(Views.PaymentMethodsPage));
        [RelayCommand] private Task OpenNotificationsAsync() => Shell.Current.GoToAsync(nameof(Views.NotificationsPage));
        [RelayCommand] private Task OpenContactUsAsync() => Shell.Current.GoToAsync(nameof(Views.ContactUsPage));
        [RelayCommand] private Task OpenAboutAsync() => Shell.Current.GoToAsync(nameof(Views.AboutPage));

        [RelayCommand]
        private async Task LogoutAsync()
        {
            await _auth.SignOutAsync();
            await Shell.Current.GoToAsync("//GetStartedPage");
        }
    }
}