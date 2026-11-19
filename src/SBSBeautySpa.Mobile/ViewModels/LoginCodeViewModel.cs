using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // Second step of logging in: type the 6-digit code that was emailed.
    public partial class LoginCodeViewModel : BaseViewModel
    {
        private const int ResendCooldownSeconds = 45;

        private readonly IFirebaseAuthService _auth;
        private int _cooldownRun;

        public LoginCodeViewModel(IFirebaseAuthService auth)
        {
            _auth = auth;
            Title = "Check your email";
        }

        [ObservableProperty] private string code = string.Empty;
        [ObservableProperty] private string? infoMessage;
        [ObservableProperty] private string emailText = string.Empty;
        [ObservableProperty] private string resendText = "Resend code";

        private int _secondsLeft;

        // Called by the page each time it opens (a code was just emailed).
        public void Start()
        {
            Code = string.Empty;
            ErrorMessage = null;
            InfoMessage = null;
            EmailText = $"We emailed a 6-digit code to {Mask(_auth.PendingEmail)}. It expires in 10 minutes.";
            _ = RunCooldownAsync();
        }

        [RelayCommand]
        private async Task VerifyAsync()
        {
            if (IsBusy) return;

            var typed = (Code ?? string.Empty).Trim();
            if (typed.Length != 6)
            {
                ErrorMessage = "Enter the 6-digit code from your email.";
                return;
            }

            IsBusy = true;
            ErrorMessage = null;
            InfoMessage = null;
            try
            {
                var result = await _auth.VerifyLoginCodeAsync(typed);
                if (result.Success)
                    await PostLoginNavigator.EnterAppAsync();
                else
                    ErrorMessage = result.ErrorMessage ?? "That code isn't right.";
            }
            catch (Exception ex)
            {
                // TEMPORARY: show the real reason while we debug. Remove ex details later.
                ErrorMessage = $"Couldn't finish login: {ex.GetType().Name}: {ex.Message}";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ResendAsync()
        {
            if (IsBusy || _secondsLeft > 0) return;

            IsBusy = true;
            ErrorMessage = null;
            InfoMessage = null;
            try
            {
                var result = await _auth.SendLoginCodeAsync();
                if (result.Success)
                {
                    Code = string.Empty;
                    InfoMessage = "A new code is on its way.";
                    _ = RunCooldownAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage ?? "Couldn't send a new code. Please try again.";
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
            }
        }

        [RelayCommand]
        private async Task BackAsync()
        {
            _cooldownRun++;
            _auth.CancelPending();
            await Shell.Current.GoToAsync("..");
        }

        private async Task RunCooldownAsync()
        {
            var run = ++_cooldownRun;
            _secondsLeft = ResendCooldownSeconds;
            while (_secondsLeft > 0 && run == _cooldownRun)
            {
                ResendText = $"Resend code in {_secondsLeft}s";
                await Task.Delay(1000);
                _secondsLeft--;
            }
            if (run == _cooldownRun)
            {
                _secondsLeft = 0;
                ResendText = "Resend code";
            }
        }

        private static string Mask(string? email)
        {
            if (string.IsNullOrEmpty(email) || !email.Contains('@')) return "your email";
            var parts = email.Split('@');
            var name = parts[0];
            var shown = name.Length <= 2 ? name[..1] : name[..2];
            return $"{shown}{new string('*', Math.Max(3, name.Length - shown.Length))}@{parts[1]}";
        }
    }
}