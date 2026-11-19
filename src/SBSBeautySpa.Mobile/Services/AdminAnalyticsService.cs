namespace SBSBeautySpa.Mobile.Services
{
    public class AnalyticsTotals
    {
        public double Total { get; set; }
        public double Spa { get; set; }
        public double Shop { get; set; }
        public int Payments { get; set; }
        public double AveragePayment { get; set; }
        public double PerDay { get; set; }
        public int BookingsInRange { get; set; }
    }

    public class RevenuePoint
    {
        public string Label { get; set; } = "";
        public double Bookings { get; set; }
        public double Shop { get; set; }
    }

    public class CountSlice
    {
        public string Label { get; set; } = "";
        public double Value { get; set; }
    }

    public class TopItem
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
        public double Revenue { get; set; }
    }

    public class AnalyticsResult
    {
        public string Range { get; set; } = "";
        public string Bucket { get; set; } = "day";
        public AnalyticsTotals Totals { get; set; } = new();
        public List<RevenuePoint> Series { get; set; } = new();
        public List<CountSlice> Split { get; set; } = new();
        public List<CountSlice> PaymentKinds { get; set; } = new();
        public List<CountSlice> BookingStatus { get; set; } = new();
        public List<TopItem> TopServices { get; set; } = new();
        public List<TopItem> TopProducts { get; set; } = new();
    }

    public class ReportFile
    {
        public string FileName { get; set; } = "";
        public string Base64 { get; set; } = "";
    }

    // Revenue numbers and the Excel report come from our own server (admin only).
    public class AdminAnalyticsService
    {
        private readonly CallableFunctionClient _client;

        public AdminAnalyticsService(CallableFunctionClient client) => _client = client;

        public Task<AnalyticsResult> GetAsync(string range, string? from, string? to, CancellationToken ct = default) =>
            _client.CallAsync<AnalyticsResult>("getAnalytics", new { range, from, to }, ct);

        // Downloads the Excel report, saves it, and returns the saved path.
        public async Task<string> ExportAsync(string range, string? from, string? to, CancellationToken ct = default)
        {
            var file = await _client.CallAsync<ReportFile>("exportRevenueReport", new { range, from, to }, ct);
            var bytes = Convert.FromBase64String(file.Base64);

            var isWindows = DeviceInfo.Platform == DevicePlatform.WinUI;
            var folder = isWindows
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SBS Reports")
                : FileSystem.CacheDirectory;
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, file.FileName);
            await File.WriteAllBytesAsync(path, bytes, ct);

            if (isWindows)
                await Launcher.Default.OpenAsync(new OpenFileRequest("Revenue report", new ReadOnlyFile(path)));
            else
                await Share.Default.RequestAsync(new ShareFileRequest("Revenue report", new ShareFile(path)));
            return path;
        }
    }
}
