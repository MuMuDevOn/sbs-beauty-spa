using System.Net.Http.Json;
using System.Text.Json;
using NodaTime;
using SBSBeautySpa.Mobile.Models;

namespace SBSBeautySpa.Mobile.Services
{
    public class FirestoreService : IFirestoreService
    {
        private readonly HttpClient _http;
        private readonly IFirebaseAuthService _auth;

        private const string BaseUrl = "https://firestore.googleapis.com/v1";
        private static readonly string DocumentsPath =
            $"projects/{FirebaseConfig.ProjectId}/databases/(default)/documents";

        public FirestoreService(HttpClient http, IFirebaseAuthService auth)
        {
            _http = http;
            _auth = auth;
        }


        // SERVICES


        public async Task<List<Service>> GetServicesAsync(string? categoryId = null, CancellationToken ct = default)
        {
            var docs = await QueryCollectionAsync("services", ct);
            var services = docs.Select(MapService).Where(s => s.IsActive);

            if (categoryId is not null)
                services = services.Where(s => s.CategoryId == categoryId);

            return services.ToList();
        }

        public async Task<Service?> GetServiceAsync(string serviceId, CancellationToken ct = default)
        {
            var doc = await GetDocumentAsync($"services/{serviceId}", ct);
            return doc is null ? null : MapService(doc.Value);
        }

        public async Task<List<Service>> GetServicesByIdsAsync(IEnumerable<string> serviceIds, CancellationToken ct = default)
        {
            var tasks = serviceIds.Select(id => GetServiceAsync(id, ct));
            var services = await Task.WhenAll(tasks);
            return services.Where(s => s is not null).Cast<Service>().ToList();
        }


        // PRODUCTS


        public async Task<List<Product>> GetProductsAsync(string? categoryId = null, CancellationToken ct = default)
        {
            var docs = await QueryCollectionAsync("products", ct);
            var products = docs.Select(MapProduct).Where(p => p.IsActive);

            if (categoryId is not null)
                products = products.Where(p => p.CategoryId == categoryId);

            return products.ToList();
        }

        public async Task<Product?> GetProductAsync(string productId, CancellationToken ct = default)
        {
            var doc = await GetDocumentAsync($"products/{productId}", ct);
            return doc is null ? null : MapProduct(doc.Value);
        }

        public async Task<List<ProductCategory>> GetProductCategoriesAsync(CancellationToken ct = default)
        {
            var docs = await QueryCollectionAsync("productCategories", ct);
            return docs.Select(MapProductCategory)
                .Where(c => c.IsActive)
                .ToList();
        }

        private static ProductCategory MapProductCategory(JsonElement doc)
        {
            var fields = doc.GetProperty("fields");
            return new ProductCategory
            {
                Id = GetDocId(doc),
                Name = GetString(fields, "name"),
                IsActive = GetBool(fields, "isActive")
            };
        }


        // CART (own data — filtered by clientId)


        public async Task<List<CartItem>> GetMyCartAsync(CancellationToken ct = default)
        {
            var uid = _auth.CurrentUserId;
            if (string.IsNullOrEmpty(uid)) return new List<CartItem>();

            var docs = await QueryCollectionAsync("cartItems", ct);
            return docs.Select(MapCartItem)
                .Where(c => c.ClientId == uid)
                .ToList();
        }

        public async Task AddToCartAsync(string productId, int quantity, CancellationToken ct = default)
        {
            var uid = _auth.CurrentUserId;
            if (string.IsNullOrEmpty(uid)) throw new InvalidOperationException("Must be signed in to add to cart.");

            var existing = (await GetMyCartAsync(ct)).FirstOrDefault(c => c.ProductId == productId);
            if (existing is not null)
            {
                await UpdateCartItemQuantityAsync(existing.Id, existing.Quantity + quantity, ct);
                return;
            }

            var doc = new
            {
                fields = new
                {
                    clientId = new { stringValue = uid },
                    productId = new { stringValue = productId },
                    quantity = new { integerValue = quantity.ToString() },
                    addedAt = new { timestampValue = DateTime.UtcNow.ToString("o") }
                }
            };
            await PostDocumentAsync("cartItems", doc, ct);
        }

        public async Task UpdateCartItemQuantityAsync(string cartItemId, int quantity, CancellationToken ct = default)
        {
            var patch = new
            {
                fields = new { quantity = new { integerValue = quantity.ToString() } }
            };
            await PatchDocumentAsync($"cartItems/{cartItemId}", patch, ct, updateMask: "quantity");
        }

