using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The game's look (terrain textures, map textures, models) is read from the player's own Valheim (GameLook): the start
/// page's card that follows it. While its files are first read (indexed), a word; when Valheim is not found, how to
/// find its folder and a field to choose it; when it failed, Try again; once it is done, a word that the next area
/// opened has it. Hidden when there is nothing to say.
/// </summary>
public sealed class GameLookBanner
{
    /// <summary>
    /// The card to put on the start page (hidden when there is nothing to say).
    /// </summary>
    public Control View => _card;
    private readonly Border _card;
    private readonly AppSettings _settings;
    /// <summary>
    /// Reads the game look's state again every half second while the card is on screen (often enough for the bar
    /// to move smoothly; a read is only a snapshot under a lock).
    /// </summary>
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(0.5) };
    /// <summary>
    /// The state last shown, so the path box is filled only when it changes.
    /// </summary>
    private string _state = "";
    /// <summary>
    /// Whether the files were read while the card was up: then "ready" is said too, else the card stays hidden.
    /// </summary>
    private bool _sawRunning;

    /// <summary>
    /// The card's heading.
    /// </summary>
    internal TextBlock Title { get; } = new() { FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// What is going on, or what went wrong.
    /// </summary>
    internal TextBlock Message { get; } = new() { TextWrapping = TextWrapping.Wrap };
    /// <summary>
    /// How far the reading is (0..1), while it runs.
    /// </summary>
    internal ProgressBar Bar { get; } = new() { Minimum = 0, Maximum = 1, Height = 6, MinHeight = 6 };
    /// <summary>
    /// The reading's last detail line, while it runs.
    /// </summary>
    internal TextBlock LastLine { get; } = Ui.Hint("");
    /// <summary>
    /// The Valheim folder to use, when it was not found.
    /// </summary>
    internal TextBox PathBox { get; } = new() { PlaceholderText = "…/steamapps/common/Valheim", FontSize = 13 };
    /// <summary>
    /// Picks the folder for PathBox.
    /// </summary>
    internal Button Browse { get; } = new() { Content = "Browse…" };
    /// <summary>
    /// Starts reading from the folder in PathBox.
    /// </summary>
    internal Button Use { get; } = new Button { Content = "Use" }.Classed("primary");
    /// <summary>
    /// Starts reading again after a failure (from the folder last tried, else the settings').
    /// </summary>
    internal Button Retry { get; } = new Button { Content = "Try again" }.Classed("primary");
    /// <summary>
    /// Opens the editor's log after a failure (OpenLog).
    /// </summary>
    internal Button ShowLog { get; } = new() { Content = "Open log" };
    /// <summary>
    /// Opens the editor's log; set by the start page.
    /// </summary>
    internal Func<Task<bool>> OpenLog { get; set; } = () => Task.FromResult(false);
    /// <summary>
    /// Why the chosen folder could not be used (hidden when empty).
    /// </summary>
    internal TextBlock Error { get; } = new() { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    /// <summary>
    /// A note under the card's state (hidden when empty).
    /// </summary>
    internal TextBlock Hint { get; } = Ui.Hint("");
    /// <summary>
    /// How to find the game folder (SetupSteps), when Valheim was not found.
    /// </summary>
    internal TextBlock Steps { get; } = new() { Text = SetupSteps(OperatingSystem.IsWindows()), TextWrapping = TextWrapping.Wrap, FontSize = 12.5, LineHeight = 19 };
    /// <summary>
    /// PathBox with Browse and Use.
    /// </summary>
    internal Grid PathRow { get; }

    /// <summary>
    /// The game look's state (tests set their own).
    /// </summary>
    internal Func<GameLook.Snapshot> Read { get; set; } = GameLook.Now;
    /// <summary>
    /// Keeps a Valheim folder in the settings and starts reading it: the problem, or null (tests set their own).
    /// </summary>
    internal Func<string, AppSettings, string?> StartExport { get; set; } = GameLook.Start;
    /// <summary>
    /// The folder picker, given its title: the folder, or null when cancelled (set by the start page; tests set their
    /// own).
    /// </summary>
    internal Func<string, Task<string?>> PickFolder { get; set; } = _ => Task.FromResult<string?>(null);

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
        Browse.Click += async (_, _) => { if (await PickFolder("Choose the Valheim game folder (with valheim_Data)") is string p) { PathBox.Text = p; } };
        Use.Click += (_, _) => Start(PathBox.Text);
        Retry.Click += (_, _) => Start(Read().Valheim ?? _settings.ValheimPath);
        _timer.Tick += (_, _) => Refresh();
        _card.AttachedToVisualTree += (_, _) => { Refresh(); _timer.Start(); };
        _card.DetachedFromVisualTree += (_, _) => _timer.Stop();
    }

    /// <summary>
    /// How to find the game folder, for Windows or for Linux (numbered lines).
    /// </summary>
    internal static string SetupSteps(bool windows) => string.Join("\n", new[]
    {
        "1. Install Valheim with Steam on this computer (owning it is enough: the editor needs its files, not the game running).",
        "2. In Steam, right-click Valheim in your Library, then Manage › Browse local files. The folder that opens is the one to choose: it holds valheim_Data.",
        windows
            ? "3. Usually: C:\\Program Files (x86)\\Steam\\steamapps\\common\\Valheim, or <your Steam library>\\steamapps\\common\\Valheim on another drive."
            : "3. Usually: ~/.local/share/Steam/steamapps/common/Valheim (Flatpak Steam: ~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Valheim), or <your Steam library>/steamapps/common/Valheim.",
        "4. Paste it below or use Browse…, then Use. A folder near it works too (steamapps, common, valheim_Data).",
    });

    /// <summary>
    /// Starts reading the game's files from a Valheim folder (or says to choose one when it is empty) and shows the new
    /// state.
    /// </summary>
    internal void Start(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            Error.Text = "Choose the folder Steam installed Valheim into (the one with valheim_Data).";
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
        bool changed = s.State != _state;
        _state = s.State;
        Bar.IsVisible = LastLine.IsVisible = PathRow.IsVisible = Retry.IsVisible = ShowLog.IsVisible = Steps.IsVisible = false;
        switch (s.State)
        {
            case "running":
                _sawRunning = true;
                Title.Text = "Preparing the game's look";
                Message.Text = s.Message ?? "Reading the game's files (a few seconds, only after a game update).";
                Bar.IsVisible = LastLine.IsVisible = true;
                Bar.Value = s.Progress ?? 0;
                LastLine.Text = s.LastLine ?? "";
                Hint.Text = "You can already pick a world and edit: areas opened once this is done have the game's look.";
                break;
            case "missing":
                Title.Text = "Get the game's look";
                Message.Text = s.Message ?? "Valheim was not found on this computer.";
                PathRow.IsVisible = Steps.IsVisible = true;
                if (changed && string.IsNullOrEmpty(PathBox.Text))
                {
                    PathBox.Text = s.Valheim ?? "";
                }
                Hint.Text = "The editor only reads the game's files (nothing is copied or changed). Without them it works too, with plain colours and boxes instead of the game's textures and models. The folder can be changed later in Settings.";
                break;
            case "failed":
                Title.Text = "The game's look could not be read";
                Message.Text = s.Message ?? "";
                Retry.IsVisible = ShowLog.IsVisible = true;
                Hint.Text = "The editor works without it, with plain colours and no models. To report the problem, attach the editor's log (Open log) to an issue on GitHub.";
                break;
            default:
                Title.Text = "The game's look is ready.";
                Message.Text = "The next world or area you open has the game's textures and models.";
                Hint.Text = "";
                break;
        }
        Hint.IsVisible = Hint.Text.Length > 0;
        Error.IsVisible = Error.Text?.Length > 0;
        _card.IsVisible = s.State != "ready" || _sawRunning;
    }
}
