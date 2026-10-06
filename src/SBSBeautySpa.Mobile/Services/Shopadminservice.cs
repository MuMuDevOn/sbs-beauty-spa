using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    // Everything you can edit about a product.
    // Used when creating or updating a product.
    public record ProductInput(string Name, string Description, string CategoryId, decimal Price, int? StockQuantity, string? ImageUrl);

    // The contract for the shop admin service.
    public interface IShopAdminService
    {
        Task<string> CreateProductCategoryAsync(string name, CancellationToken ct = default);
        Task SetProductCategoryActiveAsync(string categoryId, bool isActive, CancellationToken ct = default);

        Task<string> CreateProductAsync(ProductInput input, CancellationToken ct = default);
        Task UpdateProductAsync(string productId, ProductInput input, CancellationToken ct = default);
        Task SetProductActiveAsync(string productId, bool isActive, CancellationToken ct = default);
        Task DeleteProductAsync(string productId, CancellationToken ct = default);

        Task UpdateOrderStatusAsync(string orderId, OrderStatus newStatus, CancellationToken ct = default);
        Task<List<Order>> ListAllOrdersAsync(OrderStatus? status = null, CancellationToken ct = default);
    }

    // This class talks to the server about shop management (admin only).
    public class ShopAdminService : IShopAdminService
    {
        private readonly CallableFunctionClient _client;

        public ShopAdminService(CallableFunctionClient client) => _client = client;

        // Creates a new product category.
        public async Task<string> CreateProductCategoryAsync(string name, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<IdResult>("createProductCategory", new { name }, ct);
            return result.CategoryId;
        }

        // Turns a product category on or off.
        public Task SetProductCategoryActiveAsync(string categoryId, bool isActive, CancellationToken ct = default) =>
            _client.CallAsync("setProductCategoryActive", new { categoryId, isActive }, ct);

        // Creates a new product.
        public async Task<string> CreateProductAsync(ProductInput input, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<IdResult>("createProduct", ToPayload(input), ct);
            return result.ProductId;
        }

        // Updates an existing product.
        public Task UpdateProductAsync(string productId, ProductInput input, CancellationToken ct = default)
        {
            var payload = new
            {
                productId,
                name = input.Name,
                description = input.Description,
                categoryId = input.CategoryId,
                price = input.Price,
                stockQuantity = input.StockQuantity,
                imageUrl = input.ImageUrl,
            };
            return _client.CallAsync("updateProduct", payload, ct);
        }

        // Turns a product on or off.
        public Task SetProductActiveAsync(string productId, bool isActive, CancellationToken ct = default) =>
            _client.CallAsync("setProductActive", new { productId, isActive }, ct);

        // Deletes a product.
        public Task DeleteProductAsync(string productId, CancellationToken ct = default) =>
            _client.CallAsync("deleteProduct", new { productId }, ct);

        // Changes an order's status.
        public Task UpdateOrderStatusAsync(string orderId, OrderStatus newStatus, CancellationToken ct = default) =>
            _client.CallAsync("updateOrderStatus", new { orderId, newStatus = newStatus.ToString().ToLowerInvariant() }, ct);

        // Lists all orders (optionally filtered by status).
        public async Task<List<Order>> ListAllOrdersAsync(OrderStatus? status = null, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<OrdersResult>("listAllOrders", new { status = status?.ToString().ToLowerInvariant() }, ct);
            return result.Orders;
        }

        // Converts the ProductInput record into the shape the server expects.
        private static object ToPayload(ProductInput input) => new
        {
            name = input.Name,
            description = input.Description,
            categoryId = input.CategoryId,
            price = input.Price,
            stockQuantity = input.StockQuantity,
            imageUrl = input.ImageUrl,
        };

        // Small helper classes to match the server's response shape.
        private class IdResult
        {
            public string CategoryId { get; set; } = string.Empty;
            public string ProductId { get; set; } = string.Empty;
        }

        private class OrdersResult { public List<Order> Orders { get; set; } = new(); }
    }
}