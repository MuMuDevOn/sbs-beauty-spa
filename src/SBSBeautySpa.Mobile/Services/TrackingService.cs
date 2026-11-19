namespace SBSBeautySpa.Mobile.Services
{
    public class TrackingStep
    {
        public string Label { get; set; } = "";
        public bool Done { get; set; }
        public bool Current { get; set; }
    }

    public class TrackingEvent
    {
        public DateTimeOffset? Date { get; set; }
        public string Title { get; set; } = "";
        public string Location { get; set; } = "";
    }

    // What the server says about one order's delivery.
    public class OrderTracking
    {
        public string OrderId { get; set; } = "";
        public string? TrackingReference { get; set; }
        public string Carrier { get; set; } = "The Courier Guy";
        public bool IsMock { get; set; }
        public int Stage { get; set; }
        public string StatusText { get; set; } = "";
        public string Message { get; set; } = "";
        public bool Exception { get; set; }
        public List<TrackingStep> Steps { get; set; } = new();
        public List<TrackingEvent> Events { get; set; } = new();
    }

    // Delivery tracking through our own server (the Courier Guy key stays secret there).
    public class TrackingService
    {
        private readonly CallableFunctionClient _client;

        public TrackingService(CallableFunctionClient client) => _client = client;

        public Task<OrderTracking> GetAsync(string orderId, CancellationToken ct = default) =>
            _client.CallAsync<OrderTracking>("trackOrder", new { orderId }, ct);
    }
}
