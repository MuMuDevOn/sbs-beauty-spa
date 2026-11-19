using System.Globalization;
using NodaTime;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class BookingReviewPage : ContentPage, IQueryAttributable
    {
        private readonly IAvailabilityService _availability;
        private readonly IPaymentService _payments;

        // Everything is kept so it can be handed on to the confirmation page.
        private readonly Dictionary<string, object> _details = new();

        // Created once, so tapping Confirm again after a failed payment never double-books.
        private string? _bookingId;
        private CancellationTokenSource? _pollCts;

        // What the server will charge: the service price and its deposit.
        private decimal _basePrice;
        private decimal _deposit;
        private bool _hasAddOns;
        private bool PayFull => FullAmountRadio.IsChecked;

        public BookingReviewPage(IAvailabilityService availability, IPaymentService payments)
        {
            InitializeComponent();
            _availability = availability;
            _payments = payments;
        }

        // Receives everything chosen on the previous pages.
        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            foreach (var kv in query) _details[kv.Key] = kv.Value;

            string Text(string key) => query.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
            decimal Money(string key) => query.TryGetValue(key, out var v) && v is decimal d ? d : 0m;

            var name = Text("ServiceName");
            var date = Text("DateText");
            var time = Text("TimeText");
            var duration = Text("DurationText");
            var addOns = Text("AddOns");

            var addOnsTotal = Money("AddOnsTotal");
            var ranged = query.TryGetValue("AddOnsRanged", out var rg) && rg is bool b && b;
            var price = Money("Price") + addOnsTotal;
            var deposit = Money("Deposit");
            var dueLater = price - deposit;
            if (dueLater < 0) dueLater = 0;

            ServiceNameLabel.Text = string.IsNullOrEmpty(name) ? "Your service" : name;

            var when = $"{date} • {time}";
            if (!string.IsNullOrEmpty(duration)) when += $" ({duration})";
            WhenLabel.Text = when;

            AddOnsLabel.Text = string.IsNullOrEmpty(addOns) ? "Add-Ons: none" : $"Add-Ons: {addOns}";

            SubtotalLabel.Text = $"Subtotal: {(ranged ? "from " : "")}{Rand(price)}";
            DepositLabel.Text = $"Deposit Required Now: {Rand(deposit)}";
            TotalDueLabel.Text = $"Total Due at Appointment: {(ranged ? "from " : "")}{Rand(dueLater)}";
            _basePrice = Money("Price");
            _deposit = deposit;
            _hasAddOns = !string.IsNullOrEmpty(addOns);
            UpdateButton();
        }

        private void OnPayOptionChanged(object? sender, CheckedChangedEventArgs e) => UpdateButton();

        // The button shows exactly what will be charged now.
        private void UpdateButton()
        {
            ConfirmButton.Text = PayFull
                ? $"Confirm and Pay Full Amount ({Rand(_basePrice)})"
                : $"Confirm and Pay Deposit ({Rand(_deposit)})";
        }

        private static string Rand(decimal amount) => "R" + amount.ToString("0.00", CultureInfo.InvariantCulture);

        private async void OnPolicyTapped(object? sender, TappedEventArgs e)
            => await LegalNavigator.OpenAsync("cancellation");

        private async void OnBackTapped(object sender, TappedEventArgs e)
        {
            _pollCts?.Cancel();
            await Shell.Current.GoToAsync("..");
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _pollCts?.Cancel();
        }

        // Asks the server every few seconds whether Paystack has confirmed the payment.
        private async Task<bool> WaitForPaymentAsync(string reference, TimeSpan timeout, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(3000, ct);
                try
                {
                    var result = await _payments.VerifyPaymentAsync(reference, ct);
                    if (result.Succeeded) return true;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex);   // a hiccup; try again
                }
            }
            return false;
        }

        // Confirm: create the booking, open Paystack, then verify the payment.
        private async void OnConfirmClicked(object sender, EventArgs e)
        {
            if (!PolicyCheckBox.IsChecked)
            {
                await DisplayAlertAsync("Policy Required", "Please agree to the cancellation policy.", "OK");
                return;
            }

            var originalText = ConfirmButton.Text;
            ConfirmButton.IsEnabled = false;
            ConfirmButton.Text = "Please wait...";

            try
            {
                // 1. Create the booking (status "pending") if we haven't already.
                if (_bookingId is null)
                {
                    if (!_details.TryGetValue("ServiceId", out var sid) || string.IsNullOrEmpty(sid?.ToString())
                        || _details.GetValueOrDefault("SelectedDate") is not LocalDate date
                        || _details.GetValueOrDefault("SelectedStartTime") is not LocalTime start)
                    {
                        await DisplayAlertAsync("Booking", "Something is missing from your booking. Please go back and pick the date and time again.", "OK");
                        return;
                    }

                    var addOns = _details.GetValueOrDefault("AddOns")?.ToString();
                    var notes = string.IsNullOrWhiteSpace(addOns) ? null : $"Add-ons: {addOns}";

                    _bookingId = await _availability.CreateBookingAsync(new[] { sid!.ToString()! }, date, start, notes);
                }

                // 2. Ask the server to start a Paystack payment, and open it.
                var payFull = PayFull;   // fixed now, so changing the option mid-payment can't matter
                var init = await _payments.InitializePaymentAsync(_bookingId, payFull ? PaymentRequestType.Full : PaymentRequestType.Deposit);
                await Browser.Default.OpenAsync(init.AuthorizationUrl, BrowserLaunchMode.SystemPreferred);

                // 3. Wait for the SERVER to confirm the payment with Paystack.
                //    Nothing the client taps can confirm a booking: we just keep asking
                //    the server until Paystack says the money arrived.
                ConfirmButton.Text = "Waiting for payment...";
                _pollCts = new CancellationTokenSource();
                var confirmed = await WaitForPaymentAsync(init.Reference, TimeSpan.FromMinutes(5), _pollCts.Token);

                if (confirmed)
                {
                    var args = new ShellNavigationQueryParameters();
                    foreach (var kv in _details) args[kv.Key] = kv.Value;
                    args["BookingId"] = _bookingId;
                    args["PaidType"] = payFull ? "full" : "deposit";
                    args["PaidAmount"] = payFull ? _basePrice : _deposit;
                    args["BalanceDue"] = payFull ? 0m : Math.Max(_basePrice - _deposit, 0m);
                    args["Reference"] = _bookingId.Length > 8 ? _bookingId[..8].ToUpperInvariant() : _bookingId.ToUpperInvariant();
                    await Shell.Current.GoToAsync("BookingConfirmationPage", args);
                }
                else
                {
                    await DisplayAlertAsync("Payment not received",
                        "We haven't received your payment. If you did pay, it will confirm automatically and show under Booking history. Otherwise tap Confirm to try again.", "OK");
                }
            }
            catch (OperationCanceledException)
            {
                // The client left the page while waiting; nothing to do.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                await DisplayAlertAsync("Couldn't complete booking", ex.Message, "OK");
            }
            finally
            {
                ConfirmButton.IsEnabled = true;
                ConfirmButton.Text = originalText;
            }
        }
    }
}