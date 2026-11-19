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

        // The "Add staff member" box
        [ObservableProperty]
        private string newStaffEmail = string.Empty;

        // Green success message shown under the box
        [ObservableProperty]
        private string? infoMessage;

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
                ErrorMessage = "Couldn't load staff: " + ex.Message;
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Makes an existing account staff, by email.
        [RelayCommand]
        private async Task AddStaffAsync()
        {
            if (IsBusy) return;

            var email = (NewStaffEmail ?? string.Empty).Trim();
            ErrorMessage = null;
            InfoMessage = null;

            if (string.IsNullOrEmpty(email) || !email.Contains('@'))
            {
                ErrorMessage = "Enter the email of someone who has already registered in the app.";
                return;
            }

            IsBusy = true;
            var done = false;

            try
            {
                await _users.AddStaffByEmailAsync(email);
                NewStaffEmail = string.Empty;
                InfoMessage = $"{email} is now a staff member. They need to log in again to see the change.";
                done = true;
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

            // Reload AFTER IsBusy is cleared (LoadAsync does nothing while busy).
            if (done) await LoadAsync();
        }

        [RelayCommand]
        private async Task ToggleAdminAsync(AdminUser user)
        {
            if (IsBusy) return;

            var who = string.IsNullOrWhiteSpace(user.Email) ? "this person" : user.Email;
            var removing = user.Admin;
            var ok = await Shell.Current.DisplayAlertAsync(
                removing ? "Remove from staff" : "Make staff",
                removing ? $"Remove {who} from staff? They will lose admin access." : $"Make {who} a staff member?",
                removing ? "Remove" : "Make staff",
                "Cancel");
            if (!ok) return;

            IsBusy = true;
            ErrorMessage = null;
            InfoMessage = null;
            var done = false;

            try
            {
                await _users.SetAdminAsync(user.Uid, !user.Admin);
                InfoMessage = removing ? $"{who} was removed from staff." : $"{who} is now staff.";
                done = true;
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

            if (done) await LoadAsync();
        }

        [RelayCommand]
        private async Task ToggleSuperAdminAsync(AdminUser user)
        {
            if (IsBusy) return;

            var who = string.IsNullOrWhiteSpace(user.Email) ? "this person" : user.Email;
            var removing = user.SuperAdmin;
            var ok = await Shell.Current.DisplayAlertAsync(
                removing ? "Remove super admin" : "Make super admin",
                removing ? $"Remove super admin from {who}? They stay on as normal staff." : $"Make {who} a super admin? They will be able to manage all staff.",
                removing ? "Remove" : "Make super admin",
                "Cancel");
            if (!ok) return;

            IsBusy = true;
            ErrorMessage = null;
            InfoMessage = null;
            var done = false;

            try
            {
                await _users.SetSuperAdminAsync(user.Uid, !user.SuperAdmin);
                InfoMessage = removing ? $"{who} is no longer a super admin." : $"{who} is now a super admin.";
                done = true;
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

            if (done) await LoadAsync();
        }
    }
}