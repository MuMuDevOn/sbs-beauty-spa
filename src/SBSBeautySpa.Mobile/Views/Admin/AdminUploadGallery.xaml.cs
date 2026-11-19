using System.Collections.ObjectModel;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Maui.Controls.Shapes;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.Views.Admin;

// One photo already in the gallery (shown in the list under the form).
public sealed class GalleryRow
{
    public string Id { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string ShapeName { get; init; } = string.Empty;
}

public partial class AdminUploadGalleryPage : ContentPage
{
    // Your Firebase Storage bucket (same one the Products page uses).
    private const string StorageBucket = "sbs-beauty-spa.firebasestorage.app";
    private const long MaxPhotoBytes = 5 * 1024 * 1024;

    private static readonly HttpClient Http = new();

    private readonly IFirebaseAuthService _auth;
    private readonly IGalleryAdminService _gallery;
    private readonly IFirestoreService _firestore;

    private readonly ObservableCollection<GalleryRow> _rows = new();
    private FileResult? _photo;
    private bool _busy;

    public AdminUploadGalleryPage(IFirebaseAuthService auth, IGalleryAdminService gallery, IFirestoreService firestore)
    {
        InitializeComponent();
        _auth = auth;
        _gallery = gallery;
        _firestore = firestore;
        CategoryPicker.SelectedIndex = 0;   // Almond
        BindableLayout.SetItemsSource(PhotoList, _rows);
        SizeChanged += OnPageSizeChanged;
    }

