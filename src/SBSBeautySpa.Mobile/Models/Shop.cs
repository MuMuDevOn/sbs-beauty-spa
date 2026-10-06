using CommunityToolkit.Mvvm.ComponentModel;

namespace SBSBeautySpa.Mobile.Models
{
    // Represents a product category (like "Press-On Nails", "Kits").
    // Products point to a category via Product.CategoryId.
    public class ProductCategory
    {
        // The unique ID of this category.
        public string Id { get; set; } = string.Empty;

        // The category name shown to users.
        public string Name { get; set; } = string.Empty;

        // Whether this category is currently shown.
        public bool IsActive { get; set; } = true;
    }

    // Represents a product sold in the shop.
    //
    // IsActive is observable (the rest are plain properties) for the
    // same reason as Service.IsActive — when an admin toggles a product
    // on or off, we want the UI to update instantly without reloading.
    public partial class Product : ObservableObject
    {
        // The unique ID of this product.
        public string Id { get; set; } = string.Empty;

        // Which category this product belongs to.
        public string CategoryId { get; set; } = string.Empty;

        // The product name shown to clients.
        public string Name { get; set; } = string.Empty;

        // A short description of the product.
        public string Description { get; set; } = string.Empty;

        // The price of one unit.
        public decimal Price { get; set; }

        // How many are in stock.
        // Null = not tracked (treated as unlimited).
        // A number = actual stock on hand.
        // When an order is placed, this is reduced.
        public int? StockQuantity { get; set; }

        // Optional image URL for the product.
        public string? ImageUrl { get; set; }

        // Whether this product is currently sold.
        // This is observable so the UI updates when it changes.
        [ObservableProperty]
        private bool isActive = true;
    }

    // Represents one item in a client's cart.
    //
    // The cart IS a collection of these — there's no separate "cart"
    // table or document. It's just "what's in my basket right now."
    //
    // Read/written directly via Firestore (not a Cloud Function) because
    // nothing trust-sensitive happens until checkout.
    public class CartItem
    {
        // The unique ID of this cart item.
        public string Id { get; set; } = string.Empty;

        // Which client's cart this belongs to.
        public string ClientId { get; set; } = string.Empty;

        // Which product is in the cart.
        public string ProductId { get; set; } = string.Empty;

        // How many of this product.
        public int Quantity { get; set; } = 1;

        // The full product details, loaded separately for display.
        // This is NOT stored in the cart item itself — it's filled in
        // when the app loads the cart.
        public Product? Product { get; set; }
    }

    // The possible states an order can be in.
    public enum OrderStatus
    {
        Pending,     // Just placed, not yet confirmed
        Confirmed,   // Confirmed by admin
        Fulfilled,   // Delivered/shipped to client
        Cancelled    // Order was cancelled
    }

    // One priced line of an order.
    // This is a SNAPSHOT at checkout time — the price at that moment.
    // (If the product's price changes later, this line item keeps the old price.)
    public class OrderLineItem
    {
        // The product's ID.
        public string ProductId { get; set; } = string.Empty;

        // The product's name at checkout time.
        public string Name { get; set; } = string.Empty;

        // The price per unit at checkout time.
        public decimal UnitPrice { get; set; }

        // How many were ordered.
        public int Quantity { get; set; }

        // Unit price × quantity.
        public decimal LineTotal { get; set; }
    }

    // Represents one order placed by a client.
    //
    // Orders are only created by the server (via shop.js's createOrder),
    // which re-prices everything server-side. The client can't just
    // create an order directly — the server does it.
    public class Order
    {
        // The unique ID of this order.
        public string Id { get; set; } = string.Empty;

        // Which client placed the order.
        public string ClientId { get; set; } = string.Empty;

        // The line items in this order.
        public List<OrderLineItem> Items { get; set; } = new();

        // The total price of the order.
        public decimal Total { get; set; }

        // Where to deliver the order.
        public string? DeliveryAddress { get; set; }

        // Any notes from the client.
        public string? Notes { get; set; }

        // The current status of this order.
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        // When the order was placed.
        public DateTime? CreatedAt { get; set; }
    }
}