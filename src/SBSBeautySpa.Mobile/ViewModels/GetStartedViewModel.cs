using CommunityToolkit.Mvvm.Input;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class GetStartedViewModel : BaseViewModel
    {
        public GetStartedViewModel()
        {
            Title = "Welcome";
        }

        [RelayCommand]
        private async Task ContinueAsync()
        {
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}