using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public static partial class SettingsDialog
{
    /// <summary>
    /// The Settings window and the controls it holds while it is open. The folder and Claude sections are built first
    /// (Form), then the mods' extensions are searched for, then the rest is built (Build), and Save applies the choices.
    /// </summary>
    internal sealed partial class Form
    {
        private readonly AppSettings _settings;
        private readonly SolidColorBrush _muted = new(Color.FromRgb(142, 151, 166));
        private readonly TextBox _valheim;
        private readonly Button _browse;
        private readonly Button _auto;
        private readonly StackPanel _bep = null!;
        private readonly Func<IEnumerable<string>> _readBep = null!;
        private readonly StackPanel _worlds = null!;
        private readonly Func<IEnumerable<string>> _readWorlds = null!;
        private CheckBox _claude = null!;
        private TextBox _port = null!;
        private string _token = "";
        private TextBox _command = null!;
        private StackPanel _claudeDetails = null!;
        private CheckBox _scripts = null!;
        private TextBox _desktop = null!;
        private List<(CheckBox Box, ModExtensions.Extension Extension)> _extensionBoxes = [];
        private StackPanel _extensions = null!;
        private IReadOnlyList<Lang.Language> _languages = [];
        private ComboBox _language = null!;
        private CheckBox _driverGl = null!;
        private CheckBox _expandWorld = null!;
        private TextBlock _error = null!;

        /// <summary>Builds the window, the valheim folder choice, the BepInEx and world folder lists and Claude's section.</summary>
        /// <param name="settings">The settings the window changes when saved.</param>
        public Form(AppSettings settings)
        {
            _settings = settings;
            Dialog = new Window
            {
                Title = Lang.T("settings.title"),
                Width = 720,
                // As tall as what it holds (it scrolls past that).
                SizeToContent = SizeToContent.Height,
                MaxHeight = 760,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.FromRgb(24, 28, 34)),
            };
            _valheim = new TextBox { Text = settings.ValheimPath, PlaceholderText = Lang.T("settings.valheimAuto") }.Tip("settings.valheim");
            _browse = new Button { Content = Lang.T("common.browse") }.Tip("settings.browse");
            _browse.Click += async (_, _) => { if (await Pick(Lang.T("settings.pickValheim")) is string p) { _valheim.Text = p; } };
            _auto = new Button { Content = Lang.T("settings.automatic") }.Tip("settings.auto");
            _auto.Click += (_, _) => _valheim.Text = "";
            _bep = BuildFolders(settings.BepInExFolders, Lang.T("settings.pickBepInEx"), out _readBep);
            _worlds = BuildFolders(settings.WorldFolders, Lang.T("settings.pickWorlds"), out _readWorlds);
            BuildClaude();
        }

        /// <summary>The window, shown by Show once the rest is built.</summary>
        public Window Dialog { get; }

        /// <summary>Whether the user saved the choices (set by Save).</summary>
        public bool Saved { get; private set; }

        /// <summary>A section's heading: upper-case, muted, with space above.</summary>
        /// <param name="t">The heading's text.</param>
        private TextBlock H2(string t) => new() { Text = t.ToUpperInvariant(), FontSize = 12, Foreground = _muted, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 14, 0, 4) };

        /// <summary>A muted, wrapped explanation under a control.</summary>
        /// <param name="t">The explanation's text.</param>
        private TextBlock Hint(string t) => new() { Text = t, FontSize = 12, Foreground = _muted, TextWrapping = TextWrapping.Wrap };

        /// <summary>Asks for a folder: the test's picker when one is set, else the system's.</summary>
        /// <param name="title">The picker's title.</param>
        private async Task<string?> Pick(string title)
        {
            if (PickFolder is { } pick)
            {
                return await pick(title);
            }
            return await Pickers.Folder(Dialog.StorageProvider, title);
        }
    }
}
