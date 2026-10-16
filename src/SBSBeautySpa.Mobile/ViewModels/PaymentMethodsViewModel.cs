using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class PaymentMethodsViewModel : BaseViewModel
    {
        public PaymentMethodsViewModel()
        {
            Title = "Payment Methods";
        }

        [ObservableProperty] private bool hasSavedMethods;

        [RelayCommand]
        private async Task LoadAsync()
        {
            // TODO: load saved methods
            HasSavedMethods = false;
            await Task.CompletedTask;
        }

        [RelayCommand]
        private async Task AddPaymentMethodAsync()
        {
            await Shell.Current.DisplayAlertAsync("Add payment method", "Coming soon", "OK");
        }
    }
}