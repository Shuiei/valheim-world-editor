using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The sculpt and paint tools, as the web editor applies them every frame while the button is held (editor.html
/// applyBrush), with the brush shapes and falloffs of editor/brushes.js.
/// </summary>
public enum BrushTool
{
    /// <summary>Lifts the ground under the brush.</summary>
    Raise,
    /// <summary>Digs the ground down under the brush.</summary>
    Lower,
    /// <summary>Levels the ground to one height (the brush's Target, or where the stroke starts).</summary>
    Flatten,
    /// <summary>Evens out bumps and sharp edges.</summary>
    Smooth,
    /// <summary>Naturalize: flat, tool-made ground towards natural-looking bumps.</summary>
    Natural,
    /// <summary>Weathering: Thermal (slopes slide to the rest angle) or Water (rain).</summary>
    Erode,
    /// <summary>Back towards the ground as the world generated it.</summary>
    Restore,
    /// <summary>Paints bare dirt, like a path.</summary>
    PaintDirt,
    /// <summary>Paints cultivated soil for planting.</summary>
    PaintCultivated,
    /// <summary>Paints paved stone ground.</summary>
    PaintPaved,
    /// <summary>Removes paint: back to the biome's own ground.</summary>
    PaintClear,
}

/// <summary>
/// The brush's shape: Ring works on a band near the edge, Noise has a ragged edge, Stamp uses a picture
/// (<see cref="Brush.StampData"/>).
/// </summary>
public enum BrushShape
{
    /// <summary>A round brush.</summary>
    Circle,
    /// <summary>A square brush.</summary>
    Square,
    /// <summary>Works on a band near the edge.</summary>
    Ring,
    /// <summary>A round brush with a ragged edge.</summary>
    Noise,
    /// <summary>Shaped by a picture (<see cref="Brush.StampData"/>).</summary>
    Stamp,
}

/// <summary>How the brush fades from its middle to its edge (see <see cref="Brush.Fade"/>).</summary>
public enum Falloff
{
    /// <summary>An S-curve (smoothstep): soft at both ends.</summary>
    Smooth,
    /// <summary>Straight from the middle to the edge.</summary>
    Linear,
    /// <summary>Round on top, falling fast near the edge.</summary>
    Dome,
    /// <summary>Full strength, fading only in the outer fifth.</summary>
    Flat,
    /// <summary>Strong only near the middle (cubic).</summary>
    Peak,
    /// <summary>Full strength right up to the edge: steep walls, like the game's pickaxe.</summary>
    Sharp,
}

/// <summary>
/// The brush the sculpt and paint tools use (also the Place tool's radius and the noise of natural looks): shape,
/// falloff, size, strength and each tool's own values.
/// </summary>
public sealed class Brush
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

    /// <summary>The outline drawn on the ground: n points around the brush, as offsets from its middle.</summary>
    public List<(float X, float Z)> Outline(int n)
    {
        var pts = new List<(float, float)>();
        float a = Turn * MathF.PI / 180;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n * MathF.Tau, x = MathF.Cos(t), z = MathF.Sin(t);
            if (Turnable)
            {
                float m = MathF.Max(MathF.Abs(x), MathF.Abs(z));
                x /= m;
                z /= m;
                (x, z) = (x * MathF.Cos(a) - z * MathF.Sin(a), x * MathF.Sin(a) + z * MathF.Cos(a));
            }
            pts.Add((x * Radius, z * Radius));
        }
        return pts;
    }

    /// <summary>The Ring shape works on a band from 40% of the radius to the edge: its inner edge, or null.</summary>
    public List<(float X, float Z)>? InnerOutline(int n) => Shape != BrushShape.Ring ? null
        : Enumerable.Range(0, n).Select(i => (MathF.Cos(i / (float)n * MathF.Tau) * Radius * 0.4f, MathF.Sin(i / (float)n * MathF.Tau) * Radius * 0.4f)).ToList();
}

/// <summary>One stroke of a brush: from the button going down to it coming up.</summary>
public sealed class Stroke
{
    /// <summary>The tool of the stroke.</summary>
    public required BrushTool Tool { get; init; }
    /// <summary>The ground as it was when the stroke started.</summary>
    public required Ground.State Start { get; init; }
    /// <summary>The grid points the stroke changed.</summary>
    public HashSet<int> Touched { get; } = [];
    /// <summary>Naturalize: each point's aim, worked out once per stroke.</summary>
    public Dictionary<int, float> Natural { get; } = [];
    /// <summary>
    /// Flatten: the height taken from the ground where the stroke started (Target from click), or null for the brush's.
    /// </summary>
    public float? Target { get; set; }
    /// <summary>The game's ±8 m limit stopped the ground somewhere.</summary>
    public bool Clamped { get; set; }

