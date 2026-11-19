using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    // Talks to the checkout Cloud Functions (addresses, Courier Guy prices, order, payment).
    // The server decides every price; this only asks and shows.
    public interface ICheckoutService
    {
        Task<List<CheckoutAddress>> GetAddressesAsync(CancellationToken ct = default);
        Task<string> SaveAddressAsync(CheckoutAddress address, CancellationToken ct = default);
        Task DeleteAddressAsync(string addressId, CancellationToken ct = default);
        Task<List<CheckoutRate>> GetRatesAsync(string addressId, CancellationToken ct = default);
        Task<CheckoutOrderResult> CreateOrderAsync(string addressId, string serviceCode, CancellationToken ct = default);
        Task<CheckoutPaymentInit> InitOrderPaymentAsync(string orderId, CancellationToken ct = default);
        Task<CheckoutPaymentVerify> VerifyPaymentAsync(string reference, CancellationToken ct = default);
        Task<List<PurchaseOrder>> GetMyPurchasesAsync(CancellationToken ct = default);
    }

    public class CheckoutService : ICheckoutService
    {
        private readonly CallableFunctionClient _client;

        public CheckoutService(CallableFunctionClient client) => _client = client;

        public async Task<List<CheckoutAddress>> GetAddressesAsync(CancellationToken ct = default)
        {
            var r = await _client.CallAsync<AddressesResult>("listMyAddresses", new { }, ct);
            return r.Addresses;
        }

        public async Task<string> SaveAddressAsync(CheckoutAddress a, CancellationToken ct = default)
        {
            var r = await _client.CallAsync<SavedResult>("saveMyAddress", new
            {
                id = string.IsNullOrEmpty(a.Id) ? null : a.Id,
                recipientName = a.RecipientName,
                phone = a.Phone,
                addressType = a.AddressType,
                complex = a.Complex,
                streetAddress = a.StreetAddress,
                suburb = a.Suburb,
                city = a.City,
                province = a.Province,
                postalCode = a.PostalCode,
            }, ct);
            return r.AddressId;
        }

        public Task DeleteAddressAsync(string addressId, CancellationToken ct = default) =>
            _client.CallAsync("deleteMyAddress", new { addressId }, ct);

        public async Task<List<CheckoutRate>> GetRatesAsync(string addressId, CancellationToken ct = default)
        {
            var r = await _client.CallAsync<RatesResult>("getDeliveryRates", new { addressId }, ct);
            return r.Rates;
        }

        public Task<CheckoutOrderResult> CreateOrderAsync(string addressId, string serviceCode, CancellationToken ct = default) =>
            _client.CallAsync<CheckoutOrderResult>("createOrder", new { addressId, serviceCode }, ct);

        public Task<CheckoutPaymentInit> InitOrderPaymentAsync(string orderId, CancellationToken ct = default) =>
            _client.CallAsync<CheckoutPaymentInit>("initializePayment", new { orderId }, ct);

        public Task<CheckoutPaymentVerify> VerifyPaymentAsync(string reference, CancellationToken ct = default) =>
            _client.CallAsync<CheckoutPaymentVerify>("verifyPayment", new { reference }, ct);

        public async Task<List<PurchaseOrder>> GetMyPurchasesAsync(CancellationToken ct = default)
        {
            var r = await _client.CallAsync<PurchasesResult>("listMyOrders", new { }, ct);
            return r.Orders;
        }

        private class PurchasesResult { public List<PurchaseOrder> Orders { get; set; } = new(); }
        private class AddressesResult { public List<CheckoutAddress> Addresses { get; set; } = new(); }
        private class SavedResult { public string AddressId { get; set; } = string.Empty; }
        private class RatesResult { public List<CheckoutRate> Rates { get; set; } = new(); }
    }

    // Remembers the choices made while moving through checkout
    // (address -> delivery option -> payment). Registered as a singleton.
    public class CheckoutSession
    {
        public CheckoutAddress? Address { get; set; }
        public CheckoutRate? Rate { get; set; }
        public CheckoutAddress? EditingAddress { get; set; } // null = adding a new one
        public PurchaseOrder? SelectedPurchase { get; set; } // the order whose invoice is open

        public void Reset()
        {
            Address = null;
            Rate = null;
            EditingAddress = null;
        }
    }
}