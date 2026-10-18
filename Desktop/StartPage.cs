using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The start page, like the web editor's (start.html): how to edit (your game live, a dedicated server
/// live, or a saved world offline), the worlds found on this computer, saved servers, and the settings.
/// Choosing one opens the world (OpenRequested), then the map.
/// </summary>
public sealed partial class StartPage
{
    /// <summary>The page itself: the side menu and the chosen page.</summary>
    public Control View { get; }
    /// <summary>The settings (last mode, recent worlds, folders), saved when a world is opened.</summary>
    private readonly AppSettings _settings;

    /// <summary>
    /// Whether a connection is being made (Connect, ConnectUrl): another click meanwhile does nothing, so no second
    /// tunnel or world is opened.
    /// </summary>
    private bool _connecting;

    /// <summary>Shorthands for the app's line and muted text brushes.</summary>
    private static readonly IBrush Line = Ui.Line, Muted = Ui.Muted;

    /// <summary>A path under the home folder as ~/… (see Ui.Tilde).</summary>
    internal static string Tilde(string path) => Ui.Tilde(path);

    /// <summary>An error line: takes no room while it says nothing.</summary>
    private static TextBlock Err()
    {
        var t = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap, FontSize = 12, IsVisible = false };
        t.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBlock.TextProperty)
            {
                t.IsVisible = !string.IsNullOrEmpty(t.Text);
            }
        };
        return t;
    }
    /// <summary>A small grey wrapped explanation.</summary>
    private static TextBlock Hint(string t) => new() { Text = t, Foreground = Muted, TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    /// <summary>A floating card (Ui.Card).</summary>
    private static Border Card(Control c) => Ui.Card(c);
    /// <summary>A section heading in grey capitals.</summary>
    private static TextBlock H2(string t) => new() { Text = t.ToUpperInvariant(), FontSize = 12, Foreground = Muted, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 18, 0, 6) };

    /// <summary>What is turned off while the game's files are read: every page's actions, and Settings (which can change the game folder).</summary>
    private readonly Control[] _busyOff;

    /// <summary>The page shown (a <c>ModeButtons</c> key).</summary>
    public string Mode { get; private set; } = "";

    // ---- My game: is Valheim running with the plugin?
    /// <summary>The game's state as last shown (state, world, players, where), so an unchanged poll redraws nothing.</summary>
    private string _gameKey = "";
    /// <summary>The check of the game running or last run (see <see cref="PollGame"/>).</summary>
    private Task? _poll;
    /// <summary>Asked for during the running check: one more runs after it.</summary>
    private bool _pollAgain;

    // ---- Opening.

    // ---- Saved worlds.

    // ---- Dedicated servers.
}
