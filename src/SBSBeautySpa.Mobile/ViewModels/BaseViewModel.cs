using CommunityToolkit.Mvvm.ComponentModel;
 namespace SBSBeautySpa.Mobile.ViewModels
{
    /// <summary> Common bindable state every screen's ViewModel needs. </summary>
    public abstract partial class BaseViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string? errorMessage;

        public bool IssNotBusy => !IsBusy;

        partial void OnIsBusyChanged (bool value)
        {
            OnPropertyChanged(nameof(IsNotBusy));
        }

    }


}