using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    internal static class CheckoutText
    {
        public static string Money(decimal v) => $"R{v.ToString("0.00", CultureInfo.InvariantCulture)}";

        // CallableFunctionClient throws "functionName failed: <real message>" — show just the real message.
        public static string ServerMessage(InvalidOperationException ex)
        {
            var m = ex.Message;
            var i = m.IndexOf(": ", StringComparison.Ordinal);
            return i >= 0 ? m[(i + 2)..] : m;
        }
    }

    // ------------------------------------------------------------------
    // Step 1: pick (or add / edit / delete) a delivery address
    // ------------------------------------------------------------------
    public partial class DeliveryAddressesViewModel : BaseViewModel
    {
        private readonly ICheckoutService _checkout;
        private readonly CheckoutSession _session;

        public DeliveryAddressesViewModel(ICheckoutService checkout, CheckoutSession session)
        {
            _checkout = checkout;
            _session = session;
            Title = "Delivery Addresses";
        }

        public ObservableCollection<CheckoutAddress> Addresses { get; } = new();

        [ObservableProperty] private bool hasNoAddresses;

        [RelayCommand]
        public async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = null;
            try
            {
                var list = await _checkout.GetAddressesAsync();
                Addresses.Clear();
                foreach (var a in list) Addresses.Add(a);
                HasNoAddresses = Addresses.Count == 0;
            }
            catch (InvalidOperationException ex) { ErrorMessage = CheckoutText.ServerMessage(ex); }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load your addresses. Check your connection and try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task AddAsync()
        {
            _session.EditingAddress = null;
            await Shell.Current.GoToAsync("AddressFormPage");
        }

        [RelayCommand]
        private async Task EditAsync(CheckoutAddress address)
        {
            _session.EditingAddress = address;
            await Shell.Current.GoToAsync("AddressFormPage");
        }

        [RelayCommand]
        private async Task DeleteAsync(CheckoutAddress address)
        {
            var ok = await Shell.Current.DisplayAlertAsync("Delete address", $"Delete the address for {address.RecipientName}?", "Delete", "Cancel");
            if (!ok) return;
            try
            {
                await _checkout.DeleteAddressAsync(address.Id);
                Addresses.Remove(address);
                HasNoAddresses = Addresses.Count == 0;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't delete that address. Please try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        [RelayCommand]
        private async Task DeliverHereAsync(CheckoutAddress address)
        {
            _session.Address = address;
            _session.Rate = null;
            await Shell.Current.GoToAsync("DeliveryOptionsPage");
        }
    }

    // ------------------------------------------------------------------
    // Add / edit one address
    // ------------------------------------------------------------------
    public partial class AddressFormViewModel : BaseViewModel
    {
        private readonly ICheckoutService _checkout;
        private readonly CheckoutSession _session;
        private string _id = string.Empty;

        public AddressFormViewModel(ICheckoutService checkout, CheckoutSession session)
        {
            _checkout = checkout;
            _session = session;
        }

        public string[] Provinces { get; } =
        {
            "Eastern Cape", "Free State", "Gauteng", "KwaZulu-Natal", "Limpopo",
            "Mpumalanga", "Northern Cape", "North West", "Western Cape"
        };

        public string[] AddressTypes { get; } = { "Residential", "Business" };

        [ObservableProperty] private string pageTitle = "New Delivery Address";
        [ObservableProperty] private string recipientName = string.Empty;
        [ObservableProperty] private string phone = string.Empty;
        [ObservableProperty] private string selectedType = "Residential";
        [ObservableProperty] private string complex = string.Empty;
        [ObservableProperty] private string streetAddress = string.Empty;
        [ObservableProperty] private string suburb = string.Empty;
        [ObservableProperty] private string city = string.Empty;
        [ObservableProperty] private string? selectedProvince;
        [ObservableProperty] private string postalCode = string.Empty;

        // Called by the page each time it appears.
        public void Load()
        {
            ErrorMessage = null;
            var a = _session.EditingAddress;
            _id = a?.Id ?? string.Empty;
            PageTitle = a is null ? "New Delivery Address" : "Edit Address";
            RecipientName = a?.RecipientName ?? string.Empty;
            Phone = a?.Phone ?? string.Empty;
            SelectedType = a?.AddressType == "business" ? "Business" : "Residential";
            Complex = a?.Complex ?? string.Empty;
            StreetAddress = a?.StreetAddress ?? string.Empty;
            Suburb = a?.Suburb ?? string.Empty;
            City = a?.City ?? string.Empty;
            SelectedProvince = a?.Province;
            PostalCode = a?.PostalCode ?? string.Empty;
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(RecipientName)) { ErrorMessage = "Please enter the recipient's name."; return; }
            if (string.IsNullOrWhiteSpace(Phone)) { ErrorMessage = "Please enter a phone number."; return; }
            if (string.IsNullOrWhiteSpace(StreetAddress)) { ErrorMessage = "Please enter the street address."; return; }
            if (string.IsNullOrWhiteSpace(Suburb)) { ErrorMessage = "Please enter the suburb."; return; }
            if (string.IsNullOrWhiteSpace(City)) { ErrorMessage = "Please enter the city."; return; }
            if (string.IsNullOrWhiteSpace(SelectedProvince)) { ErrorMessage = "Please choose a province."; return; }
            if (PostalCode.Trim().Length != 4) { ErrorMessage = "Postal code must be 4 digits."; return; }

            IsBusy = true;
            ErrorMessage = null;
            try
            {
                await _checkout.SaveAddressAsync(new CheckoutAddress
                {
                    Id = _id,
                    RecipientName = RecipientName.Trim(),
                    Phone = Phone.Trim(),
                    AddressType = SelectedType == "Business" ? "business" : "residential",
                    Complex = Complex.Trim(),
                    StreetAddress = StreetAddress.Trim(),
                    Suburb = Suburb.Trim(),
                    City = City.Trim(),
                    Province = SelectedProvince!,
                    PostalCode = PostalCode.Trim(),
                });
                _session.EditingAddress = null;
                IsBusy = false;
                await Shell.Current.GoToAsync("..");
            }
            catch (InvalidOperationException ex) { ErrorMessage = CheckoutText.ServerMessage(ex); }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't save the address. Check your connection and try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally { IsBusy = false; }
        }
    }

    // ------------------------------------------------------------------
    // Step 2: choose a Courier Guy delivery option
    // ------------------------------------------------------------------
    public partial class DeliveryOptionsViewModel : BaseViewModel
    {
        private readonly ICheckoutService _checkout;
        private readonly CheckoutSession _session;

        public DeliveryOptionsViewModel(ICheckoutService checkout, CheckoutSession session)
        {
            _checkout = checkout;
            _session = session;
            Title = "Delivery Options";
        }

        public ObservableCollection<CheckoutRate> Rates { get; } = new();

        [ObservableProperty] private string recipientText = string.Empty;
        [ObservableProperty] private string addressText = string.Empty;
        [ObservableProperty] private bool hasNoRates;

        [RelayCommand]
        public async Task LoadAsync()
        {
            var address = _session.Address;
            if (address is null) { ErrorMessage = "Please choose a delivery address first."; return; }
            if (IsBusy) return;

            RecipientText = address.RecipientName;
            AddressText = address.AddressLine;

            IsBusy = true;
            ErrorMessage = null;
            HasNoRates = false;
            Rates.Clear();
            try
            {
                var rates = await _checkout.GetRatesAsync(address.Id);
                foreach (var r in rates.OrderBy(r => r.Price)) Rates.Add(r);
                HasNoRates = Rates.Count == 0;
            }
            catch (InvalidOperationException ex) { ErrorMessage = CheckoutText.ServerMessage(ex); }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't get delivery prices. Check your connection and try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task ChooseAsync(CheckoutRate rate)
        {
            _session.Rate = rate;
            await Shell.Current.GoToAsync("OrderPaymentPage");
        }
    }

    // ------------------------------------------------------------------
    // Step 3: review + pay with Paystack
    // ------------------------------------------------------------------
    public partial class OrderPaymentViewModel : BaseViewModel
    {
        private readonly ICheckoutService _checkout;
        private readonly IFirestoreService _firestore;
        private readonly CheckoutSession _session;
        private string? _orderId;
        private CancellationTokenSource? _pollCts;

        public OrderPaymentViewModel(ICheckoutService checkout, IFirestoreService firestore, CheckoutSession session)
        {
            _checkout = checkout;
            _firestore = firestore;
            _session = session;
            Title = "Payment";
        }

        [ObservableProperty] private string recipientText = string.Empty;
        [ObservableProperty] private string addressText = string.Empty;
        [ObservableProperty] private string deliveryName = string.Empty;
        [ObservableProperty] private string deliveryDate = string.Empty;
        [ObservableProperty] private string itemsText = string.Empty;
        [ObservableProperty] private string subtotalLabel = "R0.00";
        [ObservableProperty] private string shippingLabel = "R0.00";
        [ObservableProperty] private string totalLabel = "R0.00";
        [ObservableProperty] private string payButtonText = "Pay securely";
        [ObservableProperty] private string? statusText;

        private decimal _subtotal;

        public async Task LoadAsync()
        {
            var address = _session.Address;
            var rate = _session.Rate;
            if (address is null || rate is null) { ErrorMessage = "Please choose a delivery option first."; return; }

            RecipientText = address.RecipientName;
            AddressText = address.AddressLine;
            DeliveryName = rate.Name;
            DeliveryDate = rate.DateText;
            ShippingLabel = rate.PriceLabel;

            try
            {
                var cart = await _firestore.GetMyCartAsync();
                _subtotal = cart.Where(c => c.Product is { IsActive: true }).Sum(c => c.Product!.Price * c.Quantity);
                var count = cart.Sum(c => c.Quantity);
                ItemsText = count == 1 ? "1 item" : $"{count} items";
                SubtotalLabel = CheckoutText.Money(_subtotal);
                var total = _subtotal + rate.Price;
                TotalLabel = CheckoutText.Money(total);
                PayButtonText = $"Pay {CheckoutText.Money(total)} with Paystack";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex);
                ErrorMessage = "Couldn't load your order summary.";
            }
        }

        public void CancelPolling() => _pollCts?.Cancel();

        [RelayCommand]
        private async Task PayAsync()
        {
            var address = _session.Address;
            var rate = _session.Rate;
            if (IsBusy || address is null || rate is null) return;

            IsBusy = true;
            ErrorMessage = null;
            StatusText = null;
            try
            {
                // The server re-prices everything; use ITS numbers from here on.
                if (_orderId is null)
                {
                    var order = await _checkout.CreateOrderAsync(address.Id, rate.Code);
                    _orderId = order.OrderId;
                    SubtotalLabel = CheckoutText.Money(order.Subtotal);
                    ShippingLabel = CheckoutText.Money(order.ShippingFee);
                    TotalLabel = CheckoutText.Money(order.Total);
                    PayButtonText = $"Pay {CheckoutText.Money(order.Total)} with Paystack";
                }

                var init = await _checkout.InitOrderPaymentAsync(_orderId);
                await Browser.Default.OpenAsync(init.AuthorizationUrl, BrowserLaunchMode.SystemPreferred);

                StatusText = "Waiting for your payment to be confirmed…";
                var paid = await WaitForPaymentAsync(init.Reference);

                if (paid)
                {
                    var orderId = _orderId;
                    _session.Reset();
                    await Shell.Current.GoToAsync("OrderConfirmationPage", new ShellNavigationQueryParameters
                    {
                        ["OrderId"] = orderId,
                        ["Total"] = TotalLabel,
                        ["Reference"] = init.Reference,
                        ["Delivery"] = $"{rate.Name} · {rate.DateText}",
                        ["Address"] = $"{address.RecipientName}, {address.AddressLine}",
                    });
                }
                else
                {
                    StatusText = null;
                    ErrorMessage = "We haven't received your payment yet. If you didn't finish paying, tap the button to try again.";
                }
            }
            catch (InvalidOperationException ex) { StatusText = null; ErrorMessage = CheckoutText.ServerMessage(ex); }
            catch (Exception ex)
            {
                StatusText = null;
                ErrorMessage = "Something went wrong starting your payment. Please try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally { IsBusy = false; }
        }

        // The server confirms with Paystack; we just ask every few seconds (up to 5 minutes).
        private async Task<bool> WaitForPaymentAsync(string reference)
        {
            _pollCts?.Cancel();
            _pollCts = new CancellationTokenSource();
            var token = _pollCts.Token;
            var deadline = DateTime.UtcNow.AddMinutes(5);

            while (DateTime.UtcNow < deadline && !token.IsCancellationRequested)
            {
                try { await Task.Delay(TimeSpan.FromSeconds(3), token); }
                catch (OperationCanceledException) { return false; }

                try
                {
                    var result = await _checkout.VerifyPaymentAsync(reference, token);
                    if (result.Succeeded) return true;
                    if (result.Failed) return false;
                }
                catch (OperationCanceledException) { return false; }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); } // transient — keep polling
            }
            return false;
        }
    }

    // ------------------------------------------------------------------
    // Step 4: confirmation
    // ------------------------------------------------------------------
    public partial class OrderConfirmationViewModel : ObservableObject, IQueryAttributable
    {
        [ObservableProperty] private string orderNumber = string.Empty;
        [ObservableProperty] private string totalPaid = string.Empty;
        [ObservableProperty] private string delivery = string.Empty;
        [ObservableProperty] private string address = string.Empty;
        [ObservableProperty] private string reference = string.Empty;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            string Text(string key) => query.TryGetValue(key, out var v) ? v?.ToString() ?? string.Empty : string.Empty;
            var id = Text("OrderId");
            OrderNumber = id.Length > 8 ? id[..8].ToUpperInvariant() : id.ToUpperInvariant();
            TotalPaid = Text("Total");
            Delivery = Text("Delivery");
            Address = Text("Address");
            Reference = Text("Reference");
        }
    }

    // ------------------------------------------------------------------
    // Purchase history + invoice
    // ------------------------------------------------------------------
    public partial class PurchaseHistoryViewModel : BaseViewModel
    {
        private readonly ICheckoutService _checkout;
        private readonly CheckoutSession _session;

        public PurchaseHistoryViewModel(ICheckoutService checkout, CheckoutSession session)
        {
            _checkout = checkout;
            _session = session;
            Title = "Purchase History";
        }

        public ObservableCollection<PurchaseOrder> Orders { get; } = new();

        [ObservableProperty] private bool hasNoOrders;

        [RelayCommand]
        public async Task LoadAsync()
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = null;
            try
            {
                var list = await _checkout.GetMyPurchasesAsync();
                Orders.Clear();
                foreach (var o in list) Orders.Add(o);
                HasNoOrders = Orders.Count == 0;
            }
            catch (InvalidOperationException ex) { ErrorMessage = CheckoutText.ServerMessage(ex); }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load your purchases. Check your connection and try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task OpenInvoiceAsync(PurchaseOrder order)
        {
            _session.SelectedPurchase = order;
            await Shell.Current.GoToAsync("InvoicePage");
        }
    }

    public partial class InvoiceViewModel : ObservableObject
    {
        public InvoiceViewModel(CheckoutSession session) => Order = session.SelectedPurchase ?? new PurchaseOrder();

        public PurchaseOrder Order { get; }
    }
}