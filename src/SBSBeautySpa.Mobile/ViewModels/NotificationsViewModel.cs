using CommunityToolkit.Mvvm.ComponentModel;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class NotificationsViewModel : BaseViewModel
    {
        private readonly IClientProfileService _profile;

        public NotificationsViewModel(IClientProfileService profile)
        {
            _profile = profile;
            Title = "Notifications";
        }

        [ObservableProperty] private bool bookingConfirmations = true;
        [ObservableProperty] private bool bookingChanges = true;
        [ObservableProperty] private bool appointmentReminders = true;

        // Add a LoadAsync command later once preferences API exists
    }
}