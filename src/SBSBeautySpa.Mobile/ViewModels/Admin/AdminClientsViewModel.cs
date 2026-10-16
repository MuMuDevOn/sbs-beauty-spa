using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels.Admin
{
    public partial class AdminClientsViewModel : BaseViewModel
    {
        private readonly IAdminClientsService _clients;
        private List<ClientSummary> _allClients = new();

        public AdminClientsViewModel(IAdminClientsService clients)
        {
            _clients = clients;
            Title = "Manage Clients";
        }

        public ObservableCollection<ClientSummary> Clients { get; } = new();

        [ObservableProperty]
        private string searchText = string.Empty;

        [ObservableProperty]
        private bool isEmpty;

        partial void OnSearchTextChanged(string value) => ApplyFilter();

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                _allClients = await _clients.ListClientsAsync();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load clients. Please try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilter()
        {
            var query = SearchText?.Trim() ?? string.Empty;

            var filtered = query.Length == 0
                ? _allClients
                : _allClients.Where(c =>
                    c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    c.Email.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    c.PhoneNumber.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            Clients.Clear();
            foreach (var client in filtered.OrderBy(c => c.Name))
                Clients.Add(client);

            IsEmpty = Clients.Count == 0;
        }
    }
}