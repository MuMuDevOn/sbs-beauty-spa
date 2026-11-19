using SBSBeautySpa.Mobile.Services;
using SBSBeautySpa.Mobile.ViewModels.Admin;

namespace SBSBeautySpa.Mobile.Views.Admin;

public partial class AdminAnalyticsPage : ContentPage
{
    private readonly AdminAnalyticsViewModel _viewModel;
    private readonly RevenueColumnsDrawable _columns = new();
    private readonly DonutDrawable _split = new();
    private readonly DonutDrawable _status = new();

    public AdminAnalyticsPage(AdminAnalyticsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        ColumnsView.Drawable = _columns;
        SplitView.Drawable = _split;
        StatusView.Drawable = _status;

        _viewModel.DataChanged += OnDataChanged;
        _viewModel.ChipsChanged += UpdateChips;
        UpdateChips();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnDataChanged(AnalyticsResult d)
    {
        _columns.Points = d.Series.Select(s => (s.Label, s.Bookings, s.Shop)).ToList();
        ColumnsView.Invalidate();

        _split.Slices = d.Split.Select((s, i) => (s.Value, i == 0 ? AdminAnalyticsViewModel.SpaColor : AdminAnalyticsViewModel.ShopColor)).ToList();
        _split.CenterTop = "R" + d.Totals.Total.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        _split.CenterBottom = "total";
        SplitView.Invalidate();

        _status.Slices = d.BookingStatus.Select((s, i) => (s.Value, AdminAnalyticsViewModel.PaletteColor(i))).ToList();
        _status.CenterTop = ((int)d.BookingStatus.Sum(s => s.Value)).ToString();
        _status.CenterBottom = "bookings";
        StatusView.Invalidate();
    }

    private void UpdateChips()
    {
        void Style(Border chip, Label label, bool on)
        {
            chip.BackgroundColor = on ? Colors.White : Color.FromArgb("#33FFFFFF");
            label.TextColor = on ? Color.FromArgb("#4A35B0") : Colors.White;
        }
        var r = _viewModel.SelectedRange;
        Style(Chip7, Label7, r == "7d");
        Style(Chip30, Label30, r == "30d");
        Style(Chip12, Label12, r == "12m");
        Style(ChipCustom, LabelCustom, r == "custom");
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
