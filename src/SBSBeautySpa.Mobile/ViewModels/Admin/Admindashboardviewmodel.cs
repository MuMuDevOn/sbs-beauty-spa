using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    public partial class AdminDashboardViewModel : BaseViewModel
    {
        private readonly IAdminDataService _adminDataService;

        public AdminDashboardViewModel(IAdminDataService adminDataService)
        {
            _adminDataService = adminDataService;
            Title = "Dashboard";
        }

        [ObservableProperty]
        private int upcomingBookingsCount;

        [ObservableProperty]
        private decimal revenueThisMonth;

        [ObservableProperty]
        private int pendingCount;

        public ObservableCollection<Booking> RecentBookings { get; } = new();

        [RelayCommand]
        private async Task LoadDashboardAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var summary = await _adminDataService.GetDashboardSummaryAsync();

                UpcomingBookingsCount = summary.UpcomingBookingsCount;
                RevenueThisMonth = summary.RevenueThisMonth;
                PendingCount = summary.PendingCount;

                RecentBookings.Clear();
                foreach (var booking in summary.RecentBookings)
                    RecentBookings.Add(booking);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load the dashboard. Pull to refresh to try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}