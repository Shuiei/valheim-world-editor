using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

public sealed partial class AreaPanel
{
    /// <summary>Switches the selection to a box dragged on the ground (clears the current one).</summary>
    internal Button BoxButton { get; } = new() { Content = Lang.T("area.label.box"), FontSize = 12 };

    /// <summary>Switches the selection to a polygon clicked point by point (clears the current one).</summary>
    internal Button PolyButton { get; } = new() { Content = Lang.T("area.label.poly"), FontSize = 12 };

    /// <summary>Clears the selection.</summary>
    internal Button ClearButton { get; } = new() { Content = Lang.T("common.clearEsc"), FontSize = 12 };

    /// <summary>The selection's size and what is inside, or how to make one.</summary>
    internal TextBlock Info { get; } = new() { FontSize = 12, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };

    /// <summary>The soft edge, 0 to 20 m: how far inwards from the outline an action fades in.</summary>
    internal Slider SoftSlider { get; }

    /// <summary>The action list.</summary>
    internal ComboBox ActionBox { get; }

    /// <summary>Flatten: the height to flatten to, in metres.</summary>
    internal NumericUpDown HeightBox { get; } = new() { Value = 35, Increment = 0.1m, FormatString = "0.0#", FontSize = 12 };

    /// <summary>Puts the average ground height inside the selection into <see cref="HeightBox"/>.</summary>
    internal Button AverageButton { get; } = new() { Content = Lang.T("area.label.avg"), FontSize = 11, Padding = new Thickness(6, 2) };

    /// <summary>Raise and Lower: by how much, in metres.</summary>
    internal NumericUpDown AmountBox { get; } = new() { Value = 2, Increment = 0.1m, FormatString = "0.0#", FontSize = 12 };

    /// <summary>Paint: which paint (one of <c>Paints</c>).</summary>
    internal ComboBox PaintBox { get; } = new() { ItemsSource = Paints.Select(PaintLabel).ToArray(), SelectedIndex = 0, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Stretch };

    /// <summary>
    /// Flatten, Raise and Lower: No limit, the ground goes past the game's ±8 m (the brush options' switch, the
    /// same one shown here; the window keeps the two together).
    /// </summary>
    internal CheckBox NoLimitBox { get; } = new() { Content = Lang.T("brush.label.noLimit"), FontSize = 12 };

    /// <summary>No limit was switched here (on: true).</summary>
    public event Action<bool>? NoLimitChanged;

    /// <summary>Flatten, Raise and Lower: the cut and fill the action would move.</summary>
    internal TextBlock VolumeText { get; } = new() { FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap };
}