        public async Task RemoveFromCartAsync(string cartItemId, CancellationToken ct = default)
        {
            await DeleteDocumentAsync($"cartItems/{cartItemId}", ct);
        }

        public async Task ClearCartAsync(CancellationToken ct = default)
        {
            var items = await GetMyCartAsync(ct);
            foreach (var item in items)
                await DeleteDocumentAsync($"cartItems/{item.Id}", ct);
        }


        // OWN BOOKINGS / ORDERS / NOTIFICATIONS


        public async Task<List<Booking>> GetMyBookingsAsync(CancellationToken ct = default)
        {
            var uid = _auth.CurrentUserId;
            if (string.IsNullOrEmpty(uid)) return new List<Booking>();

            var docs = await QueryCollectionAsync("bookings", ct);
            return docs.Select(MapBooking)
                .Where(b => b.ClientId == uid)
                .ToList();
        }

        public async Task<List<Order>> GetMyOrdersAsync(CancellationToken ct = default)
        {
            var uid = _auth.CurrentUserId;
            if (string.IsNullOrEmpty(uid)) return new List<Order>();

            var docs = await QueryCollectionAsync("orders", ct);
            return docs.Select(MapOrder)
                .Where(o => o.ClientId == uid)
                .ToList();
        }

        public async Task<List<Notification>> GetMyNotificationsAsync(CancellationToken ct = default)
        {
            var uid = _auth.CurrentUserId;
            if (string.IsNullOrEmpty(uid)) return new List<Notification>();

            var docs = await QueryCollectionAsync("notifications", ct);
            return docs.Select(MapNotification)
                .Where(n => n.ClientId == uid)
                .ToList();
        }

        public async Task MarkNotificationReadAsync(string notificationId, CancellationToken ct = default)
        {
            var patch = new
            {
                fields = new { isRead = new { booleanValue = true } }
            };
            await PatchDocumentAsync($"notifications/{notificationId}", patch, ct, updateMask: "isRead");
        }


        // DEVICE TOKEN (push notifications)


        // Writes this device's FCM token to users/{uid}.fcmToken so the
        // Cloud Function sendPush() can find it and deliver push notifications.
        public async Task SaveFcmTokenAsync(string token, CancellationToken ct = default)
        {
            var uid = _auth.CurrentUserId;
            if (string.IsNullOrEmpty(uid)) return;
            if (string.IsNullOrEmpty(token)) return;

            var patch = new
            {
                fields = new
                {
                    fcmToken = new { stringValue = token }
                }
            };

            await PatchDocumentAsync($"users/{uid}", patch, ct, updateMask: "fcmToken");
        }

        // PROFILE (users/{uid})

        public async Task<ProfileInfo?> GetMyProfileAsync(CancellationToken ct = default)
        {
            var uid = _auth.CurrentUserId;
            if (string.IsNullOrEmpty(uid)) return null;

            var doc = await GetDocumentAsync($"users/{uid}", ct);
            if (doc is null || !doc.Value.TryGetProperty("fields", out var fields)) return null;

            return new ProfileInfo(GetString(fields, "name"), GetString(fields, "phoneNumber"));
        }

        public async Task UpdateMyProfileAsync(string name, string phone, CancellationToken ct = default)
        {
            var uid = _auth.CurrentUserId;
            if (string.IsNullOrEmpty(uid)) throw new InvalidOperationException("Must be signed in.");

            var patch = new
            {
                fields = new
                {
                    name = new { stringValue = name },
                    phoneNumber = new { stringValue = phone }
                }
            };

            await PatchDocumentAsync($"users/{uid}", patch, ct, updateMask: "name,phoneNumber");
        }


        // REVIEWS / GALLERY / CATEGORIES


        public async Task<List<Review>> GetReviewsForServiceAsync(string serviceId, CancellationToken ct = default)
        {
            var docs = await QueryCollectionAsync("reviews", ct);
            return docs.Select(MapReview)
                .Where(r => r.ServiceId == serviceId)
                .ToList();
        }

        public async Task<List<GalleryCategory>> GetGalleryCategoriesAsync(CancellationToken ct = default)
        {
            var docs = await QueryCollectionAsync("galleryCategories", ct);
            return docs.Select(MapGalleryCategory)
                .Where(c => c.IsActive)
                .ToList();
        }

