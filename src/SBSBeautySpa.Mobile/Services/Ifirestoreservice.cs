using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    // This interface says what a Firestore service must be able to do.
    //
    // IMPORTANT: This is NOT the whole FirestoreService — it's only the
    // part that ServicesViewModel needs.
    //
    // Your real FirestoreService.cs probably already has more methods
    // (gallery, profile, etc.). That's fine — just make sure it also
    // implements this interface, so it can be plugged in here.
    //
    // Why direct Firestore calls (no Cloud Function)?
    // Because this is all PUBLIC data (service catalog, product list, etc.).
    // It's low risk and doesn't need the extra security of a server function.
    // Only trust-sensitive things (payments, bookings, availability) go
    // through Cloud Functions.
    public interface IFirestoreService
    {
        // ---- Services (public catalog) ----

        // Gets all services, optionally filtered by category.
        // Pass null for categoryId to get all services.
        Task<List<Service>> GetServicesAsync(string? categoryId = null, CancellationToken ct = default);

        // Gets one service by its ID.
        Task<Service?> GetServiceAsync(string serviceId, CancellationToken ct = default);

        // Gets several services in one call.
        // Used by the Add-Ons screen and Review Booking screen to load
        // the main service plus all add-ons at once.
        Task<List<Service>> GetServicesByIdsAsync(IEnumerable<string> serviceIds, CancellationToken ct = default);

        // ---- Products (public catalog, same trust level as services) ----

        // Gets all products, optionally filtered by category.
        Task<List<Product>> GetProductsAsync(string? categoryId = null, CancellationToken ct = default);

        // Gets one product by its ID.
        Task<Product?> GetProductAsync(string productId, CancellationToken ct = default);

        // ---- Cart (own data — direct read/write allowed by security rules) ----
        // Nothing here is trust-sensitive until the user actually checks out.

        // Gets the current user's cart.
        Task<List<CartItem>> GetMyCartAsync(CancellationToken ct = default);

        // Adds a product to the cart.
        Task AddToCartAsync(string productId, int quantity, CancellationToken ct = default);

        // Changes the quantity of an item in the cart.
        Task UpdateCartItemQuantityAsync(string cartItemId, int quantity, CancellationToken ct = default);

        // Removes an item from the cart.
        Task RemoveFromCartAsync(string cartItemId, CancellationToken ct = default);

        // Empties the whole cart.
        Task ClearCartAsync(CancellationToken ct = default);

        // ---- Own bookings, orders, and notifications ----
        // The security rules only let a user read their own records.

        // Gets the current user's bookings.
        Task<List<Booking>> GetMyBookingsAsync(CancellationToken ct = default);

        // Gets the current user's orders.
        Task<List<Order>> GetMyOrdersAsync(CancellationToken ct = default);

        // Gets the current user's notifications.
        Task<List<Notification>> GetMyNotificationsAsync(CancellationToken ct = default);

        // Marks a notification as read.
        Task MarkNotificationReadAsync(string notificationId, CancellationToken ct = default);

        // ---- Reviews (public read) ----

        // Gets all reviews for one service.
        Task<List<Review>> GetReviewsForServiceAsync(string serviceId, CancellationToken ct = default);

        // ---- Gallery (public read) ----

        // Gets all gallery categories.
        Task<List<GalleryCategory>> GetGalleryCategoriesAsync(CancellationToken ct = default);

        // Gets gallery images, optionally filtered by category.
        Task<List<GalleryImage>> GetGalleryImagesAsync(string? categoryId = null, CancellationToken ct = default);

        // ---- Service categories (public read) ----

        // Gets all service categories.
        Task<List<ServiceCategory>> GetServiceCategoriesAsync(CancellationToken ct = default);
    }
}