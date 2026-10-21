using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The start page's card controls, their texts and the setup steps for finding the Valheim folder.
/// </summary>
public sealed partial class GameLookBanner
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
    /// The state last shown ("running" while the game's files are read, "missing", "failed"...), so the path box is
    /// filled only when it changes; the test driver reports it.
    /// </summary>
    internal string Shown { get; private set; } = "";
    /// <summary>
    /// Whether the files were read while the card was up: then "ready" is said too, else the card stays hidden.
    /// </summary>
    private bool _sawRunning;

    /// <summary>
    /// Whether the game's files are being read: the start page turns its actions off meanwhile.
    /// </summary>
    internal bool Busy { get; private set; }
    /// <summary>
    /// Busy changed (raised by Refresh).
    /// </summary>
    internal event Action<bool>? BusyChanged;

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
    internal TextBox PathBox { get; } = new() { PlaceholderText = Lang.T("look.hint.path"), FontSize = 13 };
    /// <summary>
    /// Picks the folder for PathBox.
    /// </summary>
    internal Button Browse { get; } = new() { Content = Lang.T("common.browse") };
    /// <summary>
    /// Starts reading from the folder in PathBox.
    /// </summary>
    internal Button Use { get; } = new Button { Content = Lang.T("look.label.use") }.Classed("primary");
    /// <summary>
    /// Starts reading again after a failure (from the folder last tried, else the settings').
    /// </summary>
    internal Button Retry { get; } = new Button { Content = Lang.T("look.label.retry") }.Classed("primary");
    /// <summary>
    /// Opens the editor's log after a failure (OpenLog).
    /// </summary>
    internal Button ShowLog { get; } = new() { Content = Lang.T("start.label.log") };
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
    internal TextBlock Steps { get; } = new() { Text = SetupSteps(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS()), TextWrapping = TextWrapping.Wrap, FontSize = 12.5, LineHeight = 19 };
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
    /// How to find the game folder, for Windows, a Mac or Linux (numbered lines).
    /// </summary>
    internal static string SetupSteps(bool windows, bool mac = false) => string.Join("\n", new[]
    {
        Lang.T("look.step1"),
        mac ? Lang.T("look.step2Mac") : Lang.T("look.step2"),
        windows ? Lang.T("look.step3Windows")
            : mac ? Lang.T("look.step3Mac")
            : Lang.T("look.step3Linux"),
        Lang.T("look.step4"),
    });
}
