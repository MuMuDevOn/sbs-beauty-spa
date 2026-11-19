using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.Views;

public partial class NotificationsPage : ContentPage
{
    private readonly InAppNotificationsService _notifications;
    private readonly PushTokenService? _push;
    private bool _loadingPush;

    public NotificationsPage(InAppNotificationsService notifications)
    {
        InitializeComponent();
        _notifications = notifications;
        _push = IPlatformApplication.Current?.Services.GetService<PushTokenService>();

        // Push only exists on phones.
        PushSection.IsVisible = _push is not null &&
            (DeviceInfo.Platform == DevicePlatform.Android || DeviceInfo.Platform == DevicePlatform.iOS);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
        await LoadPushPreferenceAsync();
    }

    private async Task LoadPushPreferenceAsync()
    {
        if (!PushSection.IsVisible) return;
        try
        {
            _loadingPush = true;
            PushSwitch.IsToggled = await _push.GetEnabledAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            _loadingPush = false;
        }
    }

    // Saves the on/off choice on the server; the server then skips push for this client when it's off.
    private async void OnPushToggled(object sender, ToggledEventArgs e)
    {
        if (_loadingPush || _push is null) return;
        try
        {
            await _push.SetEnabledAsync(e.Value);
        }
        catch (Exception ex)
        {
            _loadingPush = true;
            PushSwitch.IsToggled = !e.Value; // put it back, the change didn't save
            _loadingPush = false;
            await DisplayAlertAsync("Couldn't save", ex.Message, "OK");
        }
    }

    private async Task LoadAsync()
    {
        Spinner.IsVisible = Spinner.IsRunning = true;
        RecentList.Children.Clear();

        try
        {
            var (items, unread) = await _notifications.ListAsync();

            if (items.Count == 0)
            {
                RecentList.Children.Add(new Label
                {
                    Text = "Nothing yet. Updates about your bookings and replies from us will appear here.",
                    Style = (Style)Application.Current!.Resources["HintText"],
                });
            }

            foreach (var n in items)
                RecentList.Children.Add(BuildRow(n));

            // Opening the list counts as reading it.
            if (unread > 0)
                _ = _notifications.MarkAllReadAsync();
        }
        catch (Exception ex)
        {
            RecentList.Children.Add(new Label
            {
                Text = "Couldn't load your notifications: " + ex.Message,
                TextColor = Color.FromArgb("#FFD7D2"),
                FontSize = 13,
            });
        }
        finally
        {
            Spinner.IsVisible = Spinner.IsRunning = false;
        }
    }

    private static View BuildRow(InAppNotification n)
    {
        var text = new VerticalStackLayout { Spacing = 3 };
        text.Add(new Label
        {
            Text = n.Message,
            Style = (Style)Application.Current!.Resources["RowTitle"],
            FontFamily = n.IsRead ? "OpenSansRegular" : "OpenSansSemibold",
        });
        text.Add(new Label
        {
            Text = n.CreatedAt?.ToLocalTime().ToString("d MMM, HH:mm") ?? "",
            Style = (Style)Application.Current!.Resources["RowSubtitle"],
        });

        var dot = new BoxView
        {
            WidthRequest = 10,
            HeightRequest = 10,
            CornerRadius = 5,
            Color = Color.FromArgb("#6D3FE0"),
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 5, 0, 0),
            IsVisible = !n.IsRead,
        };

        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 10,
            Padding = new Thickness(14, 12),
        };
        grid.Add(dot, 0, 0);
        grid.Add(text, 1, 0);

        return new Border
        {
            Style = (Style)Application.Current!.Resources["GroupCard"],
            Content = grid,
        };
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void OnSwitchToggled(object sender, ToggledEventArgs e)
    {
        // TODO: persist the changed preference (sender is the Switch, e.Value is the new state)
    }
}