namespace SBSBeautySpa.Mobile.Views;

// Shows the Terms, Privacy Policy or Cancellation Policy (chosen with ?doc=...).
public partial class LegalPage : ContentPage, IQueryAttributable
{
    private static readonly Color Heading = Color.FromArgb("#2B1B6B");
    private static readonly Color Body = Color.FromArgb("#4A4272");

    public LegalPage()
    {
        InitializeComponent();
        SizeChanged += OnPageSizeChanged;
    }

    // Phones use the full width; wide windows get a readable centred column.
    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0) return;
        ContentColumn.WidthRequest = Math.Max(0, Math.Min(Width - 40, 720));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        var key = query.TryGetValue("doc", out var v) ? v?.ToString() : null;
        var doc = LegalContent.Get(key);

        TitleLabel.Text = doc.Title;
        UpdatedLabel.Text = $"Last updated: {LegalContent.LastUpdated}";

        BodyStack.Children.Clear();
        var first = true;
        foreach (var section in doc.Sections)
        {
            BodyStack.Children.Add(new Label
            {
                Text = section.Heading,
                FontFamily = "OpenSansSemibold",
                FontSize = 15,
                TextColor = Heading,
                Margin = new Thickness(0, first ? 0 : 12, 0, 0),
            });
            BodyStack.Children.Add(new Label
            {
                Text = section.Body,
                FontFamily = "OpenSansRegular",
                FontSize = 14,
                LineHeight = 1.25,
                TextColor = Body,
            });
            first = false;
        }
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");
}
