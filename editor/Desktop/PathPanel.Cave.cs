using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Path tool panel's cave controls: the preset list, Randomize, depth, headroom and roof boulders, and the
/// preset values they fill in.
/// </summary>
public sealed partial class PathPanel
{
    /// <summary>Wires the cave controls to the Path tool.</summary>
    /// <param name="rocks">The roof boulders, in the roof list's order.</param>
    private void WireCave(PathTool.CaveRock[] rocks)
    {
        RockBox.SelectionChanged += (_, _) => _path.Rock = rocks[Math.Max(0, RockBox.SelectedIndex)];
        CaveDepthBox.Minimum = 2;
        CaveDepthBox.Maximum = 20;
        HeadroomBox.Minimum = 2;
        HeadroomBox.Maximum = 15;
        CaveDepthBox.ValueChanged += (_, e) => _path.CaveDepth = (float)(e.NewValue ?? 7.5m);
        HeadroomBox.ValueChanged += (_, e) => _path.Headroom = (float)(e.NewValue ?? 4.5m);
        CavePresetBox.SelectionChanged += (_, _) => CavePreset(false);
        CaveRandomButton.Click += (_, _) => CavePreset(true);
        CavePresetBox.Tip("cave.preset");
        CaveRandomButton.Tip("cave.randomize");
        CaveDepthBox.Tip("cave.depth");
        HeadroomBox.Tip("cave.headroom");
        RockBox.Tip("cave.rock");
    }

    /// <summary>The cave preset's values (random: each within about 15% of them, and new boulders).</summary>
    internal void CavePreset(bool random)
    {
        var p = _presets[Math.Max(0, CavePresetBox.SelectedIndex)];
        float F() => random ? 0.85f + 0.3f * (float)_random.NextDouble() : 1;
        static decimal Half(float v) => Math.Round((decimal)v * 2) / 2;
        WidthSlider.Value = (double)Half(p.Width * F());
        SoftSlider.Value = (double)Half(p.Soft * F());
        CaveDepthBox.Value = Math.Min(20, Half(p.Depth * F()));
        HeadroomBox.Value = Half(p.Headroom * F());
        _path.Seed = random ? _random.Next() : _path.Seed;
        CaveHelp.Text = random ? p.Help + " " + Lang.T("cave.hint.randomized") : p.Help;
    }

    /// <summary>The Cave button: the Cave action with its preset's values.</summary>
    public void ChooseCave()
    {
        ActionBox.SelectedIndex = Array.IndexOf(Enum.GetValues<PathTool.Action>(), PathTool.Action.Cave);
        CavePreset(false);
    }
}
