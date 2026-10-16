using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NodaTime;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    public partial class AddOnOption : ObservableObject
    {
        public Service Service { get; }

        [ObservableProperty]
        private bool isSelected;

        public AddOnOption(Service service) => Service = service;
    }

    public partial class AddOnsViewModel : BaseViewModel
    {
        private readonly IFirestoreService _firestoreService;

        public AddOnsViewModel(IFirestoreService firestoreService)
        {
            _firestoreService = firestoreService;
            Title = "Add-Ons";
        }

        public string PrimaryServiceId { get; set; } = string.Empty;
        public LocalDate SelectedDate { get; set; }
        public LocalTime SelectedStartTime { get; set; }

        [ObservableProperty]
        private Service? primaryService;

        public ObservableCollection<AddOnOption> AddOnOptions { get; } = new();

        public decimal EstimatedAddOnTotal => AddOnOptions.Where(o => o.IsSelected).Sum(o => o.Service.Price);

        public decimal EstimatedTotal => (PrimaryService?.Price ?? 0) + EstimatedAddOnTotal;

        public IReadOnlyList<string> SelectedServiceIds =>
            new[] { PrimaryServiceId }.Concat(AddOnOptions.Where(o => o.IsSelected).Select(o => o.Service.Id)).ToList();

        [RelayCommand]
        private async Task LoadAddOnsAsync()
        {
            if (string.IsNullOrEmpty(PrimaryServiceId) || IsBusy) return;

            IsBusy = true;
            ErrorMessage = null;
            AddOnOptions.Clear();

            try
            {
                PrimaryService = await _firestoreService.GetServiceAsync(PrimaryServiceId);

                if (PrimaryService is null || PrimaryService.AddOnServiceIds.Count == 0) return;

                var addOnServices = await _firestoreService.GetServicesByIdsAsync(PrimaryService.AddOnServiceIds);

                foreach (var service in addOnServices.Where(s => s.IsActive))
                {
                    var option = new AddOnOption(service);

                    option.PropertyChanged += (_, __) =>
                    {
                        OnPropertyChanged(nameof(EstimatedAddOnTotal));
                        OnPropertyChanged(nameof(EstimatedTotal));
                    };

                    AddOnOptions.Add(option);
                }

                OnPropertyChanged(nameof(EstimatedAddOnTotal));
                OnPropertyChanged(nameof(EstimatedTotal));
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load add-ons. You can still continue without any.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnPrimaryServiceChanged(Service? value)
        {
            OnPropertyChanged(nameof(EstimatedTotal));
        }
    }
}