using System.Globalization;
using NodaTime;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.Views;

public partial class AddOnsPage : ContentPage, IQueryAttributable
{
    private readonly IFirestoreService _firestore;

    // Handed over from the Select Date & Time page.
    private string _serviceId = string.Empty;
    private object? _selectedDate;
    private object? _selectedStartTime;

    // Looked up from the service itself.
    private string _serviceName = "Your service";
    private int _durationMinutes;
    private decimal _price;
    private decimal _deposit;
    private bool _serviceLoaded;

    // ---- Add-on price list (from the spa's menu) ----
    // MinPrice != MaxPrice means a price range (e.g. nail art R5 - R50).
    private sealed record AddOn(string Name, decimal MinPrice, decimal MaxPrice, int Minutes = 0);

    private static readonly AddOn[] NailAddOns =
    {
        new("Gel colour / Nail art", 5, 50),
        new("Soak off", 100, 100, 30),
        new("Cuticle oil", 30, 30),
    };

    private static readonly AddOn[] PedicureAddOns =
    {
        new("Acrylic overlay", 220, 220),
        new("Gel overlay", 200, 200),
    };

    private static readonly AddOn[] LashAddOns =
    {
        new("Lash removal", 100, 100, 30),
        new("Bottom lashes", 100, 100),
    };

    private readonly List<(AddOn Item, CheckBox Box)> _rows = new();

    public AddOnsPage(IFirestoreService firestore)
    {
        InitializeComponent();
        _firestore = firestore;
    }

    // Receives ServiceId / SelectedDate / SelectedStartTime from the previous page.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("ServiceId", out var id))
            _serviceId = id?.ToString() ?? string.Empty;
        if (query.TryGetValue("SelectedDate", out var date))
            _selectedDate = date;
        if (query.TryGetValue("SelectedStartTime", out var time))
            _selectedStartTime = time;

