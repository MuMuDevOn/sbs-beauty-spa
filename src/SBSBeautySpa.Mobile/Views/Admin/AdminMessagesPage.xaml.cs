using SBSBeautySpa.Mobile.Services;

namespace SBSBeautySpa.Mobile.Views.Admin;

public partial class AdminMessagesPage : ContentPage
{
    private enum Filter { All, Waiting, Answered }

    private readonly AdminInboxService _inbox;
    private List<InboxMessage> _messages = new();
    private Filter _filter = Filter.Waiting;
    private string? _openId;   // the message whose reply box is open

    public AdminMessagesPage(AdminInboxService inbox)
    {
        InitializeComponent();
        _inbox = inbox;

        SizeChanged += (_, _) =>
            ContentColumn.WidthRequest = Math.Min(Math.Max(Width - 40, 0), 640);

        ShowChips();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void OnAllTapped(object sender, TappedEventArgs e) => Choose(Filter.All);
    private void OnWaitingTapped(object sender, TappedEventArgs e) => Choose(Filter.Waiting);
    private void OnAnsweredTapped(object sender, TappedEventArgs e) => Choose(Filter.Answered);

    private void Choose(Filter f)
    {
        _filter = f;
        _openId = null;
        ShowChips();
        Render();
    }

    private void ShowChips()
    {
        void Style(Border chip, Label label, bool on)
        {
            chip.BackgroundColor = on ? Colors.White : Color.FromArgb("#33FFFFFF");
            label.TextColor = on ? Color.FromArgb("#2B1B6B") : Colors.White;
        }

        Style(AllChip, AllLabel, _filter == Filter.All);
        Style(WaitingChip, WaitingLabel, _filter == Filter.Waiting);
        Style(AnsweredChip, AnsweredLabel, _filter == Filter.Answered);
    }

    private async Task LoadAsync()
    {
        Spinner.IsVisible = Spinner.IsRunning = true;
        ErrorLabel.IsVisible = false;

        try
        {
            _messages = await _inbox.ListAsync();
            Render();
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = "Couldn't load messages: " + ex.Message;
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            Spinner.IsVisible = Spinner.IsRunning = false;
        }
    }

    private void Render()
    {
        MessageList.Children.Clear();

        var shown = _messages
            .Where(m => _filter switch
            {
                Filter.Waiting => !m.IsAnswered,
                Filter.Answered => m.IsAnswered,
                _ => true,
            })
            .ToList();

        if (shown.Count == 0)
        {
            MessageList.Children.Add(new Label
            {
                Text = _filter == Filter.Waiting ? "No messages waiting for a reply." : "No messages here yet.",
                Style = (Style)Application.Current!.Resources["HintText"],
                Margin = new Thickness(0, 12, 0, 0),
            });
            return;
        }

        foreach (var m in shown)
            MessageList.Children.Add(BuildCard(m));
    }

    private View BuildCard(InboxMessage m)
    {
        var navy = Color.FromArgb("#2B1B6B");
        var muted = Color.FromArgb("#6E63A6");
        var purple = Color.FromArgb("#6D3FE0");

        var body = new VerticalStackLayout { Spacing = 8 };

        // Top line: name + date
        var top = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8,
        };
        top.Add(new Label
        {
            Text = m.Name,
            FontFamily = "OpenSansSemibold",
            FontSize = 16,
            TextColor = navy,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
        }, 0, 0);
        top.Add(new Label
        {
            Text = m.CreatedAt?.ToLocalTime().ToString("d MMM, HH:mm") ?? "",
            FontSize = 12,
            TextColor = muted,
            VerticalOptions = LayoutOptions.Center,
        }, 1, 0);
        body.Add(top);

        body.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(m.Subject) ? m.Email : $"{m.Subject} · {m.Email}",
            FontSize = 12,
            TextColor = muted,
        });

        // Status pill
        body.Add(new Border
        {
            HorizontalOptions = LayoutOptions.Start,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(10, 4),
            BackgroundColor = m.IsAnswered ? Color.FromArgb("#E3F6EA") : Color.FromArgb("#EFE9FF"),
            Content = new Label
            {
                Text = m.IsAnswered ? $"Answered by {m.RepliedByName}" : "Waiting for a reply",
                FontFamily = "OpenSansSemibold",
                FontSize = 12,
                TextColor = m.IsAnswered ? Color.FromArgb("#1D7A46") : purple,
            },
        });

        body.Add(new Label { Text = m.Message, FontSize = 14, TextColor = navy });

        if (m.IsAnswered)
        {
            var replyBox = new VerticalStackLayout { Spacing = 4 };
            replyBox.Add(new Label
            {
                Text = $"Reply from {m.RepliedByName}" + (m.RepliedAt is null ? "" : " · " + m.RepliedAt.Value.ToLocalTime().ToString("d MMM, HH:mm")),
                FontFamily = "OpenSansSemibold",
                FontSize = 12,
                TextColor = muted,
            });
            replyBox.Add(new Label { Text = m.Reply, FontSize = 14, TextColor = navy });

            body.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#F4F0FF"),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(12),
                Content = replyBox,
            });
        }
        else if (_openId == m.Id)
        {
            var editor = new Editor
            {
                Placeholder = "Type your reply...",
                HeightRequest = 120,
                FontSize = 14,
                TextColor = navy,
                PlaceholderColor = Color.FromArgb("#A59CC9"),
                AutoSize = EditorAutoSizeOption.Disabled,
            };
            body.Add(new Border
            {
                BackgroundColor = Color.FromArgb("#EFE9FF"),
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Padding = new Thickness(10, 4),
                Content = editor,
            });

            var send = new Button { Text = "Send reply", Style = (Style)Application.Current!.Resources["PrimaryButton"] };
            var cancel = new Button { Text = "Cancel", Style = (Style)Application.Current!.Resources["OutlineButton"] };

            send.Clicked += async (_, _) => await SendReplyAsync(m, editor.Text, send);
            cancel.Clicked += (_, _) => { _openId = null; Render(); };

            body.Add(new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 8,
                Children = { cancel, send },
            });
            Grid.SetColumn(send, 1);
        }
        else
        {
            var reply = new Button { Text = "Reply", Style = (Style)Application.Current!.Resources["PrimaryButton"] };
            reply.Clicked += (_, _) => { _openId = m.Id; Render(); };
            body.Add(reply);
        }

        return new Border
        {
            Style = (Style)Application.Current!.Resources["GroupCard"],
            Padding = new Thickness(16),
            Content = body,
        };
    }

    private async Task SendReplyAsync(InboxMessage m, string? text, Button send)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            await DisplayAlertAsync("Empty reply", "Please type a reply first.", "OK");
            return;
        }

        bool ok = await DisplayAlertAsync("Send reply",
            $"Send this reply to {m.Name}? They'll get it by email and in the app, and it will show your name.",
            "Send", "Cancel");
        if (!ok) return;

        send.IsEnabled = false;
        try
        {
            var adminName = await _inbox.ReplyAsync(m.Id, text.Trim());

            m.Reply = text.Trim();
            m.RepliedByName = adminName;
            m.RepliedAt = DateTime.UtcNow;
            m.Status = "handled";
            _openId = null;
            Render();
        }
        catch (Exception ex)
        {
            send.IsEnabled = true;
            await DisplayAlertAsync("Couldn't send the reply", ex.Message, "OK");
        }
    }
}
