using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    internal static class CartMoney
    {
        public static string Format(decimal value) => $"R{value.ToString("0.00", CultureInfo.InvariantCulture)}";
    }

    /// <summary>One row on the Cart screen — wraps a CartItem so Quantity can change on screen.</summary>
    public partial class CartLine : ObservableObject
    {
        public CartItem Item { get; }

        public CartLine(CartItem item)
        {
            Item = item;
            quantity = item.Quantity;
        }

        // Product is joined in by IFirestoreService.GetMyCartAsync. If that
        // product has since been deleted/deactivated it comes back null or
        // inactive — shown as unavailable rather than silently dropped, so
        // the person can see why checkout is blocked and remove it.
        public string Name => Item.Product?.Name ?? "Item no longer available";
        public string? ImageUrl => Item.Product?.ImageUrl;
        public bool HasImage => !string.IsNullOrEmpty(ImageUrl);
        public decimal UnitPrice => Item.Product?.Price ?? 0m;
        public string UnitPriceLabel => CartMoney.Format(UnitPrice);
        public bool IsAvailable => Item.Product is { IsActive: true };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(LineTotal))]
        private int quantity;

        public decimal LineTotal => UnitPrice * Quantity;
    }

    /// <summary>
    /// Backs the Cart screen. Quantity changes and removals write straight
    /// to the cart (direct Firestore, per firestore.rules) and update the
    /// screen optimistically, reverting if the write fails. Prices shown
    /// here are for display only — createOrder re-reads the cart and
    /// re-prices every item server-side, so nothing on this screen can
    /// affect what's actually charged.
    /// </summary>
    public partial class CartViewModel : BaseViewModel
    {
        private readonly IFirestoreService _firestore;
        private readonly IShopService _shopService;
        private bool _updatingQuantity;

        public CartViewModel(IFirestoreService firestore, IShopService shopService)
        {
            _firestore = firestore;
            _shopService = shopService;
            Title = "Cart";
        }

        public ObservableCollection<CartLine> Lines { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SubtotalLabel))]
        [NotifyPropertyChangedFor(nameof(TotalLabel))]
        private decimal subtotal;

        public string SubtotalLabel => CartMoney.Format(Subtotal);

        // createOrder adds no shipping charge, so "Free" is accurate today.
        // If shipping is ever charged, it has to be added in createOrder
        // (server-side) first — changing this label alone would just lie.
        public string ShippingLabel => "Free";

        public string TotalLabel => CartMoney.Format(Subtotal);

        [ObservableProperty]
        private bool isEmpty;

        [ObservableProperty]
        private bool orderPlaced;

        [ObservableProperty]
        private string placedOrderTotalLabel = string.Empty;

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;
            OrderPlaced = false;

            try
            {
                var items = await _firestore.GetMyCartAsync();

                Lines.Clear();
                foreach (var item in items)
                {
                    Lines.Add(new CartLine(item));
                }
                Recalculate();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load your cart. Check your connection and try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task IncreaseAsync(CartLine line)
        {
            var stock = line.Item.Product?.StockQuantity;
            if (stock is not null && line.Quantity + 1 > stock)
            {
                ErrorMessage = $"Only {stock} in stock.";
                return;
            }
            await ChangeQuantityAsync(line, line.Quantity + 1);
        }

        [RelayCommand]
        private async Task DecreaseAsync(CartLine line)
        {
            if (line.Quantity <= 1)
            {
                return; // going below 1 is what "Remove" is for
            }
            await ChangeQuantityAsync(line, line.Quantity - 1);
        }

        [RelayCommand]
        private async Task RemoveAsync(CartLine line)
        {
            ErrorMessage = null;
            try
            {
                await _firestore.RemoveFromCartAsync(line.Item.Id);
                Lines.Remove(line);
                Recalculate();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't remove that item. Please try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
        }

        [RelayCommand]
        private async Task CheckoutAsync()
        {
            if (IsBusy || Lines.Count == 0)
            {
                return;
            }

            if (Lines.Any(l => !l.IsAvailable))
            {
                ErrorMessage = "Remove the unavailable items before checking out.";
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                // No delivery address or notes: the Cart wireframe has no
                // field for either, so none is collected here.
                var result = await _shopService.CreateOrderAsync(null, null);

                PlacedOrderTotalLabel = CartMoney.Format(result.Total);
                Lines.Clear();
                Recalculate();
                OrderPlaced = true;
            }
            catch (InvalidOperationException ex)
            {
                // CallableFunctionClient puts the server's own message after
                // "createOrder failed: " — e.g. 'Not enough stock for "X"
                // (2 left).' — which is already written for the user.
                ErrorMessage = ServerMessage(ex);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't place your order. Check your connection and try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ChangeQuantityAsync(CartLine line, int newQuantity)
        {
            if (_updatingQuantity)
            {
                return; // ignore taps while a write is in flight — avoids out-of-order updates
            }

            _updatingQuantity = true;
            ErrorMessage = null;
            var previous = line.Quantity;

            line.Quantity = newQuantity;
            Recalculate();

            try
            {
                await _firestore.UpdateCartItemQuantityAsync(line.Item.Id, newQuantity);
            }
            catch (Exception ex)
            {
                line.Quantity = previous;
                Recalculate();
                ErrorMessage = "Couldn't update the quantity. Please try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                _updatingQuantity = false;
            }
        }

        private void Recalculate()
        {
            Subtotal = Lines.Where(l => l.IsAvailable).Sum(l => l.LineTotal);
            IsEmpty = Lines.Count == 0;
        }

        private static string ServerMessage(InvalidOperationException ex)
        {
            var message = ex.Message;
            var index = message.IndexOf(": ", StringComparison.Ordinal);
            return index >= 0 ? message[(index + 2)..] : message;
        }
    }
}