        RefreshSummary();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_rows.Count == 0) BuildAddOns();

        if (_serviceLoaded || string.IsNullOrEmpty(_serviceId)) return;

        try
        {
            var services = await _firestore.GetServicesAsync();
            var service = services.FirstOrDefault(s => s.Id == _serviceId);
            if (service is not null)
            {
                _serviceName = service.Name;
                _durationMinutes = service.DurationMinutes;
                _price = service.Price;
                _deposit = service.DepositAmount;
                _serviceLoaded = true;
                BuildAddOns();
                RefreshSummary();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    // Fills the card at the top ("Gel Extensions • Oct 15, 2026 • 10:00 AM (1h 30m)").
    private void RefreshSummary()
    {
        ServiceNameLabel.Text = _serviceName;
        WhenLabel.Text = BuildWhenText();
    }

    private string BuildWhenText()
    {
        var text = $"{BookingText.Date(_selectedDate)}  •  {BookingText.Time(_selectedStartTime)}";
        var total = _durationMinutes + SelectedAddOns().Sum(a => a.Minutes);
        if (total > 0)
            text += $" ({BookingText.Duration(total)})";
        return text;
    }

    private IEnumerable<AddOn> SelectedAddOns() => _rows.Where(r => r.Box.IsChecked).Select(r => r.Item);

    // Picks the right add-on list from the service name.
    private AddOn[] AddOnsForService()
    {
        var n = _serviceName.ToLowerInvariant();
        if (n.Contains("lash")) return LashAddOns;
        if (n.Contains("pedi") || n.Contains("toe")) return PedicureAddOns;
        return NailAddOns;
    }

    private static string Rand(decimal v) => "R" + v.ToString("0.##", CultureInfo.InvariantCulture);

    private static string PriceText(AddOn a) =>
        a.MinPrice == a.MaxPrice ? Rand(a.MinPrice) : $"{Rand(a.MinPrice)} - {Rand(a.MaxPrice)}";

    private void BuildAddOns()
    {
        AddOnsList.Children.Clear();
        _rows.Clear();

        var rowTitle = (Style)Application.Current!.Resources["RowTitle"];
        var rowSub = (Style)Application.Current!.Resources["RowSubtitle"];
        var divider = (Style)Application.Current!.Resources["Divider"];

        var items = AddOnsForService();
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var box = new CheckBox { Color = Colors.Black };
            box.CheckedChanged += (_, _) => { UpdateTotals(); RefreshSummary(); };

            var name = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 0 };
            name.Add(new Label { Text = item.Name, Style = rowTitle });
            if (item.Minutes > 0)
                name.Add(new Label { Text = $"+{item.Minutes} min", Style = rowSub });

            var price = new Label
            {
                Text = PriceText(item),
                VerticalOptions = LayoutOptions.Center,
                FontFamily = "OpenSansSemibold",
            };

            var row = new Grid
            {
                Padding = new Thickness(14, 10),
                ColumnDefinitions = new ColumnDefinitionCollection(
                new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto))
            };
            row.Add(box, 0, 0);
            row.Add(name, 1, 0);
            row.Add(price, 2, 0);
            row.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => box.IsChecked = !box.IsChecked) });

            AddOnsList.Children.Add(row);
            if (i < items.Length - 1) AddOnsList.Children.Add(new BoxView { Style = divider });
            _rows.Add((item, box));
        }
        UpdateTotals();
    }

    // Estimated total of the ticked add-ons (ranges count from their lowest price).
    private void UpdateTotals()
    {
        var chosen = SelectedAddOns().ToList();
        var total = chosen.Sum(a => a.MinPrice);
        var ranged = chosen.Any(a => a.MinPrice != a.MaxPrice);
        TotalLabel.Text = (ranged ? "from +" : "+") + Rand(total);
    }

    // Back arrow: goes back to the previous page.
    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    // Continue -> Booking Review page, carrying everything chosen so far.
    private async void OnContinueClicked(object sender, EventArgs e)
    {
        var chosen = SelectedAddOns().ToList();
        var extraMinutes = chosen.Sum(a => a.Minutes);
        var totalMinutes = _durationMinutes + extraMinutes;

        await Shell.Current.GoToAsync("BookingReviewPage", new ShellNavigationQueryParameters
        {
            ["ServiceId"] = _serviceId,
            ["ServiceName"] = _serviceName,
            ["DateText"] = BookingText.Date(_selectedDate),
            ["TimeText"] = BookingText.Time(_selectedStartTime),
            ["DurationText"] = totalMinutes > 0 ? BookingText.Duration(totalMinutes) : string.Empty,
            ["Price"] = _price,
            ["Deposit"] = _deposit,
            ["AddOns"] = string.Join(", ", chosen.Select(a => $"{a.Name} ({PriceText(a)})")),
            ["AddOnsTotal"] = chosen.Sum(a => a.MinPrice),
            ["AddOnsRanged"] = chosen.Any(a => a.MinPrice != a.MaxPrice),
            // Raw values, for creating the booking later.
            ["SelectedDate"] = _selectedDate ?? string.Empty,
            ["SelectedStartTime"] = _selectedStartTime ?? string.Empty,
        });
    }
}

// Small helpers to turn the chosen date / time into readable text.
internal static class BookingText
{
    public static string Date(object? value) => value switch
    {
        null => string.Empty,
        LocalDate d => d.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
        DateTime d => d.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    public static string Time(object? value) => value switch
    {
        null => string.Empty,
        LocalTime t => t.ToString("h:mm tt", CultureInfo.InvariantCulture),
        DateTime t => t.ToString("h:mm tt", CultureInfo.InvariantCulture),
        TimeSpan t => DateTime.Today.Add(t).ToString("h:mm tt", CultureInfo.InvariantCulture),
        TimeOnly t => t.ToString("h:mm tt", CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    public static string Duration(int minutes)
    {
        var h = minutes / 60;
        var m = minutes % 60;
        if (h > 0 && m > 0) return $"{h}h {m}m";
        if (h > 0) return $"{h}h";
        return $"{m}m";
    }
}