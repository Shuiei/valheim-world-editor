
namespace TerrainEditor.Desktop;

/// <summary>
/// The Path tool panel's control wiring: the depth minimum, the curve and Natural look checks, Apply, Clear, and the tips.
/// </summary>
public sealed partial class PathPanel
{
    /// <summary>Wires the curve, natural look, Apply and Clear controls, and sets the tips of the controls.</summary>
    private void WireControls()
    {
        HeightBox.ValueChanged += (_, e) => _path.Height = (float)(e.NewValue ?? 0);
        AmountBox.ValueChanged += (_, e) => _path.Amount = (float)(e.NewValue ?? 0);
        StartBox.ValueChanged += (_, e) => { _path.Start = (float)(e.NewValue ?? 0); if (!_filling) { _path.RampEdited = true; } };
        EndBox.ValueChanged += (_, e) => { _path.End = (float)(e.NewValue ?? 0); if (!_filling) { _path.RampEdited = true; } };
        DepthBox.ValueChanged += (_, e) => _path.Depth = (float)(e.NewValue ?? 0);
        DepthBox.Minimum = 0.25m;
        CurveBox.IsCheckedChanged += (_, _) => { _path.Curved = CurveBox.IsChecked == true; _path.Notify(); };
        NaturalBox.IsCheckedChanged += (_, _) => { _path.Natural = NaturalBox.IsChecked == true; ShowRows(); };
        ApplyButton.Click += (_, _) => ApplyAsked?.Invoke();
        ActionBox.Tip("path.action");
        WidthSlider.Tip("path.width");
        SoftSlider.Tip("path.soft");
        HeightBox.Tip("path.height");
        AmountBox.Tip("path.amount");
        StartBox.Tip("path.start");
        EndBox.Tip("path.end");
        DepthBox.Tip("path.depth");
        CurveBox.Tip("path.curve");
        NaturalBox.Tip("path.natural");
        ApplyButton.Tip("path.apply");
        ClearButton.Tip("path.clear");
        ClearButton.Click += (_, _) => _path.Clear();
    }
}
