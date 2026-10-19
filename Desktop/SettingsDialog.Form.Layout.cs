using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public static partial class SettingsDialog
{
    internal sealed partial class Form
    {
        /// <summary>
        /// Builds the rest of the window once the extensions are found: their list, the language and what draws (both
        /// taken at the next start), the history's length, the error line, Save and Cancel, and the content laid out in
        /// order.
        /// </summary>
        public void Build()
        {
            BuildExtensions();
            // The language: taken at the next start (the windows are built in it).
            _languages = Lang.Available();
            _language = new ComboBox
            {
                Name = "Language",
                MinWidth = 220,
                ItemsSource = _languages.Select(l => l.Name).Prepend(Lang.T("settings.languageSystem")).ToList(),
                SelectedIndex = _settings.Language is string chosen && _languages.ToList().FindIndex(l => l.Code == chosen) is int i and >= 0 ? i + 1 : 0,
            }.Tip("settings.language");
            // Expand World: the mods' world generation, for the worlds opened from now on.
            _expandWorld = new CheckBox { Content = Lang.T("settings.expandWorldSwitch"), IsChecked = _settings.ExpandWorld }.Classed("switch").Tip("settings.expandWorld");
            _expandWorldFolder = new TextBox { Name = "ExpandWorldFolder", Text = _settings.ExpandWorldFolder ?? "", PlaceholderText = Lang.T("settings.expandWorldFolderAuto") }.Tip("settings.expandWorldFolder");
            _expandWorldBrowse = new Button { Content = Lang.T("common.browse") }.Tip("settings.expandWorldBrowse");
            _expandWorldBrowse.Click += async (_, _) => { if (await Pick(Lang.T("settings.pickExpandWorld")) is string p) { _expandWorldFolder.Text = p; } };
            // What draws (Windows): taken at the next start.
            _driverGl = new CheckBox { Content = Lang.T("settings.driverGl"), IsChecked = _settings.DriverOpenGl }.Classed("switch").Tip("settings.driverGl");
            var drawing = new StackPanel
            {
                Spacing = 6,
                IsVisible = OnWindows,
                Children =
                {
                    H2(Lang.T("settings.drawing")),
                    _driverGl,
                    Hint(Lang.T("settings.drawingHint")),
                },
            };
            _error = new TextBlock { Foreground = new SolidColorBrush(Color.FromRgb(224, 96, 75)), TextWrapping = TextWrapping.Wrap };
            var save = new Button { Content = Lang.T("common.save"), IsDefault = true }.Tip("settings.save");
            save.Click += (_, _) => Save();
            var cancel = new Button { Content = Lang.T("common.cancel"), IsCancel = true }.Tip("dialog.cancel");
            cancel.Click += (_, _) => Dialog.Close();
            string detected = GameLook.FindValheim(null) is string d ? Lang.T("settings.valheimFound", Ui.Tilde(d)) : Lang.T("settings.valheimNotFound");
            Dialog.Content = new ScrollViewer
            {
                Content = new StackPanel
                {
                    Margin = new Thickness(18),
                    Spacing = 6,
                    Children =
                    {
                        H2(Lang.T("settings.language")),
                        _language,
                        Hint(Lang.T("settings.languageHint", Ui.Tilde(Lang.Folder))),
                        H2(Lang.T("settings.valheimFolder")),
                        Hint(detected),
                        new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 4, Children = { _valheim, Ui.Col(_browse, 1), Ui.Col(_auto, 2) } },
                        Hint(Lang.T("settings.valheimHint")),
                        H2(Lang.T("settings.bepInExFolders")),
                        _bep,
                        Hint(Lang.T("settings.bepInExHint")),
                        H2(Lang.T("settings.worldFolders")),
                        _worlds,
                        Hint(Lang.T("settings.worldsHint", string.Join(", ", Places.WorldRoots().Where(Directory.Exists).Select(Ui.Tilde)))),
                        H2(Lang.T("settings.claude")),
                        _claude,
                        Hint(Lang.T("settings.claudeHint")),
                        _claudeDetails,
                        H2(Lang.T("settings.extensions")),
                        Hint(Lang.T("settings.extensionsHint")),
                        _extensions,
                        H2(Lang.T("settings.expandWorld")),
                        _expandWorld,
                        Hint(Lang.T("settings.expandWorldHint")),
                        new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 4, Children = { _expandWorldFolder, Ui.Col(_expandWorldBrowse, 1) } },
                        Hint(Lang.T("settings.expandWorldFolderHint")),
                        BuildHistory(),
                        drawing,
                        _error,
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0), Children = { cancel, save } },
                    },
                },
            };
        }
    }
}
