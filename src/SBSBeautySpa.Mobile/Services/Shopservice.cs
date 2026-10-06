namespace SBSBeautySpa.Mobile.Services
{
    // What the server sends back after a successful checkout.
    // OrderId = the new order's ID
    // Total = the final total the server calculated
    public record CheckoutResult(string OrderId, decimal Total);

    // The contract for the shop service (client-side checkout).
    public interface IShopService
    {
        // Checks out whatever is currently in the caller's own cart.
        //
        // IMPORTANT: There's NO "items" parameter here on purpose.
        // The server reads straight from the user's cartItems in Firestore,
        // so the client can't send fake items or prices.
        //
        // All it sends is the delivery address and any notes.
        Task<CheckoutResult> CreateOrderAsync(string? deliveryAddress, string? notes, CancellationToken ct = default);
    }

    // This class handles the client-side checkout.
    public class ShopService : IShopService
    {
        private readonly CallableFunctionClient _client;

        public ShopService(CallableFunctionClient client) => _client = client;

        // Sends the checkout request to the server.
        // The server does all the real work (reading the cart, checking
        // stock, calculating the total, writing the order).
        public Task<CheckoutResult> CreateOrderAsync(string? deliveryAddress, string? notes, CancellationToken ct = default) =>
            _client.CallAsync<CheckoutResult>("createOrder", new { deliveryAddress, notes }, ct);
    }
}