using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    /// <summary>
    /// Needs app-level wiring that isn't in this file:
    ///   MauiProgram.cs:  AddTransient&lt;GalleryViewModel&gt;(); AddTransient&lt;GalleryView&gt;();
    ///   AppShell:        this page as a tab.
    /// </summary>
    public partial class GalleryView : ContentPage
    {
        private readonly GalleryViewModel _viewModel;

        public GalleryView(GalleryViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.LoadCommand.Execute(null);
        }

        private async void OnBackTapped(object? sender, TappedEventArgs e)
        {
            // Gallery is a tab root — only go back if there's somewhere to go.
            if (Navigation.NavigationStack.Count > 1)
            {
                await Shell.Current.GoToAsync("..");
            }
        }
    }
}
