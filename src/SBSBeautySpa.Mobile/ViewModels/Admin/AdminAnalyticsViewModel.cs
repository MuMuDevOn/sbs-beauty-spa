using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    public class LegendItem
    {
        public string Label { get; init; } = "";
        public string ValueText { get; init; } = "";
        public Color Color { get; init; } = Colors.Gray;
    }

    public class TopRow
    {
        public string Name { get; init; } = "";
        public string Detail { get; init; } = "";
        public string RevenueText { get; init; } = "";
    }

    public partial class AdminAnalyticsViewModel : BaseViewModel
    {
        public static readonly Color SpaColor = Color.FromArgb("#6D3FE0");
        public static readonly Color ShopColor = Color.FromArgb("#2F9BE0");
        private static readonly Color[] Palette =
        {
            Color.FromArgb("#6D3FE0"), Color.FromArgb("#2F9BE0"), Color.FromArgb("#F2A541"),
            Color.FromArgb("#E0527A"), Color.FromArgb("#3DB38A"), Color.FromArgb("#8A93A6"),
        };

        private readonly AdminAnalyticsService _service;
        private AnalyticsResult? _data;

        [ObservableProperty] private string _selectedRange = "30d";
        [ObservableProperty] private bool _isCustom;
        [ObservableProperty] private DateTime _fromDate = DateTime.Today.AddDays(-29);
        [ObservableProperty] private DateTime _toDate = DateTime.Today;
        [ObservableProperty] private string _totalText = "R0.00";
        [ObservableProperty] private string _spaText = "R0.00";
        [ObservableProperty] private string _shopText = "R0.00";
        [ObservableProperty] private string _paymentsText = "0";
        [ObservableProperty] private string _averageText = "R0.00";
        [ObservableProperty] private string _perDayText = "R0.00";
        [ObservableProperty] private bool _hasData;
        [ObservableProperty] private string? _infoMessage;

        public ObservableCollection<LegendItem> SplitLegend { get; } = new();
        public ObservableCollection<LegendItem> StatusLegend { get; } = new();
        public ObservableCollection<TopRow> TopServices { get; } = new();
        public ObservableCollection<TopRow> TopProducts { get; } = new();

        // The page redraws its charts when this fires.
        public event Action<AnalyticsResult>? DataChanged;

        public AdminAnalyticsViewModel(AdminAnalyticsService service)
        {
            _service = service;
            Title = "Analytics";
        }

        private static string Money(double v) => "R" + v.ToString("N2", CultureInfo.InvariantCulture);

        private (string? from, string? to) CustomDates() =>
            SelectedRange == "custom" ? (FromDate.ToString("yyyy-MM-dd"), ToDate.ToString("yyyy-MM-dd")) : (null, null);

        [RelayCommand]
        private async Task PickRangeAsync(string range)
        {
            SelectedRange = range;
            IsCustom = range == "custom";
            if (range != "custom") await LoadAsync();
            else NotifyChipsChanged();
        }

        public event Action? ChipsChanged;
        private void NotifyChipsChanged() => ChipsChanged?.Invoke();

        [RelayCommand]
        public async Task LoadAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                ErrorMessage = null;
                NotifyChipsChanged();
                var (from, to) = CustomDates();
                var d = await _service.GetAsync(SelectedRange, from, to);
                Apply(d);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load analytics: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void Apply(AnalyticsResult d)
        {
            _data = d;
            TotalText = Money(d.Totals.Total);
            SpaText = Money(d.Totals.Spa);
            ShopText = Money(d.Totals.Shop);
            PaymentsText = d.Totals.Payments.ToString(CultureInfo.InvariantCulture);
            AverageText = Money(d.Totals.AveragePayment);
            PerDayText = Money(d.Totals.PerDay);
            HasData = d.Totals.Payments > 0;
            InfoMessage = HasData ? null : "No payments in this period yet.";

            SplitLegend.Clear();
            for (int i = 0; i < d.Split.Count; i++)
                SplitLegend.Add(new LegendItem { Label = d.Split[i].Label, ValueText = Money(d.Split[i].Value), Color = i == 0 ? SpaColor : ShopColor });

            StatusLegend.Clear();
            for (int i = 0; i < d.BookingStatus.Count; i++)
                StatusLegend.Add(new LegendItem { Label = d.BookingStatus[i].Label, ValueText = ((int)d.BookingStatus[i].Value).ToString(CultureInfo.InvariantCulture), Color = Palette[i % Palette.Length] });

            TopServices.Clear();
            foreach (var t in d.TopServices)
                TopServices.Add(new TopRow { Name = t.Name, Detail = t.Count + (t.Count == 1 ? " booking" : " bookings"), RevenueText = Money(t.Revenue) });
            TopProducts.Clear();
            foreach (var t in d.TopProducts)
                TopProducts.Add(new TopRow { Name = t.Name, Detail = t.Count + (t.Count == 1 ? " unit" : " units"), RevenueText = Money(t.Revenue) });

            DataChanged?.Invoke(d);
        }

        public static Color PaletteColor(int i) => Palette[i % Palette.Length];

        [RelayCommand]
        private async Task ExportAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                ErrorMessage = null;
                var (from, to) = CustomDates();
                var path = await _service.ExportAsync(SelectedRange, from, to);
                InfoMessage = "Report saved: " + path;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't create the report: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
