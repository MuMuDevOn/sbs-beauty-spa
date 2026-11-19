using System.Globalization;
using System.Linq;

namespace SBSBeautySpa.Mobile.Models
{
    // A saved delivery address (stored on the server, only ever the caller's own).
    public class CheckoutAddress
    {
        public string Id { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string AddressType { get; set; } = "residential";
        public string Complex { get; set; } = string.Empty;
        public string StreetAddress { get; set; } = string.Empty;
        public string Suburb { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;

        public string TypeLabel => AddressType == "business" ? "BUSINESS" : "RESIDENTIAL";

        public string AddressLine => string.Join(", ",
            new[] { Complex, StreetAddress, Suburb, City, Province, PostalCode }
                .Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    // One Courier Guy delivery option with its price.
    public class CheckoutRate
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? DeliveryFrom { get; set; }
        public string? DeliveryTo { get; set; }

        public string PriceLabel => $"R{Price.ToString("0.00", CultureInfo.InvariantCulture)}";

        public string DateText
        {
            get
            {
                static string Fmt(string? iso) =>
                    DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                        ? d.ToString("ddd, d MMM", CultureInfo.InvariantCulture)
                        : string.Empty;

                var from = Fmt(DeliveryFrom);
                var to = Fmt(DeliveryTo);
                if (from.Length == 0 && to.Length == 0) return "Delivery date confirmed after payment";
                if (to.Length == 0 || from == to) return $"Estimated delivery: {(from.Length > 0 ? from : to)}";
                if (from.Length == 0) return $"Estimated delivery: {to}";
                return $"Estimated delivery: {from} – {to}";
            }
        }
    }

    public class CheckoutOrderResult
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Total { get; set; }
    }

    public class CheckoutPaymentInit
    {
        public string AuthorizationUrl { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
    }

    public class CheckoutPaymentVerify
    {
        public string Status { get; set; } = string.Empty;
        public string OrderId { get; set; } = string.Empty;
        public bool Succeeded => Status == "success";
        public bool Failed => Status == "failed";
    }

    // One paid shop order, shown in Purchase History and on its invoice.
    public class PurchaseOrder
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public string? CreatedAt { get; set; }
        public decimal Subtotal { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Total { get; set; }
        public string DeliveryServiceName { get; set; } = string.Empty;
        public PurchaseAddress DeliveryAddress { get; set; } = new();
        public List<PurchaseLine> Items { get; set; } = new();

        public string OrderNumber => Id.Length > 8 ? Id[..8].ToUpperInvariant() : Id.ToUpperInvariant();
        public string InvoiceNumber => $"INV-{OrderNumber}";
        public string TotalLabel => $"R{Total.ToString("0.00", CultureInfo.InvariantCulture)}";
        public string SubtotalLabel => $"R{Subtotal.ToString("0.00", CultureInfo.InvariantCulture)}";
        public string ShippingLabel => $"R{ShippingFee.ToString("0.00", CultureInfo.InvariantCulture)}";
        public string ItemsSummary => Items.Sum(i => i.Quantity) == 1 ? "1 item" : $"{Items.Sum(i => i.Quantity)} items";

        public string DateText =>
            DateTimeOffset.TryParse(CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture)
                : string.Empty;

        public string StatusLabel => Status switch
        {
            "confirmed" => "CONFIRMED",
            "fulfilled" => "COMPLETED",
            "cancelled" => "CANCELLED",
            _ => "PROCESSING",
        };

        public string StatusBackground => Status switch
        {
            "fulfilled" => "#E6F6EC",
            "cancelled" => "#FDECEC",
            _ => "#EEF3FD",
        };

        public string StatusColor => Status switch
        {
            "fulfilled" => "#1E7B3A",
            "cancelled" => "#B42318",
            _ => "#2F63E0",
        };

        public string AddressLine => DeliveryAddress.AddressLine;
    }

    public class PurchaseLine
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }

        public string QtyText => $"{Quantity} × R{UnitPrice.ToString("0.00", CultureInfo.InvariantCulture)}";
        public string LineTotalLabel => $"R{LineTotal.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    public class PurchaseAddress
    {
        public string RecipientName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Complex { get; set; } = string.Empty;
        public string StreetAddress { get; set; } = string.Empty;
        public string Suburb { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Province { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;

        public string AddressLine => string.Join(", ",
            new[] { Complex, StreetAddress, Suburb, City, Province, PostalCode }
                .Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}