using SBSBeautySpa.Mobile.ViewModels;

namespace SBSBeautySpa.Mobile.Views
{
    public partial class HomePage : ContentPage
    {
        // Layout is worked out from the real window width, so the page fits phones,
        // tablets and wide desktop windows.
        private const double PhoneBreakpoint = 640;    // below this: phone layout (2 columns)
        private const double WideBreakpoint = 900;     // at/above this: wide layout (4 columns)
        private const double MaxContentWidth = 1100;   // the column never gets wider than this
        private const double SidePadding = 20;         // must match the Padding in the XAML
        private const double CardGap = 12;             // space between cards
        private const double CardInnerPadding = 10;

        private readonly HomeViewModel _viewModel;
        private IDispatcherTimer? _bannerTimer;

        public HomePage(HomeViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = _viewModel;

            SizeChanged += OnPageSizeChanged;

            // New Arrivals fill in after the page is built: re-work the layout when the count changes.
            _viewModel.NewArrivals.CollectionChanged += (_, _) => OnPageSizeChanged(null, EventArgs.Empty);
        }

        // ----- Sliding banner -----

        private void StartBannerTimer()
        {
            if (_bannerTimer is null)
            {
                _bannerTimer = Dispatcher.CreateTimer();
                _bannerTimer.Interval = TimeSpan.FromSeconds(4);
                _bannerTimer.Tick += (_, _) =>
                {
                    var count = _viewModel.BannerSlides.Count;
                    if (count < 2) return;
                    BannerCarousel.Position = (BannerCarousel.Position + 1) % count;
                };
            }
            _bannerTimer.Start();
        }

        // Tap a slide: a service opens its booking, a product opens the shop.
        private void OnBannerTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is not HomeBannerCard slide) return;
            if (slide.ServiceId is not null)
                _viewModel.OpenServiceCommand.Execute(slide.ServiceId);
            else
                _viewModel.OpenProductCommand.Execute(null);
        }

        // Phones fill the screen with 2 card columns; tablets get 3; wide windows get 4.
        // Service rows become 2 columns once the screen is wider than a phone.
        // Every photo (any size or shape) is cropped to fit its card.
        private void OnPageSizeChanged(object? sender, EventArgs e)
        {
            if (Width <= 0) return;

            var columnWidth = Math.Min(Width, MaxContentWidth);
            if (this.FindByName("ContentColumn") is VisualElement column)
                column.WidthRequest = columnWidth;

            var inner = columnWidth - (SidePadding * 2);

            var cardColumns = columnWidth >= WideBreakpoint ? 4 : columnWidth >= PhoneBreakpoint ? 3 : 2;
            var cardWidth = Math.Floor((inner - (CardGap * (cardColumns - 1))) / cardColumns) - 1;
            var imageWidth = cardWidth - (CardInnerPadding * 2);

            // Wide Windows window only: services stack one per row, and the New Arrivals
            // cards share the full width (2 cards = half each). Phones and narrow windows are unchanged.
            var wideWindows = DeviceInfo.Platform == DevicePlatform.WinUI && columnWidth >= PhoneBreakpoint;

            var rowColumns = wideWindows ? 1 : (columnWidth >= PhoneBreakpoint ? 2 : 1);
            var rowWidth = Math.Floor((inner - (CardGap * (rowColumns - 1))) / rowColumns) - 1;

            var productColumns = cardColumns;
            var arrivals = _viewModel.NewArrivals.Count;
            if (wideWindows && arrivals > 0 && arrivals < cardColumns)
                productColumns = arrivals;
            var productCardWidth = Math.Floor((inner - (CardGap * (productColumns - 1))) / productColumns) - 1;
            var productImageWidth = productCardWidth - (CardInnerPadding * 2);

            Resources["CardWidth"] = cardWidth;
            Resources["CardImageWidth"] = imageWidth;
            Resources["CardImageHeight"] = Math.Clamp(Math.Floor(imageWidth * 0.85), 110, 190);
            Resources["RowWidth"] = rowWidth;

            Resources["ProductCardWidth"] = productCardWidth;
            Resources["ProductImageWidth"] = productImageWidth;
            Resources["ProductImageHeight"] = Math.Clamp(Math.Floor(productImageWidth * 0.85), 110, 190);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _viewModel.LoadCommand.Execute(null);
            _ = RefreshBellAsync();
            StartBannerTimer();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _bannerTimer?.Stop();
        }

        // Shows the red dot on the bell when there are unread notifications.
        private async Task RefreshBellAsync()
        {
            try
            {
                var svc = IPlatformApplication.Current?.Services.GetService<SBSBeautySpa.Mobile.Services.InAppNotificationsService>();
                if (svc is null) return;
                var (_, unread) = await svc.ListAsync();
                UnreadDot.IsVisible = unread > 0;
            }
            catch
            {
                UnreadDot.IsVisible = false;   // not signed in, or offline: no dot
            }
        }

        private static readonly (string Key, string Icon, string Search)[] Shortcuts =
        {
            ("Nails",    "cat_nails",    "Nails"),
            ("Lashes",   "cat_lashes",   "Lashes"),
            ("PressOns", "cat_pressons", "Press-On"),
            ("GelX",     "cat_gelx",     "Gel-X"),
            ("Shop",     "cat_shop",     ""),
        };
        private string? _selectedShortcut;

        // Category circles: icons are set here by file name (Resources\Images).
        // Tap = highlight (dark circle, white icon) and search that category; tap again to clear.
        private void OnCategoryShortcutTapped(object? sender, TappedEventArgs e)
        {
            var key = e.Parameter as string;
            if (key == null) return;

            if (key == "Shop")
            {
                _viewModel.ShopNowCommand.Execute(null);
                return;
            }

            _selectedShortcut = _selectedShortcut == key ? null : key;

            foreach (var s in Shortcuts)
            {
                bool on = s.Key == _selectedShortcut;
                if (this.FindByName("Cat" + s.Key + "Circle") is Border circle)
                    circle.BackgroundColor = on ? Color.FromArgb("#111827") : Colors.White;
                if (this.FindByName("Cat" + s.Key + "Icon") is Image icon)
                    icon.Source = on ? s.Icon + "_active.svg" : s.Icon + ".svg";
            }

            _viewModel.SearchText = _selectedShortcut == null
                ? string.Empty
                : Shortcuts.First(s => s.Key == _selectedShortcut).Search;
        }

        private void OnCategoryTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is HomeCategory category)
                _viewModel.OpenCategoryCommand.Execute(category.Id);
        }

        private void OnFilterChipTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is FilterChip chip)
                _viewModel.SelectFilterChipCommand.Execute(chip);
        }

        private void OnServiceTapped(object? sender, TappedEventArgs e)
        {
            if ((sender as BindableObject)?.BindingContext is HomeServiceCard card)
                _viewModel.OpenServiceCommand.Execute(card.Id);
        }

        private void OnProductTapped(object? sender, TappedEventArgs e)
        {
            _viewModel.OpenProductCommand.Execute(null);
        }
    }
}