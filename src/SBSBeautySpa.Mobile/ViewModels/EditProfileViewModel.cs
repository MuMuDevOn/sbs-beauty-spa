using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class EditProfileViewModel : BaseViewModel
    {
        public EditProfileViewModel()
        {
            Title = "Edit Profile";
        }

        [ObservableProperty] private string name = string.Empty;
        [ObservableProperty] private string email = string.Empty;
        [ObservableProperty] private string phoneNumber = string.Empty;

        [RelayCommand]
        private async Task LoadAsync()
        {
            // TODO: load current profile
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Please enter your name and email.";
                return;
            }

            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = null;

            try
            {
                // TODO: save profile
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't save your changes.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}