        public async Task<List<GalleryImage>> GetGalleryImagesAsync(string? categoryId = null, CancellationToken ct = default)
        {
            var docs = await QueryCollectionAsync("galleryImages", ct);
            var images = docs.Select(MapGalleryImage);

            if (categoryId is not null)
                images = images.Where(i => i.CategoryId == categoryId);

            return images.ToList();
        }

        public async Task<List<ServiceCategory>> GetServiceCategoriesAsync(CancellationToken ct = default)
        {
            var docs = await QueryCollectionAsync("serviceCategories", ct);
            return docs.Select(MapServiceCategory)
                .Where(c => c.IsActive)
                .ToList();
        }


        // REST HELPERS


        private async Task<JsonElement?> GetDocumentAsync(string path, CancellationToken ct)
        {
            var request = await BuildRequestAsync(HttpMethod.Get, $"{BaseUrl}/{DocumentsPath}/{path}", null, ct);
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            return JsonDocument.Parse(json).RootElement;
        }

        private async Task<List<JsonElement>> QueryCollectionAsync(string collection, CancellationToken ct)
        {
            var request = await BuildRequestAsync(HttpMethod.Get, $"{BaseUrl}/{DocumentsPath}/{collection}", null, ct);
            var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return new List<JsonElement>();

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("documents", out var documents))
                return new List<JsonElement>();

            var list = new List<JsonElement>();
            foreach (var d in documents.EnumerateArray())
                list.Add(d.Clone());
            return list;
        }

        private async Task PostDocumentAsync(string collection, object doc, CancellationToken ct)
        {
            var request = await BuildRequestAsync(HttpMethod.Post, $"{BaseUrl}/{DocumentsPath}/{collection}", doc, ct);
            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }

        private async Task PatchDocumentAsync(string path, object doc, CancellationToken ct, string? updateMask = null)
        {
            var mask = updateMask is null
                ? ""
                : "?" + string.Join("&", updateMask.Split(',').Select(m => $"updateMask.fieldPaths={m}"));
            var request = await BuildRequestAsync(HttpMethod.Patch, $"{BaseUrl}/{DocumentsPath}/{path}{mask}", doc, ct);
            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }

        private async Task DeleteDocumentAsync(string path, CancellationToken ct)
        {
            var request = await BuildRequestAsync(HttpMethod.Delete, $"{BaseUrl}/{DocumentsPath}/{path}", null, ct);
            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
        }

        private async Task<HttpRequestMessage> BuildRequestAsync(HttpMethod method, string url, object? body, CancellationToken ct)
        {
            var request = new HttpRequestMessage(method, url);
            if (body is not null)
                request.Content = JsonContent.Create(body);

            var token = await _auth.GetIdTokenAsync();
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            return request;
        }


        // MAPPERS — Firestore REST JSON → Models


        private static string GetString(JsonElement fields, string key)
        {
            if (fields.TryGetProperty(key, out var f) && f.TryGetProperty("stringValue", out var v))
                return v.GetString() ?? "";
            return "";
        }

        private static int GetInt(JsonElement fields, string key)
        {
            if (fields.TryGetProperty(key, out var f) && f.TryGetProperty("integerValue", out var v))
                return int.TryParse(v.GetString(), out var i) ? i : 0;
            return 0;
        }

        private static decimal GetDecimal(JsonElement fields, string key)
        {
            if (fields.TryGetProperty(key, out var f))
            {
                if (f.TryGetProperty("doubleValue", out var d)) return (decimal)d.GetDouble();
                if (f.TryGetProperty("integerValue", out var i)) return decimal.Parse(i.GetString() ?? "0");
            }
            return 0;
        }

        private static bool GetBool(JsonElement fields, string key)
        {
            if (fields.TryGetProperty(key, out var f) && f.TryGetProperty("booleanValue", out var v))
                return v.GetBoolean();
            return false;
        }

        private static LocalDate GetLocalDate(JsonElement fields, string key)
        {
            var s = GetString(fields, key);
            return string.IsNullOrEmpty(s) ? default : LocalDate.FromDateOnly(DateOnly.Parse(s));
        }

        private static LocalTime GetLocalTime(JsonElement fields, string key)
        {
            var s = GetString(fields, key);
            return string.IsNullOrEmpty(s) ? default : LocalTime.FromTimeOnly(TimeOnly.Parse(s));
        }

        private static string GetDocId(JsonElement doc)
        {
            if (doc.TryGetProperty("name", out var name))
            {
                var full = name.GetString() ?? "";
                return full.Substring(full.LastIndexOf('/') + 1);
            }
            return "";
        }

