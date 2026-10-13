using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// Settings: folders on this computer (the web start page's settings): the Valheim game folder, more
/// BepInEx folders to look for the plugin in, more world folders to list; Claude's connection; on Windows, what draws.
/// </summary>
public static class SettingsDialog
{
    /// <summary>A typed folder made absolute: trimmed, quotes dropped, a leading ~ turned into the home folder.</summary>
    private static string Expand(string path)
    {
        path = path.Trim().Trim('"');
        if (path.StartsWith('~'))
        {
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];
        }
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Checks and applies the folder choices, saving the settings; nothing is changed when there is a problem. A
    /// changed game folder starts <c>CheckGameLook</c>.
    /// </summary>
    /// <param name="settings">The settings to change and save.</param>
    /// <param name="valheim">The game folder typed; null or blank to find it automatically.</param>
    /// <param name="bepInEx">More BepInEx folders; blank entries are dropped, each must exist.</param>
    /// <param name="worlds">More world folders; blank entries are dropped, each must exist.</param>
    /// <returns>The problems, as one line, or null when saved.</returns>
    public static string? Apply(AppSettings settings, string? valheim, IEnumerable<string> bepInEx, IEnumerable<string> worlds)
    {
        var problems = new List<string>();
        string? game = string.IsNullOrWhiteSpace(valheim) ? null : Expand(valheim);
        if (game != null)
        {
            // A folder next to it (steamapps, valheim_Data…) is taken as the game folder it leads to.
            game = GameLook.GameFolder(game) ?? game;
            if (GameLook.BundlesDir(game) == null)
            {
                problems.Add("The Valheim folder must be the game's folder, the one with valheim_Data (in Steam: right-click Valheim, Manage › Browse local files).");
            }
        }
        List<string> Clean(IEnumerable<string> list, string what) => [.. list.Select(p => p.Trim()).Where(p => p.Length > 0).Select(Expand).Distinct().Where(p => { if (Directory.Exists(p)) { return true; } problems.Add($"{what} not found: {p}"); return false; })];
        var bep = Clean(bepInEx, "BepInEx folder");
        var wl = Clean(worlds, "World folder");
        if (problems.Count > 0)
        {
            return string.Join(" ", problems);
        }
        bool gameChanged = game != settings.ValheimPath;
        settings.ValheimPath = game;
        settings.BepInExFolders = bep;
        settings.WorldFolders = wl;
        settings.Save();
        if (gameChanged)
        {
            // Another game folder: its look (copied again when it is another version).
            CheckGameLook(settings);
        }
        return null;
    }

    /// <summary>Tests: what a changed game folder starts (reading the game's files otherwise).</summary>
    internal static Action<AppSettings> CheckGameLook { get; set; } = GameLook.Check;

    /// <summary>Whether the Drawing part is shown (Windows only; tests set it).</summary>
    internal static bool OnWindows { get; set; } = OperatingSystem.IsWindows();

    /// <summary>Tests: the folder picker (null: the system's).</summary>
    internal static Func<string, Task<string?>>? PickFolder { get; set; }

