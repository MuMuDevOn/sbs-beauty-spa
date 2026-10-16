using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;
using System.Security.AccessControl;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class PaymentViewModel : BaseViewModel
    {
        private readonly IPaymentService _paymentService;

        private const string CallbackUrlPrefix = "https://sbsbeautyspa.app/payment-callback";

        public PaymentViewModel(IPaymentService paymentService)
        {
            _paymentService = paymentService;
            Title = "Payment";
        }

        public string BookingId { get; set; } = string.Empty;
        public PaymentRequestType RequestType { get; set; } = PaymentRequestType.Deposit;

        [ObservableProperty]
        private string? authorizationUrl;

        [ObservableProperty]
        private string? reference;

        [ObservableProperty]
        private bool paymentSucceeded;

        [ObservableProperty]
        private bool paymentFailed;

        [RelayCommand]
        private async Task StartPaymentAsync()
        {
            if (string.IsNullOrEmpty(BookingId) || IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            PaymentSucceeded = false;
            PaymentFailed = false;

            try
            {
                var result = await _paymentService.InitializePaymentAsync(BookingId, RequestType);
                Reference = result.Reference;
                AuthorizationUrl = result.AuthorizationUrl;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't start the payment. Please try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        public bool IsPaymentCallback(string url) => url.StartsWith(CallbackUrlPrefix, StringComparison.OrdinalIgnoreCase);

        [RelayCommand]
        private async Task HandleRedirectAsync()
        {
            if (string.IsNullOrEmpty(Reference) || IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var result = await _paymentService.VerifyPaymentAsync(Reference);
                PaymentSucceeded = result.Succeeded;
                PaymentFailed = !result.Succeeded;

                if (!result.Succeeded)
                    ErrorMessage = "We couldn't confirm the payment yet. Check My Bookings shortly — it may still go through.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't confirm the payment. Check My Bookings shortly.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}