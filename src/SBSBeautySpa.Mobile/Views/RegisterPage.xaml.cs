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

            // SignUpAsync already signs the new user in, so go straight to the home page
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RegisterViewModel.RegistrationSucceeded) && _viewModel.RegistrationSucceeded)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    _viewModel.RegistrationSucceeded = false;
                    await Shell.Current.GoToAsync("//HomePage");
                });
            }
        }
    }
}