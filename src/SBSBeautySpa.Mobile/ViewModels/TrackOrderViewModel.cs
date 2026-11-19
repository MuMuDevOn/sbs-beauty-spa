using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    // One row of the delivery timeline.
    public class TrackingStepItem
    {
        public string Label { get; init; } = "";
        public Color DotColor { get; init; } = Colors.LightGray;
        public Color LineColor { get; init; } = Colors.LightGray;
        public Color TextColor { get; init; } = Colors.Gray;
        public string LabelFont { get; init; } = "OpenSansRegular";
        public bool ShowLine { get; init; }
    }

    // One courier update ("Out for delivery", 3 Nov 14:20 ...).
    public class TrackingEventItem
    {
        public string Title { get; init; } = "";
        public string Location { get; init; } = "";
        public string DateText { get; init; } = "";
        public bool HasLocation => !string.IsNullOrWhiteSpace(Location);
    }

    [QueryProperty(nameof(OrderId), "orderId")]
    public partial class TrackOrderViewModel : BaseViewModel
    {
        private static readonly Color Blue = Color.FromArgb("#2F63E0");
        private static readonly Color Line = Color.FromArgb("#E3E9F5");
        private static readonly Color Ink = Color.FromArgb("#111827");
        private static readonly Color Muted = Color.FromArgb("#8A93A6");
        private static readonly Color Red = Color.FromArgb("#B3261E");

        private readonly TrackingService _tracking;

        public TrackOrderViewModel(TrackingService tracking)
        {
            _tracking = tracking;
            Title = "Track order";
        }

        public ObservableCollection<TrackingStepItem> Steps { get; } = new();
        public ObservableCollection<TrackingEventItem> Events { get; } = new();

        [ObservableProperty] private string orderId = string.Empty;
        [ObservableProperty] private string statusText = string.Empty;
        [ObservableProperty] private string? message;
        [ObservableProperty] private string? referenceText;
        [ObservableProperty] private bool isDemo;
        [ObservableProperty] private bool hasEvents;
        [ObservableProperty] private Color statusColor = Ink;

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy || string.IsNullOrWhiteSpace(OrderId))
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var t = await _tracking.GetAsync(OrderId);

                StatusText = t.StatusText;
                Message = string.IsNullOrWhiteSpace(t.Message) ? null : t.Message;
                ReferenceText = string.IsNullOrWhiteSpace(t.TrackingReference) ? null : $"{t.Carrier} · {t.TrackingReference}";
                IsDemo = t.IsMock;
                StatusColor = t.Exception ? Red : Ink;

                Steps.Clear();
                for (var i = 0; i < t.Steps.Count; i++)
                {
                    var s = t.Steps[i];
                    var isLast = i == t.Steps.Count - 1;
                    Steps.Add(new TrackingStepItem
                    {
                        Label = s.Label,
                        DotColor = s.Done ? (t.Exception && s.Current ? Red : Blue) : Line,
                        // the line below a step is blue only when the NEXT step is reached too
                        LineColor = !isLast && t.Steps[i + 1].Done ? Blue : Line,
                        TextColor = s.Done ? Ink : Muted,
                        LabelFont = s.Current ? "OpenSansSemibold" : "OpenSansRegular",
                        ShowLine = !isLast,
                    });
                }

                Events.Clear();
                foreach (var e in t.Events)
                {
                    Events.Add(new TrackingEventItem
                    {
                        Title = e.Title,
                        Location = e.Location,
                        DateText = e.Date?.ToLocalTime().ToString("d MMM, HH:mm") ?? "",
                    });
                }
                HasEvents = Events.Count > 0;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load tracking. Check your connection and try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
