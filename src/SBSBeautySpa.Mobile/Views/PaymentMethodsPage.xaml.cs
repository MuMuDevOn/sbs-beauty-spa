namespace SBSBeautySpa.Mobile.Views;

public partial class PaymentMethodsPage : ContentPage
{
    public PaymentMethodsPage() => InitializeComponent();

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnAddPaymentClicked(object sender, EventArgs e)
    {
        // TODO: open add-card form or payment provider
        await DisplayAlertAsync("Add payment method", "Coming soon", "OK");
    }
}