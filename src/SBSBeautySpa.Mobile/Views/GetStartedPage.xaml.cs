using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class GetStartedPage : ContentPage
    {
        public GetStartedPage(GetStartedViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
        }
    }
}