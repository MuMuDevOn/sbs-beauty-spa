namespace SBSBeautySpa.Mobile.Views;

public partial class AuthShell : Shell
{
    public AuthShell()
    {
        InitializeComponent();

        // Second-step screens (reached from Log In / Sign Up)
        Routing.RegisterRoute("LoginCodePage", typeof(LoginCodePage));
        Routing.RegisterRoute("VerifyEmailPage", typeof(VerifyEmailPage));
    }
}  