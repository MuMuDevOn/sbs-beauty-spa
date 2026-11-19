using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // After sign-up: confirm the email address by tapping the link we emailed.
    public partial class VerifyEmailViewModel : BaseViewModel
    {
        private const int ResendCooldownSeconds = 45;

        private readonly IFirebaseAuthService _auth;
        private int _cooldownRun;
        private int _secondsLeft;

        public VerifyEmailViewModel(IFirebaseAuthService auth)
        {
            _auth = auth;
            Title = "Verify your email";
        }

        [ObservableProperty] private string? infoMessage;
        [ObservableProperty] private string emailText = string.Empty;
        [ObservableProperty] private string resendText = "Resend email";

        // Called by the page each time it opens (the link was just emailed).
        public void Start()
        {
            ErrorMessage = null;
            InfoMessage = null;
            EmailText = _auth.PendingEmail ?? "your email address";
            _ = RunCooldownAsync();
        }

        [RelayCommand]
        private async Task ContinueAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            InfoMessage = null;
            try
            {
                var result = await _auth.CompleteEmailVerificationAsync();
                if (result.Success)
                    await PostLoginNavigator.EnterAppAsync();
                else
                    ErrorMessage = result.ErrorMessage ?? "We can't see the verification yet.";
            }
            catch (Exception ex)
            {
                // TEMPORARY: show the real reason while we debug.
                ErrorMessage = $"Couldn't finish: {ex.GetType().Name}: {ex.Message}";
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
                var result = await _auth.SendVerificationEmailAsync();
                if (result.Success)
                {
                    InfoMessage = "We've sent the email again. Check your inbox (and spam).";
                    _ = RunCooldownAsync();
                }
                else
                {
                    ErrorMessage = result.ErrorMessage ?? "Couldn't send the email. Please try again.";
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
            try { await Shell.Current.GoToAsync("//LoginPage"); }
            catch { await Shell.Current.GoToAsync(".."); }
        }

        private async Task RunCooldownAsync()
        {
            var run = ++_cooldownRun;
            _secondsLeft = ResendCooldownSeconds;
            while (_secondsLeft > 0 && run == _cooldownRun)
            {
                ResendText = $"Resend email in {_secondsLeft}s";
                await Task.Delay(1000);
                _secondsLeft--;
            }
            if (run == _cooldownRun)
            {
                _secondsLeft = 0;
                ResendText = "Resend email";
            }
        }
    }
}