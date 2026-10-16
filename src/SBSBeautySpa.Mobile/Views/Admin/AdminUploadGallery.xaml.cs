
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Media;

namespace SBSBeautySpa.Mobile.Views.Admin;

public partial class AdminUploadGalleryPage : ContentPage
{
    public AdminUploadGalleryPage()
    {
        InitializeComponent();
        CategoryPicker.SelectedIndex = 0;   // Almond
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    // ----- Photo -----
    private async void OnReplacePhotoClicked(object sender, EventArgs e)
    {
        try
        {
            FileResult? photo = await MediaPicker.Default.PickPhotoAsync();
            if (photo is null) return;

            PhotoPreview.Source = ImageSource.FromFile(photo.FullPath);
            PhotoStatusLabel.Text = "Photo selected";
            // TODO: upload the photo to Firebase Storage
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Photo", "Couldn't open the photo picker", "OK");
        }
    }

    private void OnRemovePhotoTapped(object sender, TappedEventArgs e)
    {
        PhotoPreview.Source = null;
        PhotoStatusLabel.Text = "No photo selected";
    }

    // ----- Category -----
    private void OnChevronTapped(object sender, TappedEventArgs e)
        => CategoryPicker.Focus();

    private void OnCategoryChanged(object sender, EventArgs e)
    {
        if (CategoryPicker.SelectedItem is string category)
            InfoLabel.Text = $"Published photos appear in the {category} gallery with your title and style tags.";
    }

    // ----- Style tags -----
    private async void OnAddTagTapped(object sender, TappedEventArgs e)
    {
        string? tag = await DisplayPromptAsync("Add tag", "Enter a style tag", "Add", "Cancel", "e.g. Chrome");
        if (string.IsNullOrWhiteSpace(tag)) return;

        TagsLayout.Insert(TagsLayout.Children.Count - 1, MakeTagChip(tag.Trim()));
    }

    private void OnRemoveTagTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Element element) return;

        while (element.Parent is Element parent && !ReferenceEquals(parent, TagsLayout))
            element = parent;

        if (element is IView chip)
            TagsLayout.Remove(chip);
    }

    private Border MakeTagChip(string text)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 12,
            TextColor = Color.FromArgb("#111827"),
            VerticalOptions = LayoutOptions.Center
        };

        var close = new Image
        {
            Source = "xicon.png",
            WidthRequest = 12,
            HeightRequest = 12,
            VerticalOptions = LayoutOptions.Center
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnRemoveTagTapped;
        close.GestureRecognizers.Add(tap);

        return new Border
        {
            BackgroundColor = Color.FromArgb("#F3F4F6"),
            Stroke = Color.FromArgb("#D1D5DB"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 0, 8, 8),
            Content = new HorizontalStackLayout { Spacing = 8, Children = { label, close } }
        };
    }

    // Buttons 
    private async void OnSaveDraftClicked(object sender, EventArgs e)
    {
        // TODO: save as a draft (needs a backend endpoint)
        await DisplayAlertAsync("Draft saved", "Your design was saved as a draft", "OK");
    }

    private async void OnPublishClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TitleEntry.Text))
        {
            await DisplayAlertAsync("Missing title", "Please enter a design title", "OK");
            return;
        }

        // TODO: publish (photo upload + gallery record)
        await DisplayAlertAsync("Published", "Your photo is now in the gallery", "OK");
    }
}