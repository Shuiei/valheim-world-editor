using Avalonia.Controls;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// The switches and boxes for how objects face, size and lay out.
public sealed partial class PlacePanel
{
    /// <summary>The smallest random scale, 10 to 300%.</summary>
    internal NumericUpDown SizeMinBox { get; } = new() { Value = 80, Minimum = 10, Maximum = 300, Increment = 5, FormatString = "0", FontSize = 12 };

    /// <summary>The largest random scale, 10 to 300%.</summary>
    internal NumericUpDown SizeMaxBox { get; } = new() { Value = 120, Minimum = 10, Maximum = 300, Increment = 5, FormatString = "0", FontSize = 12 };

    /// <summary>Random facing, or all facing the rotation.</summary>
    internal CheckBox RandomYawBox { get; } = new() { Content = Lang.T("place.label.randomYaw"), IsChecked = true, FontSize = 12 };

    /// <summary>
    /// Brush: one at a time, exactly at the cursor (setting it by hand stops it following the chosen kinds).
    /// </summary>
    internal CheckBox SingleBox { get; } = new() { Content = Lang.T("place.label.single"), FontSize = 12 };

    /// <summary>Line: pieces end to end (setting it by hand stops it following the chosen kinds).</summary>
    internal CheckBox EndToEndBox { get; } = new() { Content = Lang.T("place.label.endToEnd"), FontSize = 12 };

    /// <summary>End to end: how many pieces high, 1 to 20.</summary>
    internal NumericUpDown LayersBox { get; } = new() { Value = 1, Minimum = 1, Maximum = 20, Increment = 1, FormatString = "0", FontSize = 12 };

    /// <summary>Line (Points): back to the first point.</summary>
    internal CheckBox LoopBox { get; } = new() { Content = Lang.T("place.label.loop"), FontSize = 12 };

    /// <summary>Line (Points): objects face along the line.</summary>
    internal CheckBox AlongBox { get; } = new() { Content = Lang.T("place.label.along"), IsChecked = true, FontSize = 12 };

    /// <summary>Line (Points): a smooth curve through the points.</summary>
    internal CheckBox CurveBox { get; } = new() { Content = Lang.T("place.label.curve"), IsChecked = true, FontSize = 12 };

    /// <summary>The tilt, rotation, random turn, single, end-to-end, layers and loop controls.</summary>
    private void WireFacingControls(PlaceTool t)
    {
        TiltSlider = Slide(0, 20, 1, t.Tilt, _tiV, v => $"{v:0}°", v => { t.Tilt = v; t.Notify(); });
        RotationSlider = Slide(-180, 180, 1, t.Rotation, _roV, v => $"{v:0}°", v => { if (!_filling) { t.Rotation = v; t.Notify(); } });
        RandomYawBox.IsCheckedChanged += (_, _) => { t.RandomYaw = RandomYawBox.IsChecked == true; t.Notify(); };
        SingleBox.IsCheckedChanged += (_, _) => { if (!_filling) { t.OneAtATime = SingleBox.IsChecked == true; t.OneAtATimeByHand = true; Sync(); t.Notify(); } };
        EndToEndBox.IsCheckedChanged += (_, _) => { if (!_filling) { t.EndToEnd = EndToEndBox.IsChecked == true; t.EndToEndByHand = true; Sync(); t.Notify(); } };
        LayersBox.ValueChanged += (_, e) => { t.Layers = (int)(e.NewValue ?? 1); t.Notify(); };
        LoopBox.IsCheckedChanged += (_, _) => { t.Loop = LoopBox.IsChecked == true; t.Notify(); };
        TiltSlider.Tip("place.tilt");
        RotationSlider.Tip("place.rotation");
    }
}
