using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class LoginPage : ContentPage
    {
        private readonly LoginViewModel _viewModel;

        public LoginPage(LoginViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            // Go to the home page as soon as the ViewModel reports a successful login
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LoginViewModel.LoginSucceeded) && _viewModel.LoginSucceeded)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    _viewModel.LoginSucceeded = false;
                    _viewModel.Password = string.Empty;
                    await Shell.Current.GoToAsync("//HomePage");
                });
            }
        }

        // For the "Sign Up" button (top of page)
        private async void OnSignUpClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("RegisterPage");
        }

        // For the "Sign Up" label (bottom of page)
        private async void OnSignUpTapped(object? sender, TappedEventArgs e)
        {
            await Shell.Current.GoToAsync("RegisterPage");
        }
    }
}