using Avalonia.Controls;

namespace TerrainEditor.Desktop;

// The sliders: size, density, spacing, clumping, patch, tilt, rotation, every, wiggle and cell.
public sealed partial class PlacePanel
{
    /// <summary>Brush: its radius, 1 to 30 m.</summary>
    internal Slider SizeSlider { get; private set; } = null!;

    /// <summary>Objects per 100 m², 0.2 to 20.</summary>
    internal Slider DensitySlider { get; private set; } = null!;

    /// <summary>The least distance between objects, 0.5 m to <c>PlaceTool.MaxSpacing</c>.</summary>
    internal Slider SpacingSlider { get; private set; } = null!;

    /// <summary>Clumping, 0 to 100%.</summary>
    internal Slider ClumpSlider { get; private set; } = null!;

    /// <summary>The clumping pattern's size, 5 to 120 m (shown when clumping is on).</summary>
    internal Slider PatchSlider { get; private set; } = null!;

    /// <summary>The most random lean, 0 to 20 degrees.</summary>
    internal Slider TiltSlider { get; private set; } = null!;

    /// <summary>The turn added to every facing, -180 to 180 degrees.</summary>
    internal Slider RotationSlider { get; private set; } = null!;

    /// <summary>Line: the distance between objects, 0.5 m to <c>PlaceTool.MaxEvery</c>.</summary>
    internal Slider EverySlider { get; private set; } = null!;

    /// <summary>Line: the random sideways stray, 0 to 5 m.</summary>
    internal Slider WiggleSlider { get; private set; } = null!;

    /// <summary>Grid: the spacing, 1 m to <c>PlaceTool.MaxCell</c>.</summary>
    internal Slider CellSlider { get; private set; } = null!;

    /// <summary>
    /// A slider that shows its value through <c>fmt</c> in <c>shown</c> and passes each new value to <c>set</c>.
    /// </summary>
    private static Slider Slide(double min, double max, double step, double value, TextBlock shown, Func<double, string> fmt, Action<float> set) =>
        Ui.Slide(min, max, step, value, shown, fmt, v => set((float)v));

    /// <summary>The size, density, spacing, clumping and patch sliders.</summary>
    private void WireSizeSliders(PlaceTool t)
    {
        SizeSlider = Slide(1, 30, 0.5, t.Brush.Radius, _sizeV, v => $"{v:0.#} m", v => { t.Brush.Radius = v; t.Notify(); });
        DensitySlider = Slide(0.2, 20, 0.2, t.Density, _densV, v => $"{v:0.0}", v => { t.Density = v; t.Notify(); });
        SpacingSlider = Slide(0.5, PlaceTool.MaxSpacing, 0.5, t.Spacing, _spV, v => $"{v:0.#} m", v => { t.Spacing = v; t.Notify(); });
        ClumpSlider = Slide(0, 100, 5, t.Clump, _clV, v => $"{v:0}%", v => { t.Clump = v; Sync(); t.Notify(); });
        PatchSlider = Slide(5, 120, 5, t.Patch, _paV, v => $"{v:0} m", v => { t.Patch = v; t.Notify(); });
        SizeMinBox.ValueChanged += (_, e) => { t.SizeMin = (float)(e.NewValue ?? 80); t.Notify(); };
        SizeMaxBox.ValueChanged += (_, e) => { t.SizeMax = (float)(e.NewValue ?? 120); t.Notify(); };
        SizeSlider.Tip("place.size");
        DensitySlider.Tip("place.density");
        SpacingSlider.Tip("place.spacing");
        ClumpSlider.Tip("place.clump");
        PatchSlider.Tip("place.patch");
    }

    /// <summary>The every, wiggle, along, curve and cell controls.</summary>
    private void WireLineControls(PlaceTool t)
    {
        EverySlider = Slide(0.5, PlaceTool.MaxEvery, 0.5, t.Every, _evV, v => $"{v:0.#} m", v => { t.Every = v; t.Notify(); });
        WiggleSlider = Slide(0, 5, 0.25, t.Wiggle, _wiV, v => $"{v:0.##} m", v => { t.Wiggle = v; t.Notify(); });
        AlongBox.IsCheckedChanged += (_, _) => { t.Along = AlongBox.IsChecked == true; t.Notify(); };
        CurveBox.IsCheckedChanged += (_, _) => { t.Curve = CurveBox.IsChecked == true; t.Notify(); };
        EverySlider.Tip("place.every");
        WiggleSlider.Tip("place.wiggle");
        CellSlider = Slide(1, PlaceTool.MaxCell, 0.5, t.Cell, _ceV, v => $"{v:0.#} m", v => { t.Cell = v; t.Notify(); });
        CellSlider.Tip("place.cell");
    }

    /// <summary>The heading of the mode's own settings (Brush, Along the line, Grid, Zone fill).</summary>
    /// <summary>The size slider's shown value.</summary>
    private readonly TextBlock _sizeV = new(), _densV = new(), _spV = new(), _clV = new(), _paV = new(), _tiV = new(), _roV = new(), _evV = new(), _wiV = new(), _ceV = new();
}
