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
                problems.Add(Lang.T("settings.notValheim"));
            }
        }
        List<string> Clean(IEnumerable<string> list, string notFound) => [.. list.Select(p => p.Trim()).Where(p => p.Length > 0).Select(Expand).Distinct().Where(p => { if (Directory.Exists(p)) { return true; } problems.Add(Lang.T(notFound, p)); return false; })];
        var bep = Clean(bepInEx, "settings.bepInExNotFound");
        var wl = Clean(worlds, "settings.worldNotFound");
        if (problems.Count > 0)
        {
            return string.Join(" ", problems);
        }
        bool gameChanged = game != settings.ValheimPath;
        var before = (settings.ValheimPath, settings.BepInExFolders, settings.WorldFolders);
        settings.ValheimPath = game;
        settings.BepInExFolders = bep;
        settings.WorldFolders = wl;
        if (settings.Save() is string notSaved)
        {
            // Not saved, not changed: they would look kept until the next start.
            (settings.ValheimPath, settings.BepInExFolders, settings.WorldFolders) = before;
            return Lang.T("settings.notSaved", notSaved);
        }
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
            Title = Lang.T("settings.title"),
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
            return await Pickers.Folder(dialog.StorageProvider, title);
        }
        var valheim = new TextBox { Text = settings.ValheimPath, PlaceholderText = Lang.T("settings.valheimAuto") }.Tip("settings.valheim");
        var browse = new Button { Content = Lang.T("common.browse") }.Tip("settings.browse");
        browse.Click += async (_, _) => { if (await Pick(Lang.T("settings.pickValheim")) is string p) { valheim.Text = p; } };
        var auto = new Button { Content = Lang.T("settings.automatic") }.Tip("settings.auto");
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
                var br = new Button { Content = Lang.T("common.browse") }.Tip("settings.browse");
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
            var add = new Button { Content = Lang.T("settings.addFolder") }.Tip("settings.add");
            add.Click += (_, _) => Add("");
            read = () => boxes.Select(b => b.Text ?? "");
            return new StackPanel { Spacing = 4, Children = { rows, add } };
        }
        var bep = List(settings.BepInExFolders, Lang.T("settings.pickBepInEx"), out var readBep);
        var worlds = List(settings.WorldFolders, Lang.T("settings.pickWorlds"), out var readWorlds);
        // Claude's connection.
        var claude = new CheckBox { Content = Lang.T("settings.claudeAllow"), IsChecked = settings.ClaudeConnect }.Classed("switch").Tip("settings.claude");
        var port = new TextBox { Name = "ClaudePort", Text = settings.ClaudePort.ToString(System.Globalization.CultureInfo.InvariantCulture), Width = 90 }.Tip("settings.claudePort");
        string token = string.IsNullOrEmpty(settings.ClaudeToken) ? ClaudeServer.NewToken() : settings.ClaudeToken;
        var command = new TextBox { Name = "ClaudeCommand", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, FontFamily = new FontFamily("monospace") }.Tip("settings.claudeCommand");
        void ShowCommand() => command.Text = ClaudeServer.ClaudeCodeCommand(int.TryParse(port.Text, out int p) ? p : settings.ClaudePort, token);
        ShowCommand();
        port.TextChanged += (_, _) => ShowCommand();
        var copy = new Button { Content = Lang.T("common.copy") }.Tip("settings.claudeCopy");
        copy.Click += async (_, _) => { if (TopLevel.GetTopLevel(dialog)?.Clipboard is { } cb) { await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(cb, command.Text ?? ""); } };
        var renew = new Button { Content = Lang.T("settings.newToken") }.Tip("settings.claudeToken");
        renew.Click += (_, _) => { token = ClaudeServer.NewToken(); ShowCommand(); };
        var claudeDetails = new StackPanel { Spacing = 6, IsVisible = claude.IsChecked == true };
        claude.IsCheckedChanged += (_, _) => claudeDetails.IsVisible = claude.IsChecked == true;
        // Scripts have the editor's rights on this computer: the user's leave, once.
        var scripts = new CheckBox { Content = Lang.T("settings.claudeScripts"), IsChecked = settings.ClaudeScripts }.Classed("switch").Tip("settings.claudeScripts");
        claudeDetails.Children.Add(scripts);
        claudeDetails.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { new TextBlock { Text = Lang.T("settings.port"), VerticalAlignment = VerticalAlignment.Center }, port, renew } });
        claudeDetails.Children.Add(Hint(Lang.T("settings.claudeCodeHint")));
        claudeDetails.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4, Children = { command, Ui.Col(copy, 1) } });
        // Claude Desktop starts a program: this one, relaying to the running editor (ClaudeRelay).
        var desktop = new TextBox { Name = "ClaudeDesktop", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontSize = 12, FontFamily = new FontFamily("monospace"), Text = ClaudeServer.DesktopConfig(Environment.ProcessPath ?? "ValheimWorldEditor") }.Tip("settings.claudeDesktop");
        var copyDesktop = new Button { Content = Lang.T("common.copy") }.Tip("settings.claudeCopy");
        copyDesktop.Click += async (_, _) => { if (TopLevel.GetTopLevel(dialog)?.Clipboard is { } cb) { await Avalonia.Input.Platform.ClipboardExtensions.SetTextAsync(cb, desktop.Text ?? ""); } };
        claudeDetails.Children.Add(Hint(Lang.T("settings.claudeDesktopHint")));
        claudeDetails.Children.Add(new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4, Children = { desktop, Ui.Col(copyDesktop, 1) } });
        if (ClaudeServer.Status is string status)
        {
            claudeDetails.Children.Add(Hint(status));
        }
        // The mods' editor extensions: found again now (a mod installed since), each allowed or not. Away from the
        // window's thread: every extension file is read and hashed.
        await Task.Run(() =>
        {
            ModManifests.Refresh(settings);
            ModExtensions.Refresh(settings);
        });
        var extensionBoxes = new List<(CheckBox Box, ModExtensions.Extension Extension)>();
        var extensions = new StackPanel { Spacing = 4 };
        foreach (var x in ModExtensions.Offered)
        {
            var box = new CheckBox { Content = $"{x.Mod} · {Path.GetFileName(x.Dll)}", IsChecked = x.State != ModExtensions.Status.Off }.Classed("switch").Tip("settings.extension");
            ToolTip.SetTip(box, $"{Ui.Tilde(x.Dll)}\nSHA-256 {x.Sha256}");
            extensionBoxes.Add((box, x));
            extensions.Children.Add(box);
            string? state = x.State switch
            {
                ModExtensions.Status.Running => Lang.T("settings.extensionRunning"),
                ModExtensions.Status.Failed => Lang.T("settings.extensionFailed", x.Error),
                _ => x.Error != null ? Lang.T("settings.extensionOff", x.Error) : null,
            };
            if (state != null)
            {
                extensions.Children.Add(new TextBlock { Text = state, FontSize = 11, Foreground = muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(28, 0, 0, 4) });
            }
        }
        if (extensionBoxes.Count == 0)
        {
            extensions.Children.Add(Hint(Lang.T("settings.noExtension")));
        }
        // The language: taken at the next start (the windows are built in it).
        var languages = Lang.Available();
        var language = new ComboBox
        {
            Name = "Language",
            MinWidth = 220,
            ItemsSource = languages.Select(l => l.Name).Prepend(Lang.T("settings.languageSystem")).ToList(),
            SelectedIndex = settings.Language is string chosen && languages.ToList().FindIndex(l => l.Code == chosen) is int i and >= 0 ? i + 1 : 0,
        }.Tip("settings.language");
        // What draws (Windows): taken at the next start.
        var driverGl = new CheckBox { Content = Lang.T("settings.driverGl"), IsChecked = settings.DriverOpenGl }.Classed("switch").Tip("settings.driverGl");
        var drawing = new StackPanel
        {
            Spacing = 6,
            IsVisible = OnWindows,
            Children =
            {
                H2(Lang.T("settings.drawing")),
                driverGl,
                Hint(Lang.T("settings.drawingHint")),
            },
        };
        var error = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap };
        var save = new Button { Content = Lang.T("common.save"), IsDefault = true }.Tip("settings.save");
        save.Click += (_, _) =>
        {
            if (!int.TryParse(port.Text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int claudePort) || claudePort is < 1024 or > 65535)
            {
                error.Text = Lang.T("settings.badPort");
                return;
            }
            // Set before Apply, which saves them with the folders; taken back when the folders are refused, so a
            // Cancel after that keeps nothing.
            var before = (settings.ClaudeConnect, settings.ClaudeScripts, settings.ClaudePort, settings.ClaudeToken, settings.DriverOpenGl, settings.Language);
            var allowedBefore = new Dictionary<string, string>(settings.Extensions);
            foreach (var (box, x) in extensionBoxes)
            {
                ModExtensions.Allow(settings, x, box.IsChecked == true);
            }
            settings.ClaudeConnect = claude.IsChecked == true;
            settings.ClaudeScripts = scripts.IsChecked == true;
            settings.ClaudePort = claudePort;
            settings.ClaudeToken = token;
            settings.DriverOpenGl = driverGl.IsChecked == true;
            settings.Language = language.SelectedIndex > 0 ? languages[language.SelectedIndex - 1].Code : null;
            if (Apply(settings, valheim.Text, readBep(), readWorlds()) is string problem)
            {
                (settings.ClaudeConnect, settings.ClaudeScripts, settings.ClaudePort, settings.ClaudeToken, settings.DriverOpenGl, settings.Language) = before;
                settings.Extensions = allowedBefore;
                error.Text = problem;
                return;
            }
            // The extensions turned on run now, those turned off stop.
            ModExtensions.Refresh(settings);
            saved = true;
            dialog.Close();
        };
        var cancel = new Button { Content = Lang.T("common.cancel"), IsCancel = true }.Tip("dialog.cancel");
        cancel.Click += (_, _) => dialog.Close();
        string detected = GameLook.FindValheim(null) is string d ? Lang.T("settings.valheimFound", Ui.Tilde(d)) : Lang.T("settings.valheimNotFound");
        dialog.Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Spacing = 6,
                Children =
                {
                    H2(Lang.T("settings.language")),
                    language,
                    Hint(Lang.T("settings.languageHint", Ui.Tilde(Lang.Folder))),
                    H2(Lang.T("settings.valheimFolder")),
                    Hint(detected),
                    new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 4, Children = { valheim, Ui.Col(browse, 1), Ui.Col(auto, 2) } },
                    Hint(Lang.T("settings.valheimHint")),
                    H2(Lang.T("settings.bepInExFolders")),
                    bep,
                    Hint(Lang.T("settings.bepInExHint")),
                    H2(Lang.T("settings.worldFolders")),
                    worlds,
                    Hint(Lang.T("settings.worldsHint", string.Join(", ", Places.WorldRoots().Where(Directory.Exists).Select(Ui.Tilde)))),
                    H2(Lang.T("settings.claude")),
                    claude,
                    Hint(Lang.T("settings.claudeHint")),
                    claudeDetails,
                    H2(Lang.T("settings.extensions")),
                    Hint(Lang.T("settings.extensionsHint")),
                    extensions,
                    drawing,
                    error,
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Children = { cancel, save } },
                },
            },
        };
        await dialog.ShowDialog(owner);
        return saved;
    }
}
