using SBSBeautySpa.Mobile.Services;
using SBSBeautySpa.Mobile.ViewModels.Admin;

namespace SBSBeautySpa.Mobile.Views.Admin;

public partial class AdminStaffPage : ContentPage
{
    private readonly AdminStaffViewModel _viewModel;
    private readonly IFirebaseAuthService _auth;
    private bool _alertShown;

    // Bound from the XAML: true only for a super admin
    private bool _isSuperAdmin;
    public bool IsSuperAdmin
    {
        get => _isSuperAdmin;
        private set
        {
            if (_isSuperAdmin == value) return;
            _isSuperAdmin = value;
            OnPropertyChanged(nameof(IsSuperAdmin));
            OnPropertyChanged(nameof(IsNotSuperAdmin));
        }
    }
    public bool IsNotSuperAdmin => !_isSuperAdmin;

    public AdminStaffPage(AdminStaffViewModel viewModel, IFirebaseAuthService auth)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _auth = auth;
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);

        // Read the signed-in user's super-admin claim from their token
        var token = await _auth.GetIdTokenAsync();
        IsSuperAdmin = TokenClaims.Read(token, "superAdmin") == "true";

        if (!IsSuperAdmin && !_alertShown)
        {
            _alertShown = true;
            await DisplayAlertAsync("Super admin only",
                "You are not a super admin, so you cannot edit staff. " +
                "Only a super admin can promote or demote staff.",
                "OK");
        }
    }

    // Back arrow: go back if this page was opened on top of another one,
    // otherwise (it is the Staff tab) jump to the Dashboard tab.
    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        try
        {
            if (Navigation.NavigationStack.Count > 1)
                await Shell.Current.GoToAsync("..");
            else
                await Shell.Current.GoToAsync("//AdminMain/AdminDashboard");
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Couldn't go back", ex.Message, "OK");
        }
    }
}