using CommunityToolkit.Mvvm.Input;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // Backs the "Get Started" welcome screen.
    // Just handles navigation to the login page.
    public partial class GetStartedViewModel : BaseViewModel
    {
        public GetStartedViewModel()
        {
            Title = "Welcome";
        }

        // Navigates to the login page when "Get Started" is tapped
        [RelayCommand]
        private async Task ContinueAsync()
        {
            await Shell.Current.GoToAsync("LoginPage");
        }
    }
}