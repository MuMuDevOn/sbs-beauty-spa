using SBSBeautySpa.Mobile.Services;
using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class RegisterPage : ContentPage
    {
        private readonly RegisterViewModel _viewModel;

        public RegisterPage(RegisterViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RegisterViewModel.RegistrationSucceeded) && _viewModel.RegistrationSucceeded)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    _viewModel.RegistrationSucceeded = false;

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
    }
}