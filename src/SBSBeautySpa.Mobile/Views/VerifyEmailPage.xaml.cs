using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class VerifyEmailPage : ContentPage
    {
        private readonly VerifyEmailViewModel _viewModel;

        public VerifyEmailPage(VerifyEmailViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            SizeChanged += OnPageSizeChanged;
        }

        // Phones use the full width; wide windows get a centred column.
        private void OnPageSizeChanged(object? sender, EventArgs e)
        {
            if (Width <= 0) return;
            ContentColumn.WidthRequest = Math.Min(Width, 460);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.Start();
        }

        // The phone's back button / gesture does the same as the arrow.
        protected override bool OnBackButtonPressed()
        {
            _viewModel.BackCommand.Execute(null);
            return true;
        }
    }
}
