using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The game's look (terrain textures, map textures, models) is read from the player's own Valheim (GameLook): the start
/// page's card that follows it. While its files are first read (indexed), a word; when Valheim is not found, how to
/// find its folder and a field to choose it; when it failed, Try again; once it is done, a word that the next area
/// opened has it. Hidden when there is nothing to say.
/// </summary>
public sealed partial class GameLookBanner
{
    /// <summary>
    /// Builds the card; it follows the game look while it is on screen.
    /// </summary>
    public GameLookBanner(AppSettings settings)
    {
        _settings = settings;
        Grid.SetColumn(Browse, 1);
        Grid.SetColumn(Use, 2);
        PathRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 6, Children = { PathBox, Browse, Use } };
        _card = Ui.Card(new StackPanel { Spacing = 6, Children = { Title, Message, Steps, Bar, LastLine, PathRow, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { Retry, ShowLog } }, Error, Hint } });
        _card.Margin = new Thickness(0, 10, 0, 0);
        _card.IsVisible = false;
        PathBox.Tip("look.path");
        Browse.Tip("look.browse");
        Use.Tip("look.use");
        Retry.Tip("look.retry");
        ShowLog.Tip("look.log");
        ShowLog.Click += async (_, _) => await OpenLog();
        Browse.Click += async (_, _) => { if (await PickFolder(Lang.T("look.pickFolder")) is string p) { PathBox.Text = p; } };
        Use.Click += (_, _) => Start(PathBox.Text);
        Retry.Click += (_, _) => Start(Read().Valheim ?? _settings.ValheimPath);
        _timer.Tick += (_, _) => Refresh();
        _card.AttachedToVisualTree += (_, _) => { Refresh(); _timer.Start(); };
        _card.DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    /// <summary>
    /// Starts reading the game's files from a Valheim folder (or says to choose one when it is empty) and shows the new
    /// state.
    /// </summary>
    internal void Start(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            Error.Text = Lang.T("look.error.noFolder");
            Error.IsVisible = true;
            return;
        }
        Error.Text = StartExport(folder, _settings) ?? "";
        Refresh();
    }

    /// <summary>
    /// Shows the game look's state (rebuilt when it changes; the progress every time).
    /// </summary>
    internal void Refresh()
    {
        var s = Read();
        bool changed = s.State != Shown;
        Shown = s.State;
        Bar.IsVisible = LastLine.IsVisible = PathRow.IsVisible = Retry.IsVisible = ShowLog.IsVisible = Steps.IsVisible = false;
        switch (s.State)
        {
            case "running" when s.Reading:
                ShowReading(s);
                break;
            case "missing":
                ShowMissing(s, changed);
                break;
            case "failed":
                ShowFailed(s);
                break;
            default:
                ShowReady();
                break;
        }
        Hint.IsVisible = !string.IsNullOrEmpty(Hint.Text);
        Error.IsVisible = Error.Text?.Length > 0;
        // Running without reading (the kept index and caches checked, at each start): nothing to say.
        _card.IsVisible = s.State == "running" ? s.Reading : s.State != "ready" || _sawRunning;
        bool busy = s.State == "running" && s.Reading;
        if (busy != Busy)
        {
            Busy = busy;
            BusyChanged?.Invoke(busy);
        }
    }
}
