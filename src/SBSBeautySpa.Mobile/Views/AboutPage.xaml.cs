namespace SBSBeautySpa.Mobile.Views;

public partial class AboutPage : ContentPage
{
    private static readonly Color Heading = Color.FromArgb("#2B1B6B");
    private static readonly Color Body = Color.FromArgb("#4A4272");

    private const string Background =
        "SBS Beauty Spa is a nails, lashes and press-ons studio. Browse services, make and manage bookings, explore the shop and view the gallery. Keep your profile, payment methods and notification preferences together in your account.";

    public AboutPage()
    {
        InitializeComponent();

        AddParagraph(AboutBody, Background);
        Fill(TermsBody, LegalContent.Terms);
        Fill(PrivacyBody, LegalContent.Privacy);
        Fill(CancellationBody, LegalContent.Cancellation);

        UpdatedLabel.Text = $"Legal documents last updated: {LegalContent.LastUpdated}";
        SizeChanged += OnPageSizeChanged;
    }

    // Phones use the full width; wide windows get a readable centred column.
    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0) return;
        ContentColumn.WidthRequest = Math.Max(0, Math.Min(Width - 40, 720));
    }

    private static void AddParagraph(VerticalStackLayout host, string text)
        => host.Children.Add(new Label
        {
            Text = text,
            FontFamily = "OpenSansRegular",
            FontSize = 14,
            LineHeight = 1.25,
            TextColor = Body,
        });

    private static void Fill(VerticalStackLayout host, LegalDocument doc)
    {
        var first = true;
        foreach (var section in doc.Sections)
        {
            host.Children.Add(new Label
            {
                Text = section.Heading,
                FontFamily = "OpenSansSemibold",
                FontSize = 15,
                TextColor = Heading,
                Margin = new Thickness(0, first ? 0 : 12, 0, 0),
            });
            AddParagraph(host, section.Body);
            first = false;
        }
    }

    // Opens or closes one section and turns its arrow.
    private static async void Toggle(VerticalStackLayout body, Image chevron)
    {
        body.IsVisible = !body.IsVisible;
        await chevron.RotateToAsync(body.IsVisible ? 180 : 0, 180);
    }

    private void OnAboutTapped(object? sender, TappedEventArgs e) => Toggle(AboutBody, AboutChevron);

    private void OnTermsTapped(object? sender, TappedEventArgs e) => Toggle(TermsBody, TermsChevron);

    private void OnPrivacyTapped(object? sender, TappedEventArgs e) => Toggle(PrivacyBody, PrivacyChevron);

    private void OnCancellationTapped(object? sender, TappedEventArgs e) => Toggle(CancellationBody, CancellationChevron);

    private async void OnBackTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");
}