        private static Service MapService(JsonElement doc)
        {
            var fields = doc.GetProperty("fields");
            return new Service
            {
                Id = GetDocId(doc),
                Name = GetString(fields, "name"),
                Description = GetString(fields, "description"),
                CategoryId = GetString(fields, "categoryId"),
                Price = GetDecimal(fields, "price"),
                DepositAmount = GetDecimal(fields, "depositAmount"),
                DurationMinutes = GetInt(fields, "durationMinutes"),
                BufferMinutes = GetInt(fields, "bufferMinutes"),
                ImageUrl = GetString(fields, "imageUrl"),
                IsActive = GetBool(fields, "isActive")
            };
        }

        private static Product MapProduct(JsonElement doc)
        {
            var fields = doc.GetProperty("fields");
            return new Product
            {
                Id = GetDocId(doc),
                Name = GetString(fields, "name"),
                Description = GetString(fields, "description"),
                CategoryId = GetString(fields, "categoryId"),
                Price = GetDecimal(fields, "price"),
                StockQuantity = GetInt(fields, "stockQuantity"),
                ImageUrl = GetString(fields, "imageUrl"),
                IsActive = GetBool(fields, "isActive")
            };
        }

        private static CartItem MapCartItem(JsonElement doc)
        {
            var fields = doc.GetProperty("fields");
            return new CartItem
            {
                Id = GetDocId(doc),
                ClientId = GetString(fields, "clientId"),
                ProductId = GetString(fields, "productId"),
                Quantity = GetInt(fields, "quantity")
            };
        }

        private static Booking MapBooking(JsonElement doc)
        {
            var fields = doc.GetProperty("fields");
            return new Booking
            {
                Id = GetDocId(doc),
                ClientId = GetString(fields, "clientId"),
                ServiceId = GetString(fields, "serviceId"),
                Date = GetLocalDate(fields, "date"),
                StartTime = GetLocalTime(fields, "startTime"),
                EndTime = GetLocalTime(fields, "endTime"),
                DepositAmount = GetDecimal(fields, "depositAmount"),
                TotalAmount = GetDecimal(fields, "totalAmount"),
                Notes = GetString(fields, "notes")
            };
        }

        private static Order MapOrder(JsonElement doc)
        {
            var fields = doc.GetProperty("fields");
            return new Order
            {
                Id = GetDocId(doc),
                ClientId = GetString(fields, "clientId"),
                Total = GetDecimal(fields, "totalAmount"),
                DeliveryAddress = GetString(fields, "deliveryAddress"),
                Status = OrderStatus.Pending
            };
        }

        private static Notification MapNotification(JsonElement doc)
        {
            var fields = doc.GetProperty("fields");
            return new Notification
            {
                Id = GetDocId(doc),
                ClientId = GetString(fields, "clientId"),
                Message = GetString(fields, "message"),
                IsRead = GetBool(fields, "isRead")
            };
        }

        private static Review MapReview(JsonElement doc)
        {
            var fields = doc.GetProperty("fields");
            return new Review
            {
                Id = GetDocId(doc),
                ClientId = GetString(fields, "clientId"),
                BookingId = GetString(fields, "bookingId"),
                ServiceId = GetString(fields, "serviceId"),
                Rating = GetInt(fields, "rating"),
                Comment = GetString(fields, "comment")
            };
        }

        private static GalleryCategory MapGalleryCategory(JsonElement doc)
        {
            var fields = doc.GetProperty("fields");
            return new GalleryCategory
            {
                Id = GetDocId(doc),
                Name = GetString(fields, "name"),
                IsActive = GetBool(fields, "isActive")
            };
        }

        private static GalleryImage MapGalleryImage(JsonElement doc)
        {
            var fields = doc.GetProperty("fields");
            return new GalleryImage
            {
                Id = GetDocId(doc),
                CategoryId = GetString(fields, "categoryId"),
                ImageUrl = GetString(fields, "imageUrl"),
                Title = GetString(fields, "title"),
                Description = GetString(fields, "description")
            };
        }

        private static ServiceCategory MapServiceCategory(JsonElement doc)
        {
            var fields = doc.GetProperty("fields");
            return new ServiceCategory
            {
                Id = GetDocId(doc),
                Name = GetString(fields, "name"),
                Description = GetString(fields, "description"),
                IsActive = GetBool(fields, "isActive")
            };
        }
    }
}