    /// <summary>A point's height when the stroke started.</summary>
    public float StartHeight(Ground g, int p) => g.Base[p] + Start.Lift[p] + (Start.Mod[p] != 0
        ? Math.Clamp(Start.Level[p] + Start.Smooth[p], -Editing.EditStore.MaxLevel, Editing.EditStore.MaxLevel)
        : 0);
}

/// <summary>Applies the sculpt and paint tools to the ground.</summary>
public static class Sculpt
{
    /// <summary>
    /// One frame of a stroke with the brush's middle at grid point (cx, cz), <c>dt</c> seconds after the last (counted
    /// as 0.1 at most). Returns the grid rectangle changed (to redraw), or null.
    /// </summary>
    /// <param name="g">The ground.</param>
    /// <param name="b">The brush.</param>
    /// <param name="s">The stroke.</param>
    /// <param name="cx">The brush middle's grid x.</param>
    /// <param name="cz">The brush middle's grid z.</param>
    /// <param name="dt">Seconds since the last frame.</param>
    /// <param name="mask">How much each point may change (1 everywhere when null).</param>
    public static (int X0, int Z0, int X1, int Z1)? Apply(Ground g, Brush b, Stroke s, float cx, float cz, float dt, Func<int, float>? mask = null)
    {
        float reach = b.Reach;
        int x0 = Math.Max(1, (int)MathF.Floor(cx - reach)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(cx + reach));
        int z0 = Math.Max(1, (int)MathF.Floor(cz - reach)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(cz + reach));
        if (x0 > x1 || z0 > z1)
        {
            return null;
        }
        float rate = b.Strength * MathF.Min(dt, 0.1f);
        float ox = g.X0 * 64 - 32, oz = g.Z0 * 64 - 32;
        // Erosion works on the neighbourhood of every point (Erosion).
        if (s.Tool == BrushTool.Erode)
        {
            float r = b.Radius, er = reach + 2, talus = Erosion.Talus(b.RestAngle);
            bool clamped = false;
            var (ex0, ez0, ex1, ez1) = Erosion.Run(g, (int)MathF.Floor(cx - er), (int)MathF.Floor(cz - er), (int)MathF.Ceiling(cx + er), (int)MathF.Ceiling(cz + er),
                (gx, gz, p) => b.Weight(gx - cx, gz - cz, ox + gx, oz + gz) * (mask?.Invoke(p) ?? 1),
                (h, wt, w, d) =>
                {
                    if (b.ErodeWater)
                    {
                        Erosion.Hydraulic(h, wt, w, d, (int)MathF.Ceiling(r * r * rate * 6), 1, b.Random);
                    }
                    else
                    {
                        Erosion.Thermal(h, wt, w, d, talus, MathF.Min(1, rate * 12), 2);
                    }
                }, s.Touched, ref clamped);
            s.Clamped |= clamped;
            return (ex0 - 1, ez0 - 1, ex1 + 1, ez1 + 1);
        }
        // Smoothing reads the heights from before this frame.
        Dictionary<int, float>? snapshot = null;
        if (s.Tool == BrushTool.Smooth)
        {
            snapshot = [];
            for (int gz = z0 - 1; gz <= z1 + 1; gz++)
            {
                for (int gx = x0 - 1; gx <= x1 + 1; gx++)
                {
                    snapshot[gz * g.W + gx] = g.HeightOf(gz * g.W + gx);
                }
            }
        }
        float[]? colour = Brush.PaintOf(s.Tool);
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                if (g.Locked(gx, gz))
                {
                    continue;
                }
                float wb = b.Weight(gx - cx, gz - cz, ox + gx, oz + gz);
                if (wb <= 0)
                {
                    continue;
                }
                int p = gz * g.W + gx;
                float w = wb * (mask?.Invoke(p) ?? 1);
                if (w <= 0)
                {
                    continue;
                }
                float h = g.HeightOf(p);
                s.Touched.Add(p);
                switch (s.Tool)
                {
                    case BrushTool.Raise:
                        s.Clamped |= g.SetHeight(p, h + 6 * rate * w);
                        break;
                    case BrushTool.Lower:
                        s.Clamped |= g.SetHeight(p, h - 6 * rate * w);
                        break;
                    case BrushTool.Flatten:
                        s.Clamped |= g.SetHeight(p, h + ((s.Target ?? b.Target) - h) * MathF.Min(1, 8 * rate * w));
                        break;
                    case BrushTool.Smooth:
                    {
                        float sum = 0;
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                sum += snapshot![(gz + dz) * g.W + gx + dx];
                            }
                        }
                        s.Clamped |= g.SetHeight(p, h + (sum / 9 - h) * MathF.Min(1, 10 * rate * w));
                        break;
                    }
                    case BrushTool.Natural:
                        s.Clamped |= g.SetHeight(p, h + (NaturalTarget(g, b, s, p) - h) * MathF.Min(1, 4 * rate * w));
                        break;
                    case BrushTool.Restore:
                    {
                        float k = MathF.Min(1, 8 * rate * w);
                        if (g.Mod[p] != 0)
                        {
                            g.Level[p] *= 1 - k;
                            g.Smooth[p] *= 1 - k;
                            if (MathF.Abs(g.Level[p]) + MathF.Abs(g.Smooth[p]) < 0.01f)
                            {
                                g.Level[p] = 0;
                                g.Smooth[p] = 0;
                                g.Mod[p] = 0;
                            }
                        }
                        // Also back down from a No limit lift.
                        g.Lift[p] = MathF.Abs(g.Lift[p] * (1 - k)) < 0.01f ? 0 : g.Lift[p] * (1 - k);
                        if (g.PMod[p] != 0 && w > 0.5f)
                        {
                            g.PMod[p] = 0;
                        }
                        break;
                    }
                    default:
                    {
                        float k = MathF.Min(1, 8 * rate * w);
                        g.StartPaint(p);
                        for (int c = 0; c < 4; c++)
                        {
                            g.Paint[p * 4 + c] += (colour![c] - g.Paint[p * 4 + c]) * k;
                        }
                        break;
                    }
                }
            }
        }
        return (x0 - 1, z0 - 1, x1 + 1, z1 + 1);
    }

    /// <summary>
    /// Stamp once: the ground under the stamp moves by <c>amount</c> metres times the picture, all at once. Returns the
    /// points changed, the rectangle around them, and whether the game's limit stopped some.
    /// </summary>
    public static (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, bool Clamped) StampOnce(Ground g, Brush b, float cx, float cz, float amount, Func<int, float>? mask)
    {
        var touched = new List<int>();
        bool clamped = false;
        float reach = b.Reach, ox = g.X0 * 64 - 32, oz = g.Z0 * 64 - 32;
        int x0 = Math.Max(1, (int)MathF.Floor(cx - reach)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(cx + reach));
        int z0 = Math.Max(1, (int)MathF.Floor(cz - reach)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(cz + reach));
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                if (g.Locked(gx, gz))
                {
                    continue;
                }
                int p = gz * g.W + gx;
                float w = b.Weight(gx - cx, gz - cz, ox + gx, oz + gz) * (mask?.Invoke(p) ?? 1);
                if (w <= 0)
                {
                    continue;
                }
                clamped |= g.SetHeight(p, g.HeightOf(p) + amount * w);
                touched.Add(p);
            }
        }
        return (touched, (x0 - 1, z0 - 1, x1 + 1, z1 + 1), clamped);
    }

    /// <summary>
    /// Naturalize's aim: the ground's broad shape (blurred, from the start of the stroke) plus detail noise, in world
    /// coordinates so the pattern is continuous across areas.
    /// </summary>
    private static float NaturalTarget(Ground g, Brush b, Stroke s, int p)
    {
        if (s.Natural.TryGetValue(p, out float t))
        {
            return t;
        }
        int gx = p % g.W, gz = p / g.W, r = Math.Max(2, Math.Min(8, (int)MathF.Round(b.NoiseSize / 3)));
        float sum = 0;
        int n = 0;
        for (int dz = -r; dz <= r; dz += 2)
        {
            for (int dx = -r; dx <= r; dx += 2)
            {
                int x = Math.Clamp(gx + dx, 0, g.W - 1), z = Math.Clamp(gz + dz, 0, g.H - 1);
                sum += s.StartHeight(g, z * g.W + x);
                n++;
            }
        }
        float wx = g.X0 * 64 - 32 + gx, wz = g.Z0 * 64 - 32 + gz;
        t = sum / n + b.NoiseAmp * b.Noise.Fbm(wx / b.NoiseSize, wz / b.NoiseSize);
        s.Natural[p] = t;
        return t;
    }
}
