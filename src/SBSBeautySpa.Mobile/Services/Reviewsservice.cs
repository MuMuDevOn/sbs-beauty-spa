namespace SBSBeautySpa.Mobile.Services
{
    // The contract for the reviews service.
    public interface IReviewsService
    {
        // Submits a review for a completed booking.
        //
        // The server rejects this if:
        //   - The booking isn't the caller's own
        //   - The booking isn't yet "completed"
        //   - The booking already has a review
        //
        // None of these are checked on the client side.
        // The server's error message is what the UI should show.
        Task<string> SubmitReviewAsync(string bookingId, int rating, string? comment, CancellationToken ct = default);
    }

    // This class talks to the server about submitting reviews.
    public class ReviewsService : IReviewsService
    {
        private readonly CallableFunctionClient _client;

        public ReviewsService(CallableFunctionClient client) => _client = client;

        // Sends a review to the server.
        // The server does all the validation (ownership, status, duplicates).
        public async Task<string> SubmitReviewAsync(string bookingId, int rating, string? comment, CancellationToken ct = default)
        {
            var result = await _client.CallAsync<ReviewResult>("submitReview", new { bookingId, rating, comment }, ct);
            return result.ReviewId;
        }

        // Small helper class to match the server's response shape.
        private class ReviewResult { public string ReviewId { get; set; } = string.Empty; }
    }
}