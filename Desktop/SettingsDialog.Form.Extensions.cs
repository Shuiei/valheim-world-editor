using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public static partial class SettingsDialog
{
    private sealed partial class Form
    {
        /// <summary>
        /// Builds the mods' extensions list, each one's switch and its state line under it, or a hint when there are none.
        /// Called once the extensions have been found again.
        /// </summary>
        private void BuildExtensions()
        {
            _extensionBoxes = [];
            _extensions = new StackPanel { Spacing = 4 };
            foreach (var x in ModExtensions.Offered)
            {
                var box = new CheckBox { Content = $"{x.Mod} · {Path.GetFileName(x.Dll)}", IsChecked = x.State != ModExtensions.Status.Off }.Classed("switch").Tip("settings.extension");
                ToolTip.SetTip(box, Lang.T("settings.extensionFile", Ui.Tilde(x.Dll), x.Sha256));
                _extensionBoxes.Add((box, x));
                _extensions.Children.Add(box);
                string? state = x.State switch
                {
                    ModExtensions.Status.Running => Lang.T("settings.extensionRunning"),
                    ModExtensions.Status.Failed => Lang.T("settings.extensionFailed", x.Error),
                    _ => x.Error != null ? Lang.T("settings.extensionOff", x.Error) : null,
                };
                if (state != null)
                {
                    _extensions.Children.Add(new TextBlock { Text = state, FontSize = 11, Foreground = _muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(28, 0, 0, 4) });
                }
            }
            if (_extensionBoxes.Count == 0)
            {
                _extensions.Children.Add(Hint(Lang.T("settings.noExtension")));
            }
        }
    }
}
