using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    // This class controls the admin dashboard overview screen.
    //
    // It shows three main numbers:
    //   1. Upcoming bookings count
    //   2. Revenue this month
    //   3. Pending bookings count
    //
    // Plus a short list of recent bookings.
    //
    // This screen is READ-ONLY. It only shows information.
    // If the admin wants to actually change something (like confirm a
    // booking), they go to the "Manage Bookings" screen, which uses
    // AdminBookingsViewModel.
    public partial class AdminDashboardViewModel : BaseViewModel
    {
        private readonly IAdminDataService _adminDataService;

        public AdminDashboardViewModel(IAdminDataService adminDataService)
        {
            _adminDataService = adminDataService;
            Title = "Dashboard";
        }

        // How many bookings are coming up.
        [ObservableProperty]
        private int upcomingBookingsCount;

        // How much money came in this month.
        [ObservableProperty]
        private decimal revenueThisMonth;

        // How many bookings are still pending.
        [ObservableProperty]
        private int pendingCount;

        // A short list of the most recent bookings.
        public ObservableCollection<Booking> RecentBookings { get; } = new();

        // Loads all the dashboard data from the server.
        [RelayCommand]
        private async Task LoadDashboardAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                // Ask the server for the summary.
                var summary = await _adminDataService.GetDashboardSummaryAsync();

                // Update the three numbers.
                UpcomingBookingsCount = summary.UpcomingBookingsCount;
                RevenueThisMonth = summary.RevenueThisMonth;
                PendingCount = summary.PendingCount;

                // Replace the recent bookings list.
                RecentBookings.Clear();
                foreach (var booking in summary.RecentBookings)
                {
                    RecentBookings.Add(booking);
                }
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