using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class ChangePasswordViewModel : BaseViewModel
    {
        public ChangePasswordViewModel()
        {
            Title = "Change Password";
        }

        [ObservableProperty] private string currentPassword = string.Empty;
        [ObservableProperty] private string newPassword = string.Empty;
        [ObservableProperty] private string confirmPassword = string.Empty;

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentPassword) ||
                string.IsNullOrWhiteSpace(NewPassword) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ErrorMessage = "Please fill in all fields.";
                return;
            }

            if (NewPassword.Length < 8)
            {
                ErrorMessage = "Use at least 8 characters.";
                return;
            }

            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "The new passwords don't match.";
                return;
            }

            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = null;

            try
            {
                // TODO: change password
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't change your password.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}