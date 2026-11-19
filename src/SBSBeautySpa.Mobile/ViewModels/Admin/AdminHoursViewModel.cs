using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NodaTime;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    // One row on the page: a weekday with an open/closed switch and opening times.
    public partial class DayRow : ObservableObject
    {
        public DayRow(int weekday, string name)
        {
            Weekday = weekday;
            Name = name;
        }

        public int Weekday { get; }   // 0 = Sunday ... 6 = Saturday (same as the server)
        public string Name { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsClosed))]
        private bool isOpen;

        [ObservableProperty]
        private TimeSpan open = new(9, 0, 0);

        [ObservableProperty]
        private TimeSpan close = new(17, 0, 0);

        public bool IsClosed => !IsOpen;
    }

    // One blocked period (holiday, break...) shown in the closures list.
    public class ClosureRow
    {
        public ClosureRow(string id, string title, string reason)
        {
            Id = id;
            Title = title;
            Reason = reason;
        }

        public string Id { get; }
        public string Title { get; }
        public string Reason { get; }
    }

    public partial class AdminHoursViewModel : BaseViewModel
    {
        // The salon's time zone, used to turn closure dates into exact moments.
        private static readonly DateTimeZone Zone = DateTimeZoneProviders.Tzdb["Africa/Johannesburg"];

        private readonly IAdminAvailabilityService _availability;

        public AdminHoursViewModel(IAdminAvailabilityService availability)
        {
            _availability = availability;
            Title = "Business Hours";

            // Monday first, Sunday last.
            Days.Add(new DayRow(1, "Monday"));
            Days.Add(new DayRow(2, "Tuesday"));
            Days.Add(new DayRow(3, "Wednesday"));
            Days.Add(new DayRow(4, "Thursday"));
            Days.Add(new DayRow(5, "Friday"));
            Days.Add(new DayRow(6, "Saturday"));
            Days.Add(new DayRow(0, "Sunday"));
        }

        public ObservableCollection<DayRow> Days { get; } = new();
        public ObservableCollection<ClosureRow> Closures { get; } = new();

        [ObservableProperty]
        private string? infoMessage;

        [ObservableProperty]
        private bool isClosureFormOpen;

        [ObservableProperty]
        private DateTime closureStart = DateTime.Today;

        [ObservableProperty]
        private DateTime closureEnd = DateTime.Today;

        [ObservableProperty]
        private string closureReason = string.Empty;

        [ObservableProperty]
        private bool hasNoClosures = true;

        // ---------- Load ----------

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var hours = await _availability.GetBusinessHoursAsync();

                foreach (var row in Days)
                {
                    var saved = hours.FirstOrDefault(h => h.Weekday == row.Weekday);
                    if (saved is null) continue;

                    row.IsOpen = !saved.IsClosed;
                    if (saved.OpenTime.HasValue)
                        row.Open = TimeSpan.FromTicks(saved.OpenTime.Value.TickOfDay);
                    if (saved.CloseTime.HasValue)
                        row.Close = TimeSpan.FromTicks(saved.CloseTime.Value.TickOfDay);
                }

                await FetchClosuresAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load business hours: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ---------- Save the weekly hours ----------

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (IsBusy) return;

            ErrorMessage = null;
            InfoMessage = null;

            foreach (var row in Days)
            {
                if (row.IsOpen && row.Close <= row.Open)
                {
                    ErrorMessage = $"{row.Name}: closing time must be after opening time.";
                    return;
                }
            }

            IsBusy = true;
            try
            {
                var payload = Days.Select(d => d.IsOpen
                    ? new BusinessHoursDay(
                        d.Weekday,
                        false,
                        LocalTime.FromTicksSinceMidnight(d.Open.Ticks),
                        LocalTime.FromTicksSinceMidnight(d.Close.Ticks))
                    : new BusinessHoursDay(d.Weekday, true, null, null))
                    .ToList();

                await _availability.SetBusinessHoursAsync(payload);
                InfoMessage = "Business hours saved.";
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't save business hours: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ---------- Closure dates (holidays, breaks) ----------

        [RelayCommand]
        private void ToggleClosureForm()
        {
            if (!IsClosureFormOpen)
            {
                ClosureStart = DateTime.Today;
                ClosureEnd = DateTime.Today;
                ClosureReason = string.Empty;
                ErrorMessage = null;
                InfoMessage = null;
            }

            IsClosureFormOpen = !IsClosureFormOpen;
        }

        [RelayCommand]
        private async Task AddClosureAsync()
        {
            if (IsBusy) return;

            ErrorMessage = null;
            InfoMessage = null;

            var startDate = LocalDate.FromDateTime(ClosureStart);
            var endDate = LocalDate.FromDateTime(ClosureEnd);

            if (endDate < startDate)
            {
                ErrorMessage = "The last closed day can't be before the first.";
                return;
            }

            var reason = string.IsNullOrWhiteSpace(ClosureReason) ? "Closed" : ClosureReason.Trim();

            // From midnight of the first day up to midnight after the last day.
            var start = Zone.AtStartOfDay(startDate).ToInstant();
            var end = Zone.AtStartOfDay(endDate.PlusDays(1)).ToInstant();

            IsBusy = true;
            bool added = false;
            try
            {
                await _availability.AddBlockedTimeAsync(start, end, reason);
                IsClosureFormOpen = false;
                added = true;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't add the closure: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }

            if (added)
            {
                try
                {
                    await FetchClosuresAsync();
                    InfoMessage = "Closure added.";
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Added, but couldn't refresh the list: " + ex.Message;
                }
            }
        }

        [RelayCommand]
        private async Task DeleteClosureAsync(ClosureRow row)
        {
            if (IsBusy || row is null) return;

            IsBusy = true;
            ErrorMessage = null;
            InfoMessage = null;

            try
            {
                await _availability.DeleteBlockedTimeAsync(row.Id);
                Closures.Remove(row);
                HasNoClosures = Closures.Count == 0;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't remove the closure: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Loads upcoming closures (next 12 months). No busy check, so callers can use it freely.
        private async Task FetchClosuresAsync()
        {
            var today = LocalDate.FromDateTime(DateTime.Today);
            var blocks = await _availability.ListBlockedTimeAsync(today, today.PlusYears(1));

            Closures.Clear();
            foreach (var b in blocks.OrderBy(b => b.Start))
            {
                var first = b.Start.InZone(Zone).Date;
                var last = b.End.InZone(Zone).Date.PlusDays(-1);   // end is midnight AFTER the last day

                string title = first == last
                    ? first.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)
                    : $"{first.ToString("dd MMM", CultureInfo.InvariantCulture)} - {last.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}";

                Closures.Add(new ClosureRow(b.Id, title, b.Reason));
            }

            HasNoClosures = Closures.Count == 0;
        }
    }
}
