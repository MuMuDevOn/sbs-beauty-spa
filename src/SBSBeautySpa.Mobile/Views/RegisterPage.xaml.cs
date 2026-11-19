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

            SizeChanged += OnPageSizeChanged;
        }

        // Phones use the full width; wide windows get a centred column.
        private void OnPageSizeChanged(object? sender, EventArgs e)
        {
            if (Width <= 0) return;
            ContentColumn.WidthRequest = Math.Min(Width, 460);
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RegisterViewModel.RegistrationSucceeded) && _viewModel.RegistrationSucceeded)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    _viewModel.RegistrationSucceeded = false;

                    var services = IPlatformApplication.Current?.Services;

                    // New accounts must verify their email before entering the app.
                    var auth = services?.GetService<IFirebaseAuthService>();
                    if (auth is not null && auth.HasPendingSession)
                    {
                        await Shell.Current.GoToAsync("VerifyEmailPage");
                        return;
                    }

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

        private async void OnTermsTapped(object? sender, TappedEventArgs e) => await LegalNavigator.OpenAsync("terms");

        private async void OnPrivacyTapped(object? sender, TappedEventArgs e) => await LegalNavigator.OpenAsync("privacy");

        private async void OnBackTapped(object? sender, TappedEventArgs e) => await GoToLoginAsync();

        private async void OnLoginTapped(object? sender, TappedEventArgs e) => await GoToLoginAsync();

        private static async Task GoToLoginAsync()
        {
            try { await Shell.Current.GoToAsync("//LoginPage"); }
            catch { await Shell.Current.GoToAsync(".."); }
        }
    }
}