    /// <summary>Shows the Settings dialog over <paramref name="owner"/> and waits for it to close.</summary>
    /// <returns>True when the user saved (the folders, Claude's connection settings and what draws), false when cancelled or closed.</returns>
    public static async Task<bool> Show(Window owner, AppSettings settings)
    {
        bool saved = false;
        var dialog = new Window
        {
            Title = "Settings: folders on this computer",
            Width = 720,
            // As tall as what it holds (it scrolls past that).
            SizeToContent = SizeToContent.Height,
            MaxHeight = 760,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(24, 28, 34)),
        };
        var muted = new SolidColorBrush(Color.FromRgb(142, 151, 166));
        TextBlock H2(string t) => new() { Text = t.ToUpperInvariant(), FontSize = 12, Foreground = muted, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 14, 0, 4) };
        TextBlock Hint(string t) => new() { Text = t, FontSize = 12, Foreground = muted, TextWrapping = TextWrapping.Wrap };
        async Task<string?> Pick(string title)
        {
            if (PickFolder is { } pick)
            {
                return await pick(title);
            }
            var picked = await dialog.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions { Title = title });
            return picked.Count > 0 && picked[0].Path.IsFile ? picked[0].Path.LocalPath : null;
        }
        var valheim = new TextBox { Text = settings.ValheimPath, PlaceholderText = "automatic (Steam libraries)" }.Tip("settings.valheim");
        var browse = new Button { Content = "Browse…" }.Tip("settings.browse");
        browse.Click += async (_, _) => { if (await Pick("The Valheim game folder (with valheim_Data)") is string p) { valheim.Text = p; } };
        var auto = new Button { Content = "Automatic" }.Tip("settings.auto");
        auto.Click += (_, _) => valheim.Text = "";
        StackPanel List(List<string> initial, string pickTitle, out Func<IEnumerable<string>> read)
        {
            var rows = new StackPanel { Spacing = 4 };
            var boxes = new List<TextBox>();
            void Add(string text)
            {
                var box = new TextBox { Text = text }.Tip("settings.folder");
                var x = new Button { Content = "✕" }.Tip("settings.remove");
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 4 };
                var br = new Button { Content = "Browse…" }.Tip("settings.browse");
                br.Click += async (_, _) => { if (await Pick(pickTitle) is string p) { box.Text = p; } };
                Grid.SetColumn(br, 1);
                Grid.SetColumn(x, 2);
                row.Children.Add(box);
                row.Children.Add(br);
                row.Children.Add(x);
                x.Click += (_, _) => { rows.Children.Remove(row); boxes.Remove(box); };
                boxes.Add(box);
                rows.Children.Add(row);
            }
            foreach (string f in initial)
            {
                Add(f);
            }
            var add = new Button { Content = "Add a folder" }.Tip("settings.add");
            add.Click += (_, _) => Add("");
            read = () => boxes.Select(b => b.Text ?? "");
            return new StackPanel { Spacing = 4, Children = { rows, add } };
        }
        var bep = List(settings.BepInExFolders, "A BepInEx folder or a mod manager profile", out var readBep);
        var worlds = List(settings.WorldFolders, "A world folder or a folder of worlds", out var readWorlds);
        // Claude's connection.
        var claude = new CheckBox { Content = "Allow Claude to connect", IsChecked = settings.ClaudeConnect }.Classed("switch").Tip("settings.claude");
        var port = new TextBox { Name = "ClaudePort", Text = settings.ClaudePort.ToString(System.Globalization.CultureInfo.InvariantCulture), Width = 90 }.Tip("settings.claudePort");
        string token = string.IsNullOrEmpty(settings.ClaudeToken) ? ClaudeServer.NewToken() : settings.ClaudeToken;
        var command = new TextBox { Name = "ClaudeCommand", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, FontFamily = new FontFamily("monospace") }.Tip("settings.claudeCommand");
        void ShowCommand() => command.Text = ClaudeServer.ClaudeCodeCommand(int.TryParse(port.Text, out int p) ? p : settings.ClaudePort, token);
        ShowCommand();
        port.TextChanged += (_, _) => ShowCommand();
        var copy = new Button { Content = "Copy" }.Tip("settings.claudeCopy");
        copy.Click += async (_, _) => { if (TopLevel.GetTopLevel(dialog)?.Clipboard is { } cb) { await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(cb, command.Text ?? ""); } };
        var renew = new Button { Content = "New token" }.Tip("settings.claudeToken");
        renew.Click += (_, _) => { token = ClaudeServer.NewToken(); ShowCommand(); };
        var claudeDetails = new StackPanel { Spacing = 6, IsVisible = claude.IsChecked == true };
        claude.IsCheckedChanged += (_, _) => claudeDetails.IsVisible = claude.IsChecked == true;
        claudeDetails.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = "Port", VerticalAlignment = VerticalAlignment.Center }, port, renew } });
        claudeDetails.Children.Add(Hint("To connect Claude Code, run this once in a terminal (other MCP clients: the address, with the header):"));
        claudeDetails.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4, Children = { command, Col(copy, 1) } });
        // Claude Desktop starts a program: this one, relaying to the running editor (ClaudeRelay).
        var desktop = new TextBox { Name = "ClaudeDesktop", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, FontFamily = new FontFamily("monospace"), Text = ClaudeServer.DesktopConfig(Environment.ProcessPath ?? "ValheimWorldEditor") }.Tip("settings.claudeDesktop");
        var copyDesktop = new Button { Content = "Copy" }.Tip("settings.claudeCopy");
        copyDesktop.Click += async (_, _) => { if (TopLevel.GetTopLevel(dialog)?.Clipboard is { } cb) { await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(cb, desktop.Text ?? ""); } };
        claudeDetails.Children.Add(Hint("Claude Desktop: add this to its configuration (Settings > Developer > Edit Config, claude_desktop_config.json), then restart it. The editor must be running:"));
        claudeDetails.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4, Children = { desktop, Col(copyDesktop, 1) } });
        if (ClaudeServer.Status is string status)
        {
            claudeDetails.Children.Add(Hint(status));
        }
        // What draws (Windows): taken at the next start.
        var driverGl = new CheckBox { Content = "Draw with the graphics driver's OpenGL", IsChecked = settings.DriverOpenGl }.Classed("switch").Tip("settings.driverGl");
        var drawing = new StackPanel
        {
            Spacing = 6,
            IsVisible = OnWindows,
            Children =
            {
                H2("Drawing"),
                driverGl,
                Hint("Off (the default): the editor draws through Direct3D, like games do. On: through the graphics driver's OpenGL, as earlier versions did; try it if the 3D view or the map draws wrongly or slowly. Taken when the editor starts again."),
            },
        };
        var error = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap };
        var save = new Button { Content = "Save", IsDefault = true }.Tip("settings.save");
        save.Click += (_, _) =>
        {
            if (!int.TryParse(port.Text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int claudePort) || claudePort is < 1024 or > 65535)
            {
                error.Text = "The port for Claude must be a number from 1024 to 65535.";
                return;
            }
            // Set before Apply, which saves them with the folders; taken back when the folders are refused, so a
            // Cancel after that keeps nothing.
            var before = (settings.ClaudeConnect, settings.ClaudePort, settings.ClaudeToken, settings.DriverOpenGl);
            settings.ClaudeConnect = claude.IsChecked == true;
            settings.ClaudePort = claudePort;
            settings.ClaudeToken = token;
            settings.DriverOpenGl = driverGl.IsChecked == true;
            if (Apply(settings, valheim.Text, readBep(), readWorlds()) is string problem)
            {
                (settings.ClaudeConnect, settings.ClaudePort, settings.ClaudeToken, settings.DriverOpenGl) = before;
                error.Text = problem;
                return;
            }
            saved = true;
            dialog.Close();
        };
        var cancel = new Button { Content = "Cancel", IsCancel = true }.Tip("dialog.cancel");
        cancel.Click += (_, _) => dialog.Close();
        string detected = GameLook.FindValheim(null) is string d ? $"Found automatically: {Ui.Tilde(d)}" : "Not found automatically: choose the folder.";
        dialog.Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Spacing = 6,
                Children =
                {
                    H2("Valheim game folder"),
                    Hint(detected),
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 4, Children = { valheim, Col(browse, 1), Col(auto, 2) } },
                    Hint("The folder with valheim_Data. Used for the game's look, and to find BepInEx for \"My game\"."),
                    H2("BepInEx folders"),
                    bep,
                    Hint("Where else to look for the plugin for \"My game\": a BepInEx folder, a mod manager profile, or a folder of profiles. The Valheim folder and the default r2modman / Thunderstore Mod Manager profiles are always searched."),
                    H2("World folders"),
                    worlds,
                    Hint($"Always listed under \"A saved world\": a world folder, or a folder of worlds (a server's <savedir>/worlds_local, a backup folder…). Valheim's usual folders are always searched: {string.Join(", ", Places.WorldRoots().Where(Directory.Exists).Select(Ui.Tilde))}"),
                    H2("Claude"),
                    claude,
                    Hint("Lets Claude (Claude Code, Claude Desktop) look at the world or the Workshop open in the editor and change it from a prompt: shape the ground, place objects, build. Only from this computer, with the token below. What it changes stays pending, like your own changes: you save, apply live or save the blueprint."),
                    claudeDetails,
                    drawing,
                    error,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Children = { cancel, save } },
                },
            },
        };
        await dialog.ShowDialog(owner);
        return saved;
    }

    /// <summary>Puts a control in a grid column and returns it, for building rows inline.</summary>
    private static Control Col(Control c, int col)
    {
        Grid.SetColumn(c, col);
        return c;
    }
}
