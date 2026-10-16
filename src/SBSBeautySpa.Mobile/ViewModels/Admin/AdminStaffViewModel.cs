using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    public partial class AdminStaffViewModel : BaseViewModel
    {
        private readonly IAdminUsersService _users;

        public AdminStaffViewModel(IAdminUsersService users)
        {
            _users = users;
            Title = "Manage Staff";
        }

        public ObservableCollection<AdminUser> Staff { get; } = new();

        [ObservableProperty]
        private bool isEmpty;

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var admins = await _users.ListAdminsAsync();
                Staff.Clear();
                foreach (var a in admins.OrderByDescending(a => a.SuperAdmin).ThenBy(a => a.Email))
                    Staff.Add(a);

                IsEmpty = Staff.Count == 0;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load staff. Please try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ToggleAdminAsync(AdminUser user)
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = null;

            try
            {
                await _users.SetAdminAsync(user.Uid, !user.Admin);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ToggleSuperAdminAsync(AdminUser user)
        {
            if (IsBusy) return;
            IsBusy = true;
            ErrorMessage = null;

            try
            {
                await _users.SetSuperAdminAsync(user.Uid, !user.SuperAdmin);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}