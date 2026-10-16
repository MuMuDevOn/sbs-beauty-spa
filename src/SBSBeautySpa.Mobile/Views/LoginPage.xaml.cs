using SBSBeautySpa.Mobile.Services;
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

                    var services = IPlatformApplication.Current?.Services;
                    var claims = services?.GetService<IUserClaimsService>();
                    if (claims is not null)
                        await claims.RefreshAsync();

                    var window = Application.Current?.Windows.FirstOrDefault();
                    if (window is null) return;

                    if (claims?.IsAdmin == true)
                    {
                        var adminShell = new AdminShell();
                        window.Page = adminShell;
                        await adminShell.GoToAsync("//AdminMain/AdminDashboard");
                    }
                    else
                    {
                        var clientShell = new ClientShell();
                        window.Page = clientShell;
                        await clientShell.GoToAsync("//Main/HomePage");
                    }
                });
            }
        }

        private async void OnSignUpClicked(object? sender, EventArgs e)
            => await Shell.Current.GoToAsync("//RegisterPage");

        private async void OnSignUpTapped(object? sender, TappedEventArgs e)
            => await Shell.Current.GoToAsync("//RegisterPage");
    }
}