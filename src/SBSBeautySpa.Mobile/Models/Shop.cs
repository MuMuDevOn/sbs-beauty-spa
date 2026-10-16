using CommunityToolkit.Mvvm.ComponentModel;

namespace SBSBeautySpa.Mobile.Models
{
    public class ProductCategory
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public partial class Product : ObservableObject
    {
        public string Id { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int? StockQuantity { get; set; }
        public string? ImageUrl { get; set; }

        public bool IsInStock => StockQuantity is null or > 0;
        public bool HasImage => !string.IsNullOrEmpty(ImageUrl);
        public string PriceLabel => $"R{Price:0.00}";

        [ObservableProperty]
        private bool isActive = true;
    }

    public class CartItem
    {
        public string Id { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public Product? Product { get; set; }
    }

    public enum OrderStatus
    {
        Pending,
        Confirmed,
        Fulfilled,
        Cancelled
    }

    public class OrderLineItem
    {
        public string ProductId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class Order
    {
        public string Id { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public List<OrderLineItem> Items { get; set; } = new();
        public decimal Total { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? Notes { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public DateTime? CreatedAt { get; set; }
    }
}