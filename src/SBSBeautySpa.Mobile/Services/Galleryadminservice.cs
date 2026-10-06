namespace SBSBeautySpa.Mobile.Services
{
    // The contract for the gallery admin service.
    public interface IGalleryAdminService
    {
        // Creates a new gallery category.
        Task<string> CreateGalleryCategoryAsync(string name, CancellationToken ct = default);

        // Adds a new gallery image record.
        //
        // IMPORTANT: The imageUrl must already point at a file that's
        // been uploaded to Firebase Storage. Uploading the actual file
        // is a separate step — done directly from the admin app using
        // the Storage SDK, NOT through this call.
        //
        // This method only creates the Firestore record that points at
        // the already-uploaded image.
        Task<string> AddGalleryImageAsync(string categoryId, string imageUrl, string? title, string? description, CancellationToken ct = default);

        // Deletes a gallery image record.
        //
        // NOTE: This only removes the Firestore record. The actual image
        // file in Firebase Storage is NOT deleted. That's a separate
        // cleanup task (which is flagged as a gap in the backend).
        Task DeleteGalleryImageAsync(string imageId, CancellationToken ct = default);
    }

    // This class talks to the server about gallery management (admin only).
    public class GalleryAdminService : IGalleryAdminService
    {
        private readonly CallableFunctionClient _client;

        public GalleryAdminService(CallableFunctionClient client) => _client = client;

        // Creates a new gallery category.
        public async Task<string> CreateGalleryCategoryAsync(string name, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<CategoryResult>("createGalleryCategory", new { name }, ct);
            return result.CategoryId;
        }

        // Adds a new gallery image record.
        // The actual image must already be uploaded to Firebase Storage.
        public async Task<string> AddGalleryImageAsync(string categoryId, string imageUrl, string? title, string? description, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<ImageResult>("addGalleryImage", new { categoryId, imageUrl, title, description }, ct);
            return result.ImageId;
        }

        // Deletes a gallery image record.
        public Task DeleteGalleryImageAsync(string imageId, CancellationToken ct = default) =>
            _client.CallAsync("deleteGalleryImage", new { imageId }, ct);

        // Small helper classes to match the server's response shape.
        private class CategoryResult { public string CategoryId { get; set; } = string.Empty; }
        private class ImageResult { public string ImageId { get; set; } = string.Empty; }
    }
}