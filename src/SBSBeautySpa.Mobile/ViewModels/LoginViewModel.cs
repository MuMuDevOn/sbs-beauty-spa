using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class LoginViewModel : BaseViewModel
    {
        private readonly IFirebaseAuthService _authService;

        public LoginViewModel(IFirebaseAuthService authService)
        {
            _authService = authService;
            Title = "Sign In";
            Email = string.Empty;
            Password = string.Empty;
        }

        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private string password = string.Empty;

        [ObservableProperty]
        private bool loginSucceeded;

        // Green confirmation text (e.g. "Reset link sent")
        [ObservableProperty]
        private string? infoMessage;

        private bool CanLogin() =>
            !IsBusy &&
            !string.IsNullOrWhiteSpace(Email) &&
            !string.IsNullOrWhiteSpace(Password);

        [RelayCommand(CanExecute = nameof(CanLogin))]
        private async Task LoginAsync()
        {
            if (!CanLogin()) return;

            IsBusy = true;
            ErrorMessage = null;
            InfoMessage = null;
            LoginSucceeded = false;

            try
            {
                var result = await _authService.SignInAsync(Email.Trim(), Password);
                if (!result.Success)
                {
                    ErrorMessage = result.ErrorMessage ?? "Incorrect email or password.";
                }
                else if (result.NextStep == "code")
                {
                    // Password is right - now email a 6-digit code and ask for it.
                    var sent = await _authService.SendLoginCodeAsync();
                    if (sent.Success)
                    {
                        Password = string.Empty;
                        await Shell.Current.GoToAsync("LoginCodePage");
                    }
                    else
                    {
                        _authService.CancelPending();
                        ErrorMessage = sent.ErrorMessage ?? "Couldn't send your login code. Please try again.";
                    }
                }
                else
                {
                    LoginSucceeded = true;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't connect. Check your internet connection.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
                LoginCommand.NotifyCanExecuteChanged();
            }
        }

        // "Forgot password?" - emails a reset link to the address typed above.
        [RelayCommand]
        private async Task ForgotPasswordAsync()
        {
            if (IsBusy) return;

            ErrorMessage = null;
            InfoMessage = null;

            var address = Email?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(address))
            {
                ErrorMessage = "Type your email address above, then tap Forgot password again.";
                return;
            }

            IsBusy = true;
            try
            {
                var result = await _authService.SendPasswordResetAsync(address);
                if (result.Success)
                    InfoMessage = $"If an account exists for {address}, a password reset link is on its way. Check your inbox (and spam).";
                else
                    ErrorMessage = result.ErrorMessage ?? "Couldn't send the reset email. Please try again.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't connect. Check your internet connection.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
                LoginCommand.NotifyCanExecuteChanged();
            }
        }

        partial void OnEmailChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
        partial void OnPasswordChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    }
}