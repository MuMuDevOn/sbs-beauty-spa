using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class GetStartedPage : ContentPage
    {
        // Only the newest animation run is allowed to finish the page.
        private int _runId;

        public GetStartedPage(GetStartedViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RollLogoInAsync();
        }

        // The logo starts off the left edge of the screen, spinning, and rolls to its place
        // like a wheel (turning clockwise as it moves right), slowing down as it arrives.
        private async Task RollLogoInAsync()
        {
            var myRun = ++_runId;

            try
            {
                // On Windows the page can appear before it has a width, so give layout a moment.
                if (Width <= 0)
                    await Task.Delay(150);

                var distance = Width > 0 ? Width : 500;

                LogoImage.CancelAnimations();
                LogoImage.TranslationX = -distance;
                LogoImage.Rotation = -720;          // two full turns
                LogoImage.Opacity = 1;
                TaglineLabel.Opacity = 0;

                const uint duration = 1600;
                await Task.WhenAll(
                    LogoImage.TranslateToAsync(0, 0, duration, Easing.CubicOut),
                    LogoImage.RotateToAsync(0, duration, Easing.CubicOut));

                if (myRun == _runId)
                    await TaglineLabel.FadeToAsync(1, 500, Easing.CubicIn);
            }
            catch (Exception ex)
            {
                // Never let an animation problem hide the page.
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                // Whatever happened, the newest run always leaves the logo and tagline in place.
                if (myRun == _runId)
                {
                    LogoImage.TranslationX = 0;
                    LogoImage.Rotation = 0;
                    LogoImage.Opacity = 1;
                    TaglineLabel.Opacity = 1;
                }
            }
        }
    }
}