using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The brush the sculpt and paint tools use (also the Place tool's radius and the noise of natural looks): shape,
/// falloff, size, strength and each tool's own values.
/// </summary>
public sealed partial class Brush
{
    /// <summary>The shape.</summary>
    public BrushShape Shape { get; set; } = BrushShape.Circle;
    /// <summary>How it fades towards its edge (not for Stamp: a picture has its own).</summary>
    public Falloff Falloff { get; set; } = Falloff.Smooth;
    /// <summary>The picture of the Stamp shape (Stamps.Size squared weights), or null.</summary>
    public float[]? StampData { get; set; }
    /// <summary>The stamp's name, for the undo step's label.</summary>
    public string StampLabel { get; set; } = "";
    /// <summary>
    /// Stamp once: a click of Raise or Lower puts the whole picture in, <see cref="StampHeight"/> metres high.
    /// </summary>
    public bool StampOnce { get; set; }
    /// <summary>Stamp once: how high the picture goes in, in metres.</summary>
    public float StampHeight { get; set; } = 4;
    /// <summary>Square brushes and stamps turn (degrees).</summary>
    public float Turn { get; set; }
    /// <summary>The radius in metres (half the side for squares).</summary>
    public float Radius { get; set; } = 6;
    /// <summary>How fast the tools work, from 0 to 1.</summary>
    public float Strength { get; set; } = 0.4f;
    /// <summary>Flatten: the height to level to, in metres.</summary>
    public float Target { get; set; } = 35;
    /// <summary>
    /// Flatten: level to the ground's height where the stroke starts instead of <see cref="Target"/> (which then takes
    /// it).
    /// </summary>
    public bool TargetFromClick { get; set; } = true;
    /// <summary>Naturalize (and natural looks): how big the bumps are, in metres.</summary>
    public float NoiseAmp { get; set; } = 1.2f;
    /// <summary>Naturalize (and natural looks): how wide the bumps are, in metres.</summary>
    public float NoiseSize { get; set; } = 16;
    /// <summary>The noise for the bumps, the Noise shape's edge and the Place tool's clumping.</summary>
    public Noise Noise { get; set; } = new(Random.Shared.Next());
    /// <summary>Erode: rain (Water) instead of sliding to the rest angle (Thermal).</summary>
    public bool ErodeWater { get; set; }
    /// <summary>Erode, Thermal: the slope ground slides down to, in degrees.</summary>
    public float RestAngle { get; set; } = 33;
    /// <summary>Erode, Water: where the raindrops fall.</summary>
    public Random Random { get; set; } = new();

    /// <summary>The tool's name on its button.</summary>
    public static string Label(BrushTool t) => t switch
    {
        BrushTool.Raise => Lang.T("brush.label.raise"),
        BrushTool.Lower => Lang.T("brush.label.lower"),
        BrushTool.Flatten => Lang.T("brush.label.flatten"),
        BrushTool.Smooth => Lang.T("brush.label.smooth"),
        BrushTool.Natural => Lang.T("brush.label.natural"),
        BrushTool.Erode => Lang.T("brush.label.erode"),
        BrushTool.Restore => Lang.T("brush.label.restore"),
        BrushTool.PaintDirt => Lang.T("brush.label.dirt"),
        BrushTool.PaintCultivated => Lang.T("brush.label.cultivated"),
        BrushTool.PaintPaved => Lang.T("brush.label.paved"),
        _ => Lang.T("brush.label.clear"),
    };

    /// <summary>What the tool does, in a line.</summary>
    public static string Help(BrushTool t) => t switch
    {
        BrushTool.Raise => Lang.T("brush.help.raise"),
        BrushTool.Lower => Lang.T("brush.help.lower"),
        BrushTool.Flatten => Lang.T("brush.help.flatten"),
        BrushTool.Smooth => Lang.T("brush.help.smooth"),
        BrushTool.Natural => Lang.T("brush.help.natural"),
        BrushTool.Erode => Lang.T("brush.help.erode"),
        BrushTool.Restore => Lang.T("brush.help.restore"),
        BrushTool.PaintDirt => Lang.T("brush.help.dirt"),
        BrushTool.PaintCultivated => Lang.T("brush.help.cultivated"),
        BrushTool.PaintPaved => Lang.T("brush.help.paved"),
        _ => Lang.T("brush.help.clear"),
    };

    /// <summary>
    /// The colour each paint tool moves the paint towards (dirt, cultivated, paved, vegetation weights); null for the
    /// sculpt tools.
    /// </summary>
    public static float[]? PaintOf(BrushTool t) => t switch
    {
        BrushTool.PaintDirt => [1f, 0, 0, 1],
        BrushTool.PaintCultivated => [0f, 1, 0, 1],
        BrushTool.PaintPaved => [0f, 0, 1, 1],
        BrushTool.PaintClear => [0f, 0, 0, 1],
        _ => null,
    };

    /// <summary>The falloff's weight, from 0 to 1, at <c>t</c>: 1 at the middle of the brush, 0 at its edge.</summary>
    public static float Fade(Falloff f, float t) => f switch
    {
        Falloff.Linear => t,
        Falloff.Dome => MathF.Sqrt(MathF.Max(0, 1 - (1 - t) * (1 - t))),
        Falloff.Flat => t >= 0.2f ? 1 : (t / 0.2f) * (t / 0.2f) * (3 - 2 * t / 0.2f),
        Falloff.Peak => t * t * t,
        // Full strength right up to the edge, nothing beyond: steep walls, like the game's pickaxe.
        Falloff.Sharp => t > 0 ? 1 : 0,
        _ => t * t * (3 - 2 * t),
    };

    /// <summary>Square brushes and stamps turn.</summary>
    private bool Turnable => Shape is BrushShape.Square or BrushShape.Stamp;

    /// <summary>The weight of a point (dx, dz) from the brush middle, from 0 to 1.</summary>
    /// <param name="dx">East of the middle, in metres.</param>
    /// <param name="dz">North of the middle, in metres.</param>
    /// <param name="wx">Its world x, for the noise.</param>
    /// <param name="wz">Its world z, for the noise.</param>
    public float Weight(float dx, float dz, float wx = 0, float wz = 0)
    {
        float r = Radius, u = dx / r, v = dz / r;
        if (Turnable)
        {
            float a = -Turn * MathF.PI / 180, c = MathF.Cos(a), s = MathF.Sin(a);
            (u, v) = ((dx * c - dz * s) / r, (dx * s + dz * c) / r);
        }
        // A picture has its own falloff.
        if (Shape == BrushShape.Stamp)
        {
            return StampData != null ? Stamps.Sample(StampData, u, v) : 0;
        }
        float d = Shape switch
        {
            BrushShape.Square => MathF.Max(MathF.Abs(u), MathF.Abs(v)),
            BrushShape.Ring => MathF.Abs(MathF.Sqrt(u * u + v * v) - 0.7f) / 0.3f,
            BrushShape.Noise => MathF.Sqrt(u * u + v * v) * (1 + 0.35f * Noise.Fbm(wx / MathF.Max(2, r * 0.45f), wz / MathF.Max(2, r * 0.45f))),
            _ => MathF.Sqrt(u * u + v * v),
        };
        return d >= 1 ? 0 : Fade(Falloff, 1 - d);
    }

    /// <summary>
    /// How far from the middle a point can be affected: a turned square reaches its corners; the Noise shape reaches
    /// 1 / (1 - 0.35 × the noise's lowest), and the noise stays above -0.9 in practice (-0.89 over 8 million samples),
    /// so 1.5 radii.
    /// </summary>
    public float Reach => Shape == BrushShape.Noise ? Radius * 1.5f : Turnable ? Radius * 1.42f : Radius;
}
