using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // This class controls the Paystack checkout screen.
    //
    // How it works with the View:
    //   - The View contains a WebView (a mini browser).
    //   - The WebView loads the payment page from the URL we give it.
    //   - When the user finishes paying, Paystack sends them to our
    //     callback URL (https://sbsbeautyspa.app/payment-callback).
    //   - The View sees that URL and cancels the navigation — it does NOT
    //     actually load that page. Instead, it calls HandleRedirectCommand.
    //   - That command asks the server to verify the payment.
    //
    // NOTE: The callback URL isn't a page this app hosts — it's just a
    // stable URL the WebView watches for. It's a good idea to also create
    // a simple "you can close this" page at that URL, in case the WebView
    // misses the interception on some devices.
    public partial class PaymentViewModel : BaseViewModel
    {
        private readonly IPaymentService _paymentService;

        // The URL that signals "payment flow is done, go verify it".
        private const string CallbackUrlPrefix = "https://sbsbeautyspa.app/payment-callback";

        public PaymentViewModel(IPaymentService paymentService)
        {
            _paymentService = paymentService;
            Title = "Payment";
        }

        // Set by the previous screen before opening this one.
        public string BookingId { get; set; } = string.Empty;

        // What kind of payment this is (deposit, balance, or full).
        public PaymentRequestType RequestType { get; set; } = PaymentRequestType.Deposit;

        // The URL the WebView should load.
        // The View binds to this and navigates when it changes.
        [ObservableProperty]
        private string? authorizationUrl;

        // The reference for this payment (used to verify later).
        [ObservableProperty]
        private string? reference;

        // True when the payment succeeded.
        [ObservableProperty]
        private bool paymentSucceeded;

        // True when the payment failed.
        [ObservableProperty]
        private bool paymentFailed;

        // Starts the payment: asks the server for a payment URL.
        [RelayCommand]
        private async Task StartPaymentAsync()
        {
            if (string.IsNullOrEmpty(BookingId) || IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;
            PaymentSucceeded = false;
            PaymentFailed = false;

            try
            {
                // Ask the server to start a payment.
                var result = await _paymentService.InitializePaymentAsync(BookingId, RequestType);

                // Save the reference so we can verify later.
                Reference = result.Reference;

                // Set the URL — the View's WebView will navigate to it.
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

        // Called by the View when the WebView is about to navigate somewhere.
        // Returns true if the URL is our callback — the View should then
        // cancel that navigation and call HandleRedirectCommand instead.
        public bool IsPaymentCallback(string url) => url.StartsWith(CallbackUrlPrefix, StringComparison.OrdinalIgnoreCase);

        // Called after the WebView sees the callback URL.
        // Asks the server to verify whether the payment really succeeded.
        [RelayCommand]
        private async Task HandleRedirectAsync()
        {
            if (string.IsNullOrEmpty(Reference) || IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                // Ask the server to check the payment.
                var result = await _paymentService.VerifyPaymentAsync(Reference);

                PaymentSucceeded = result.Succeeded;
                PaymentFailed = !result.Succeeded;

                if (!result.Succeeded)
                {
                    // Paystack's webhook may still confirm this payment a few
                    // seconds later, even if this immediate check sees "pending".
                    // So we don't treat this as final — the booking list should
                    // re-check on refresh.
                    ErrorMessage = "We couldn't confirm the payment yet. Check My Bookings shortly — it may still go through.";
                }
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