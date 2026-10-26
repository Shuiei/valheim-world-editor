using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public static partial class SettingsDialog
{
    internal sealed partial class Form
    {
        /// <summary>
        /// Builds the mods' extensions list, each one's switch and its state line under it, or a hint when there are none.
        /// Called once the extensions have been found again.
        /// </summary>
        private void BuildExtensions()
        {
            _extensionBoxes = [];
            _extensions = new StackPanel { Spacing = 4 };
            // Add-ons' extensions have no switch: they are listed with the add-ons.
            foreach (var x in ModExtensions.Offered.Where(x => !x.AddOn))
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
            BuildAddOns();
        }

        /// <summary>
        /// Builds the add-ons' list (AddOns): each one's switch, with its name and the mod it serves, and what became of
        /// it under it, or where to install one when there is none.
        /// </summary>
        private void BuildAddOns()
        {
            _addOns = new StackPanel { Spacing = 4 };
            _addOnBoxes = [];
            foreach (var a in ModManifests.InstalledAddOns)
            {
                var box = new CheckBox { Content = a.Serves is { } mod ? Lang.T("settings.addOnFor", a.Name, mod) : a.Name, IsChecked = a.On }.Classed("switch").Tip("settings.addOn");
                ToolTip.SetTip(box, Ui.Tilde(a.Folder));
                _addOnBoxes.Add((box, a));
                _addOns.Children.Add(box);
                _addOns.Children.Add(new TextBlock { Text = AddOnState(a), FontSize = 11, Foreground = _muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(28, 0, 0, 4) });
            }
            if (ModManifests.InstalledAddOns.Count == 0)
            {
                _addOns.Children.Add(Hint(Lang.T("settings.noAddOn", string.Join(", ", AddOns.Folders.Select(Ui.Tilde)))));
            }
        }

        /// <summary>
        /// What became of an add-on: turned off, waiting for its mod, in use with only its notes, or its extension's state.
        /// </summary>
        internal static string AddOnState(AddOns.AddOn a)
        {
            if (!a.On)
            {
                return Lang.T("settings.addOnOff");
            }
            if (!a.Served)
            {
                return Lang.T("settings.addOnWaiting", a.Serves);
            }
            if (a.Dll == null)
            {
                return Lang.T("settings.addOnNotes");
            }
            return ModExtensions.Offered.FirstOrDefault(x => x.AddOn && x.Dll == a.Dll) switch
            {
                null => Lang.T("settings.addOnNotRead"),
                { State: ModExtensions.Status.Running } => Lang.T("settings.extensionRunning"),
                { State: ModExtensions.Status.Failed } x => Lang.T("settings.extensionFailed", x.Error),
                var x => Lang.T("settings.extensionOff", x.Error),
            };
        }
    }
}
