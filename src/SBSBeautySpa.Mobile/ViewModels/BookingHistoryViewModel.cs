using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class BookingHistoryViewModel : BaseViewModel
    {
        public BookingHistoryViewModel()
        {
            Title = "Booking History";

            Tabs = new ObservableCollection<BookingHistoryTab>
            {
                new("All", isSelected: true),
                new("Confirmed"),
                new("Completed"),
                new("Cancelled"),
            };
        }

        public ObservableCollection<BookingHistoryTab> Tabs { get; }
        public ObservableCollection<BookingHistoryItem> Bookings { get; } = new();

        [RelayCommand]
        private void SelectTab(BookingHistoryTab tab)
        {
            foreach (var t in Tabs) t.IsSelected = false;
            tab.IsSelected = true;
            // TODO: filter Bookings by tab.Name
        }

        [RelayCommand]
        private Task RescheduleAsync(BookingHistoryItem item)
        {
            // TODO: navigate to reschedule flow
            return Task.CompletedTask;
        }

        [RelayCommand]
        private Task ViewAddressAsync(BookingHistoryItem item)
        {
            // TODO: show address
            return Task.CompletedTask;
        }
    }

    public partial class BookingHistoryTab : ObservableObject
    {
        public string Name { get; }

        [ObservableProperty]
        private bool isSelected;

        public BookingHistoryTab(string name, bool isSelected = false)
        {
            Name = name;
            IsSelected = isSelected;
        }

        public Color ChipBackground => IsSelected ? Color.FromArgb("#1B2A4A") : Colors.White;
        public Color ChipTextColor => IsSelected ? Colors.White : Color.FromArgb("#1B2A4A");
    }

    public class BookingHistoryItem
    {
        public string ServiceName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string DateText { get; set; } = string.Empty;
        public string PriceText { get; set; } = string.Empty;
        public bool IsConfirmed { get; set; }
    }
}