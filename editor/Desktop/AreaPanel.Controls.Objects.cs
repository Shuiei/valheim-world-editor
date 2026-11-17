using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>One toggle per object kind: the kinds Select objects, Regrow nature and a backup restore work on.</summary>
    internal Dictionary<ObjectKind, ToggleButton> KindButtons { get; } = [];

    /// <summary>
    /// Select objects: every object of the chosen kinds (its first entry), or only one kind of object among those
    /// inside the selection (shown as "name (count)").
    /// </summary>
    internal ComboBox OnlyBox { get; } = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch, MaxDropDownHeight = 400 };

    /// <summary>Reset zones: keep the player's buildings in the zones (the map's zone filter has the same switch).</summary>
    internal CheckBox KeepBuildingsBox { get; } = new() { Content = Lang.T("reset.label.keepBuildings"), IsChecked = true, FontSize = 12 };

    /// <summary>Reset zones: also undo the zones' ground edits (the map's zone filter has the same switch).</summary>
    internal CheckBox ResetGroundBox { get; } = new() { Content = Lang.T("reset.label.ground"), IsChecked = true, FontSize = 12 };

    /// <summary>Reset zones: unmarks the zones under the selection.</summary>
    internal Button UnresetButton { get; } = new() { Content = Lang.T("area.label.unreset"), FontSize = 12 };

    /// <summary>Reset zones: unmarks every zone marked for reset, in the area shown or not.</summary>
    internal Button UnresetAllButton { get; } = new() { Content = Lang.T("reset.label.unmarkAll"), FontSize = 12 };

    /// <summary>Runs the current action (Enter); its text names the action. Hidden for the clipboard and Heightmap.</summary>
    internal Button ApplyButton { get; } = new Button { FontSize = 12 }.Classed("primary");
}
