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
            LoginSucceeded = false;

            try
            {
                var result = await _authService.SignInAsync(Email.Trim(), Password);
                if (result.Success)
                    LoginSucceeded = true;
                else
                    ErrorMessage = result.ErrorMessage ?? "Incorrect email or password.";
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