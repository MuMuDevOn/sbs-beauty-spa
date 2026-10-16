using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SBSBeautySpa.Mobile.Models;
using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.ViewModels
{
    /// <summary>
    /// Backs the Gallery screen: a category sidebar and a staggered
    /// two-column image grid.
    ///
    /// The staggered look in the wireframe (tiles of different heights)
    /// comes from each image keeping its own aspect ratio, which
    /// CollectionView's grid layout can't do (it forces equal row heights).
    /// So images are dealt alternately into two columns (LeftColumn /
    /// RightColumn) and the View stacks each column independently.
    /// Alternating by index doesn't balance column heights perfectly —
    /// heights aren't known until images load — but it's close enough in
    /// practice.
    /// </summary>
    public partial class GalleryViewModel : BaseViewModel
    {
        private readonly IFirestoreService _firestore;
        private int _imageLoadVersion;

        public GalleryViewModel(IFirestoreService firestore)
        {
            _firestore = firestore;
            Title = "Gallery";
        }

        public ObservableCollection<SelectableTab> Categories { get; } = new();
        public ObservableCollection<GalleryImage> LeftColumn { get; } = new();
        public ObservableCollection<GalleryImage> RightColumn { get; } = new();

        [ObservableProperty]
        private bool isLoadingImages;

        [ObservableProperty]
        private bool isEmpty;

        [RelayCommand]
        private async Task LoadAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;
            ErrorMessage = null;

            try
            {
                var categories = await _firestore.GetGalleryCategoriesAsync();

                Categories.Clear();
                foreach (var category in categories.Where(c => c.IsActive).OrderBy(c => c.Name))
                {
                    Categories.Add(new SelectableTab(category.Id, category.Name));
                }

                if (Categories.Count == 0)
                {
                    LeftColumn.Clear();
                    RightColumn.Clear();
                    IsEmpty = true;
                    return;
                }

                Categories[0].IsSelected = true; // wireframe opens with the first category (Almond) selected
                await LoadImagesAsync(Categories[0].Id);
            }
            catch (Exception ex)
            {
                ErrorMessage = "Couldn't load the gallery. Check your connection and try again.";
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SelectCategoryAsync(SelectableTab tab)
        {
            if (tab.IsSelected)
            {
                return;
            }

            foreach (var category in Categories)
            {
                category.IsSelected = false;
            }
            tab.IsSelected = true;

            await LoadImagesAsync(tab.Id);
        }

        private async Task LoadImagesAsync(string? categoryId)
        {
            // Each call gets a version number; if the person taps another
            // category before this one finishes, the older result is
            // discarded instead of overwriting the newer one.
            var version = ++_imageLoadVersion;

            LeftColumn.Clear();
            RightColumn.Clear();
            IsEmpty = false;
            IsLoadingImages = true;
            ErrorMessage = null;

            try
            {
                var images = await _firestore.GetGalleryImagesAsync(categoryId);
                if (version != _imageLoadVersion)
                {
                    return;
                }

                for (var i = 0; i < images.Count; i++)
                {
                    (i % 2 == 0 ? LeftColumn : RightColumn).Add(images[i]);
                }
                IsEmpty = images.Count == 0;
            }
            catch (Exception ex)
            {
                if (version == _imageLoadVersion)
                {
                    ErrorMessage = "Couldn't load these images. Please try again.";
                }
                System.Diagnostics.Debug.WriteLine(ex);
            }
            finally
            {
                if (version == _imageLoadVersion)
                {
                    IsLoadingImages = false; // a newer load is still running otherwise — leave its spinner alone
                }
            }
        }
    }
}
