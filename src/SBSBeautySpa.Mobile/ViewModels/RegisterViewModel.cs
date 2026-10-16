using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class RegisterViewModel : BaseViewModel
    {
        private readonly IFirebaseAuthService _authService;

        public RegisterViewModel(IFirebaseAuthService authService)
        {
            _authService = authService;
            Title = "Create Account";
            FullName = string.Empty;
            Email = string.Empty;
            PhoneNumber = string.Empty;
            Password = string.Empty;
            ConfirmPassword = string.Empty;
        }

        [ObservableProperty]
        private string fullName = string.Empty;

        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private string phoneNumber = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty]
        private string confirmPassword = string.Empty;

        [ObservableProperty]
        private bool agreedToTerms;

        [ObservableProperty]
        private bool registrationSucceeded;

        private bool CanRegister() =>
            !IsBusy &&
            !string.IsNullOrWhiteSpace(FullName) &&
            !string.IsNullOrWhiteSpace(Email) &&
            !string.IsNullOrWhiteSpace(PhoneNumber) &&
            !string.IsNullOrWhiteSpace(Password) &&
            AgreedToTerms;

        [RelayCommand(CanExecute = nameof(CanRegister))]
        private async Task RegisterAsync()
        {
            if (!CanRegister()) return;

            if (Password != ConfirmPassword)
            {
                ErrorMessage = "Passwords don't match.";
                return;
            }

            IsBusy = true;
            RegisterCommand.NotifyCanExecuteChanged();
            ErrorMessage = null;
            RegistrationSucceeded = false;

            try
            {
                var result = await _authService.SignUpAsync(FullName.Trim(), Email.Trim(), PhoneNumber.Trim(), Password);
                if (result.Success)
                    RegistrationSucceeded = true;
                else
                    ErrorMessage = result.ErrorMessage ?? "Couldn't create your account. Please try again.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Something went wrong. Check your connection and try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
                RegisterCommand.NotifyCanExecuteChanged();
            }
        }

        partial void OnFullNameChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();
        partial void OnEmailChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();
        partial void OnPhoneNumberChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();
        partial void OnPasswordChanged(string value) => RegisterCommand.NotifyCanExecuteChanged();
        partial void OnAgreedToTermsChanged(bool value) => RegisterCommand.NotifyCanExecuteChanged();
    }
}