using Avalonia;
using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class StartPage
{
    /// <summary>A world to open: how to open it (run by the window, which shows the progress) and what to say meanwhile.</summary>
    public event Action<Func<Task<WorldSession>>, string>? OpenRequested;
    /// <summary>The Workshop: a blueprint file to open in it, or null for a blank plot.</summary>
    public event Action<string?>? WorkshopRequested;
    /// <summary>Asks the window for a folder (Browse…), with the picker's title; null when cancelled.</summary>
    internal Func<string, Task<string?>> PickFolder { get; set; } = _ => Task.FromResult<string?>(null);
    /// <summary>Asks the window for a file (Browse…), with the picker's title; null when cancelled.</summary>
    internal Func<string, Task<string?>> PickFile { get; set; } = _ => Task.FromResult<string?>(null);
    /// <summary>Asks the window a yes/no question (forgetting a server).</summary>
    internal Func<string, Task<bool>> Confirm { get; set; } = _ => Task.FromResult(true);
    /// <summary>Tests: the SSH tunnel (the real one otherwise).</summary>
    internal Func<Tunnel.Request, Task<Tunnel.Result>> StartTunnel { get; set; } = Tunnel.Start;
    /// <summary>Tests: the worlds found on this computer (the real ones otherwise).</summary>
    internal Func<AppSettings, List<Worlds.Info>> FindWorlds { get; set; } = Worlds.Find;
    /// <summary>The Settings button was clicked.</summary>
    public event Action? SettingsRequested;
    /// <summary>The project's page, which Documentation opens.</summary>
    public const string DocsUrl = "https://github.com/Shuiei/valheim-world-editor#readme";
    /// <summary>Documentation: the project's page, in the web browser.</summary>
    internal Button DocsLink { get; } = new Button { Content = Icons.With("help", Lang.T("start.label.docs")), FontSize = 11, Padding = new Thickness(6, 2) }.Classed("ghost");
    /// <summary>Opens a web page or file with the system (the window's launcher); does nothing until set.</summary>
    internal Func<Uri, Task<bool>> OpenUrl { get; set; } = _ => Task.FromResult(false);
    /// <summary>Open log: ValheimWorldEditor.log in the system's text viewer, to attach to a bug report.</summary>
    internal Button LogLink { get; } = new Button { Content = Lang.T("start.label.log"), FontSize = 11, Padding = new Thickness(6, 2) }.Classed("ghost");

    /// <summary>The card about reading the game's look from Valheim (or finding its folder), above every page.</summary>
    internal GameLookBanner LookBanner { get; }
    /// <summary>The side menu's buttons by page key ("home", "game", "server", "offline", "workshop", "new").</summary>
    internal Dictionary<string, Button> ModeButtons { get; } = [];
}
