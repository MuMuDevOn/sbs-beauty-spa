using SBSBeautySpa.Mobile.ViewModels.Admin;

namespace SBSBeautySpa.Mobile.Views.Admin;

public partial class AdminDashboardPage : ContentPage
{
    private readonly AdminDashboardViewModel _viewModel;

    public AdminDashboardPage(AdminDashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadDashboardCommand.Execute(null);
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnManageBookingsTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AdminBookingsPage));

    private async void OnManageServicesTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AdminServicesPage));

    private async void OnManageProductsTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AdminProductPage));

    private async void OnBusinessHoursTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AdminHoursPage));

    private async void OnUploadGalleryTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AdminUploadGalleryPage));

    private async void OnManageClientsTapped(object sender, TappedEventArgs e)
    => await Shell.Current.GoToAsync(nameof(AdminClientsPage));

    private async void OnManageStaffTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(AdminStaffPage));
}