using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>One toggle per object kind: the kinds the object actions (and a backup restore) work on.</summary>
    internal Dictionary<ObjectKind, ToggleButton> KindButtons { get; } = [];

    /// <summary>Replace: the kind to replace, from those inside the selection (shown as "name (count)").</summary>
    internal ComboBox FromBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };

    /// <summary>Replace: the kind to put in their place, from the kinds the world can make.</summary>
    internal ComboBox ToBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 400 };

    /// <summary>Eyedropper: the "with" kind picked from the world (the window takes the next click on an object).</summary>
    internal Button ToPickButton { get; } = new() { Content = Lang.T("select.label.pick"), FontSize = 11, Padding = new Thickness(6, 2), Margin = new Thickness(4, 0, 0, 0) };

    /// <summary>
    /// Asks the window to take the next click on an object: the action's name for the prompt, and what to do with
    /// the picked prefab hash.
    /// </summary>
    public event Action<string, Action<int>>? PickKindAsked;

    /// <summary>Reset zones: keep the player's buildings in the zones.</summary>
    internal CheckBox KeepBuildingsBox { get; } = new() { Content = Lang.T("area.label.keepBuildings"), IsChecked = true, FontSize = 12 };

    /// <summary>Reset zones: also undo the zones' ground edits.</summary>
    internal CheckBox ResetGroundBox { get; } = new() { Content = Lang.T("area.label.resetGround"), IsChecked = true, FontSize = 12 };

    /// <summary>Reset zones: unmarks the zones under the selection.</summary>
    internal Button UnresetButton { get; } = new() { Content = Lang.T("area.label.unreset"), FontSize = 12 };

    /// <summary>Runs the current action (Enter); its text names the action. Hidden for Copy and Heightmap.</summary>
    internal Button ApplyButton { get; } = new Button { FontSize = 12 }.Classed("primary");
}
