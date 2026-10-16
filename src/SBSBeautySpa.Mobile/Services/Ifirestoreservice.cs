using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    public interface IFirestoreService
    {
        // Services 
        Task<List<Service>> GetServicesAsync(string? categoryId = null, CancellationToken ct = default);
        Task<Service?> GetServiceAsync(string serviceId, CancellationToken ct = default);
        Task<List<Service>> GetServicesByIdsAsync(IEnumerable<string> serviceIds, CancellationToken ct = default);

        // Products 
        Task<List<Product>> GetProductsAsync(string? categoryId = null, CancellationToken ct = default);
        Task<Product?> GetProductAsync(string productId, CancellationToken ct = default);
        Task<List<ProductCategory>> GetProductCategoriesAsync(CancellationToken ct = default);

        //  Cart 
        Task<List<CartItem>> GetMyCartAsync(CancellationToken ct = default);
        Task AddToCartAsync(string productId, int quantity, CancellationToken ct = default);
        Task UpdateCartItemQuantityAsync(string cartItemId, int quantity, CancellationToken ct = default);
        Task RemoveFromCartAsync(string cartItemId, CancellationToken ct = default);
        Task ClearCartAsync(CancellationToken ct = default);

        // Own bookings / orders / notifications 
        Task<List<Booking>> GetMyBookingsAsync(CancellationToken ct = default);
        Task<List<Order>> GetMyOrdersAsync(CancellationToken ct = default);
        Task<List<Notification>> GetMyNotificationsAsync(CancellationToken ct = default);
        Task MarkNotificationReadAsync(string notificationId, CancellationToken ct = default);

        //  Device token (push notifications) 
        Task SaveFcmTokenAsync(string token, CancellationToken ct = default);

        // Reviews 
        Task<List<Review>> GetReviewsForServiceAsync(string serviceId, CancellationToken ct = default);

        //  Gallery 
        Task<List<GalleryCategory>> GetGalleryCategoriesAsync(CancellationToken ct = default);
        Task<List<GalleryImage>> GetGalleryImagesAsync(string? categoryId = null, CancellationToken ct = default);

        //  Service categories
        Task<List<ServiceCategory>> GetServiceCategoriesAsync(CancellationToken ct = default);
    }
}