    // Phones use the full width; wide windows get a centred column.
    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0) return;
        var w = Math.Max(0, Math.Min(Width - 40, 640));
        ContentColumn.WidthRequest = w;
        PublishButton.WidthRequest = w;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPhotosAsync();
    }

    // ----- Photos already in the gallery -----
    private async Task LoadPhotosAsync()
    {
        try
        {
            var categories = await _firestore.GetGalleryCategoriesAsync();
            var shapeNames = new Dictionary<string, string>();
            foreach (var cat in categories)
                if (!string.IsNullOrEmpty(cat.Id)) shapeNames.TryAdd(cat.Id, cat.Name ?? string.Empty);

            var images = await _firestore.GetGalleryImagesAsync();

            _rows.Clear();
            foreach (var img in images)
            {
                _rows.Add(new GalleryRow
                {
                    Id = img.Id,
                    ImageUrl = img.ImageUrl,
                    Title = string.IsNullOrWhiteSpace(img.Title) ? "Untitled" : img.Title,
                    ShapeName = shapeNames.TryGetValue(img.CategoryId ?? string.Empty, out var n) && n.Length > 0 ? n : "No shape",
                });
            }
            UpdateListTitle();
            ShowList(null, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            ShowList("Couldn't load the gallery photos. Please try again.", null);
        }
    }

    // Remove: confirm, delete the record, then delete the picture file.
    private async void OnRemoveClicked(object? sender, EventArgs e)
    {
        if (_busy) return;
        if ((sender as Button)?.CommandParameter is not GalleryRow row) return;

        var ok = await DisplayAlertAsync(
            "Remove photo",
            $"Remove \"{row.Title}\" from the gallery? Clients will no longer see it.",
            "Remove", "Cancel");
        if (!ok) return;

        SetBusy(true);
        ShowList(null, null);
        try
        {
            // 1. Delete the gallery record (this is what makes it vanish for clients).
            await _gallery.DeleteGalleryImageAsync(row.Id);
            _rows.Remove(row);
            UpdateListTitle();

            // 2. Delete the picture file from Storage so nothing is left behind.
            var fileGone = await TryDeleteStorageFileAsync(row.ImageUrl);
            ShowList(null, fileGone
                ? "Photo removed."
                : "Photo removed from the gallery, but its file couldn't be deleted from storage.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            ShowList("Couldn't remove the photo: " + ex.Message, null);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // Deletes gallery/... from Firebase Storage. Only ever touches this bucket's gallery/ folder.
    private async Task<bool> TryDeleteStorageFileAsync(string imageUrl)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return true;
            if (!imageUrl.Contains($"/b/{StorageBucket}/o/", StringComparison.Ordinal)) return true;   // not our file

            var start = imageUrl.IndexOf("/o/", StringComparison.Ordinal) + 3;
            var end = imageUrl.IndexOf('?', start);
            var encoded = end >= 0 ? imageUrl[start..end] : imageUrl[start..];
            var objectName = Uri.UnescapeDataString(encoded);
            if (!objectName.StartsWith("gallery/", StringComparison.Ordinal)) return true;          // never touch other folders

            var token = await _auth.GetIdTokenAsync();
            if (string.IsNullOrEmpty(token)) return false;

            var url = $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o/{Uri.EscapeDataString(objectName)}";
            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Firebase", token);

            using var response = await Http.SendAsync(request);
            return response.IsSuccessStatusCode || (int)response.StatusCode == 404;   // 404 = already gone
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            return false;
        }
    }

    private void UpdateListTitle()
        => ListTitle.Text = _rows.Count == 1 ? "1 PHOTO IN THE GALLERY" : $"{_rows.Count} PHOTOS IN THE GALLERY";

    private void ShowList(string? error, string? info)
    {
        ListErrorLabel.Text = error ?? string.Empty;
        ListErrorLabel.IsVisible = !string.IsNullOrEmpty(error);
        ListInfoLabel.Text = info ?? string.Empty;
        ListInfoLabel.IsVisible = !string.IsNullOrEmpty(info);
    }

    // ----- Photo -----
    private async void OnReplacePhotoClicked(object? sender, EventArgs e)
    {
        if (_busy) return;
        try
        {
            FileResult? photo = await MediaPicker.Default.PickPhotoAsync();
            if (photo is null) return;

            _photo = photo;
            PhotoPreview.Source = ImageSource.FromFile(photo.FullPath);
            PhotoStatusLabel.Text = "Photo selected";
            ChoosePhotoButton.Text = "Replace photo";
            ShowError(null);
        }
        catch (Exception)
        {
            await DisplayAlertAsync("Photo", "Couldn't open the photo picker", "OK");
        }
    }

    private void OnRemovePhotoTapped(object? sender, TappedEventArgs e)
    {
        if (_busy) return;
        _photo = null;
        PhotoPreview.Source = null;
        PhotoStatusLabel.Text = "No photo selected";
        ChoosePhotoButton.Text = "Choose photo";
    }

    // ----- Category -----
    private void OnChevronTapped(object? sender, TappedEventArgs e)
        => CategoryPicker.Focus();

    private void OnCategoryChanged(object? sender, EventArgs e)
    {
        if (CategoryPicker.SelectedItem is string category)
            InfoLabel.Text = $"Published photos appear in the {category} gallery with your title and style tags.";
    }

    // ----- Style tags -----
    private async void OnAddTagTapped(object? sender, TappedEventArgs e)
    {
        string? tag = await DisplayPromptAsync("Add tag", "Enter a style tag", "Add", "Cancel", "e.g. Chrome");
        if (string.IsNullOrWhiteSpace(tag)) return;

        TagsLayout.Insert(TagsLayout.Children.Count - 1, MakeTagChip(tag.Trim()));
    }

    private void OnRemoveTagTapped(object? sender, TappedEventArgs e)
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
            TextColor = Color.FromArgb("#2B1B6B"),
            VerticalOptions = LayoutOptions.Center
        };

        var close = new Label
        {
            Text = "✕",
            FontSize = 12,
            TextColor = Color.FromArgb("#6E63A6"),
            VerticalOptions = LayoutOptions.Center
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += OnRemoveTagTapped;
        close.GestureRecognizers.Add(tap);

        return new Border
        {
            BackgroundColor = Color.FromArgb("#EFE9FF"),
            Stroke = Color.FromArgb("#CFC6EE"),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
            Padding = new Thickness(10, 6),
            Margin = new Thickness(0, 0, 8, 8),
            Content = new HorizontalStackLayout { Spacing = 8, Children = { label, close } }
        };
    }

    private List<string> CurrentTags()
    {
        var tags = new List<string>();
        foreach (var child in TagsLayout.Children)
        {
            if (child is Border { Content: HorizontalStackLayout row } && row.Children.FirstOrDefault() is Label l
                && !string.IsNullOrWhiteSpace(l.Text))
                tags.Add(l.Text);
        }
        return tags;
    }

    // ----- Publish: upload the photo, find/create the category, save the gallery record -----
    private async void OnPublishClicked(object? sender, EventArgs e)
    {
        if (_busy) return;

        if (_photo is null)
        {
            ShowError("Please choose a photo first.");
            return;
        }
        if (string.IsNullOrWhiteSpace(TitleEntry.Text))
        {
            ShowError("Please enter a design title.");
            return;
        }
        if (CategoryPicker.SelectedItem is not string shape)
        {
            ShowError("Please choose a shape / category.");
            return;
        }

        SetBusy(true);
        ShowError(null);
        try
        {
            // 1. Upload the picture to Firebase Storage.
            var imageUrl = await UploadPhotoAsync(_photo);

            // 2. Find the category for this shape, or create it the first time.
            var categories = await _firestore.GetGalleryCategoriesAsync();
            var match = categories.FirstOrDefault(c =>
                string.Equals(c.Name?.Trim(), shape, StringComparison.OrdinalIgnoreCase));
            var categoryId = match?.Id ?? await _gallery.CreateGalleryCategoryAsync(shape);

            // 3. Save the gallery record (the client Gallery reads this).
            var caption = CaptionEditor.Text?.Trim() ?? string.Empty;
            var tags = CurrentTags();
            var description = tags.Count == 0
                ? caption
                : (caption.Length == 0 ? "" : caption + "\n\n") + "Style: " + string.Join(", ", tags);

            await _gallery.AddGalleryImageAsync(categoryId, imageUrl, TitleEntry.Text.Trim(), description);

            await DisplayAlertAsync("Published", $"Your photo is now in the {shape} gallery.", "OK");
            ResetForm();
            await LoadPhotosAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            ShowError(ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // Uploads the photo to Firebase Storage (gallery/...) and returns its link.
    private async Task<string> UploadPhotoAsync(FileResult photo)
    {
        var idToken = await _auth.GetIdTokenAsync();
        if (string.IsNullOrEmpty(idToken))
            throw new InvalidOperationException("Please log in again.");

        var ext = System.IO.Path.GetExtension(photo.FileName)?.ToLowerInvariant() ?? ".jpg";
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg",
        };

        byte[] bytes;
        using (var stream = await photo.OpenReadAsync())
        using (var ms = new MemoryStream())
        {
            await stream.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        if (bytes.Length > MaxPhotoBytes)
            throw new InvalidOperationException("That photo is over 5 MB. Please choose a smaller one.");

        var objectName = $"gallery/{Guid.NewGuid():N}{ext}";
        var uploadUrl =
            $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o?name={Uri.EscapeDataString(objectName)}";

        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Firebase", idToken);

        using var response = await Http.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException((int)response.StatusCode switch
            {
                401 or 403 => "Storage refused the upload. Log out and log in again with a fresh code, and check the Storage rules allow admins to write to gallery/.",
                404 => "Storage bucket not found. Check Storage is enabled and the bucket name in AdminUploadGalleryPage.",
                _ => body,
            });
        }

        using var doc = JsonDocument.Parse(body);
        var name = doc.RootElement.GetProperty("name").GetString() ?? objectName;
        var token = doc.RootElement.TryGetProperty("downloadTokens", out var t) ? t.GetString() : null;

        return $"https://firebasestorage.googleapis.com/v0/b/{StorageBucket}/o/{Uri.EscapeDataString(name)}?alt=media"
               + (string.IsNullOrEmpty(token) ? "" : $"&token={token}");
    }

    // ----- Helpers -----
    private void SetBusy(bool busy)
    {
        _busy = busy;
        PublishButton.IsEnabled = !busy;
        PublishButton.Text = busy ? "Publishing..." : "Publish to Gallery";
        ChoosePhotoButton.IsEnabled = !busy;
        Busy.IsVisible = busy;
        Busy.IsRunning = busy;
    }

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message ?? string.Empty;
        ErrorLabel.IsVisible = !string.IsNullOrEmpty(message);
    }

    private void ResetForm()
    {
        _photo = null;
        PhotoPreview.Source = null;
        PhotoStatusLabel.Text = "No photo selected";
        ChoosePhotoButton.Text = "Choose photo";
        TitleEntry.Text = string.Empty;
        CaptionEditor.Text = string.Empty;
        CategoryPicker.SelectedIndex = 0;

        // Remove the tag chips but keep the "+ Add tag" button (the last child).
        for (var i = TagsLayout.Children.Count - 2; i >= 0; i--)
            TagsLayout.Remove((IView)TagsLayout.Children[i]);
    }
}