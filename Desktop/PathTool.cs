using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Path tool, like the web editor's (editor.html, Path tool): draw a line, then apply an action along it with a
/// width and a soft edge: flatten to a height, ramp from start to end, raise or lower, smooth, dig a river, dig a cave,
/// or paint. The line is kept after applying, so another action can follow. Cave: a trench along the line (its floor
/// follows the ground's lie below it, deepest in the middle, with an entrance slope at each end), roofed over with the
/// game's boulders where it is deep enough (CaveRoof): the ground cannot overhang (one height per point), so the roof
/// is rocks, like the game's own caves. Points are grid points of the block (x east, z north, one per metre).
/// </summary>
public sealed class PathTool
{
    /// <summary>What Apply does along the line; the panel lists them in this order.</summary>
    public enum Action
    {
        /// <summary>Flattens the ground to a height.</summary>
        Flatten,
        /// <summary>A slope from the start's height to the end's.</summary>
        Ramp,
        /// <summary>Raises the ground.</summary>
        Raise,
        /// <summary>Lowers the ground.</summary>
        Lower,
        /// <summary>Evens out bumps.</summary>
        Smooth,
        /// <summary>Digs a river bed.</summary>
        River,
        /// <summary>Digs a trench roofed over with the game's boulders.</summary>
        Cave,
        /// <summary>Paints dirt.</summary>
        PaintDirt,
        /// <summary>Paints paving.</summary>
        PaintPaved,
        /// <summary>Paints cultivated ground.</summary>
        PaintCultivated,
        /// <summary>Removes paint.</summary>
        PaintClear,
    }

    /// <summary>
    /// The boulders a cave's roof is made of. Auto picks them by the biome under each boulder; the others are the
    /// game's big world rocks of that biome (see <see cref="RoofRocks"/>).
    /// </summary>
    public enum CaveRock
    {
        /// <summary>Picked by the biome under each boulder.</summary>
        Auto,
        /// <summary>Forest boulders (rock4_forest); Auto's pick in most biomes.</summary>
        Forest,
        /// <summary>Coast boulders (rock4_coast); Auto's pick over Ocean.</summary>
        Coast,
        /// <summary>Heath boulders (rock4_heath); Auto's pick in the Plains.</summary>
        Heath,
        /// <summary>Mountain boulders (rock3_mountain); Auto's pick in the Mountains and Deep North.</summary>
        Mountain,
    }

    /// <summary>
    /// Each roof boulder's prefab, how wide it is at least (m, unscaled) and how far its lowest point is below its
    /// middle (CaveRoof hangs them by their real underside). These rocks keep a saved size. Auto has no entry: it
    /// stands for one of the others.
    /// </summary>
    public static readonly Dictionary<CaveRock, (string Prefab, float Footprint, float Bottom)> RoofRocks = new()
    {
        [CaveRock.Forest] = ("rock4_forest", 24.2f, 12.9f),
        [CaveRock.Coast] = ("rock4_coast", 24.2f, 12.9f),
        [CaveRock.Heath] = ("rock4_heath", 24.2f, 12.9f),
        [CaveRock.Mountain] = ("rock3_mountain", 12.7f, 8.2f),
    };

    /// <summary>The roof choice as the panel lists it.</summary>
    public static string Label(CaveRock r) => r switch
    {
        CaveRock.Auto => "By biome",
        CaveRock.Forest => "Forest boulders",
        CaveRock.Coast => "Coast boulders",
        CaveRock.Heath => "Heath boulders",
        _ => "Mountain rocks",
    };

    /// <summary>A cave preset the panel offers.</summary>
    /// <param name="Name">The preset as listed.</param>
    /// <param name="Width">Width in metres.</param>
    /// <param name="Soft">Soft edge in metres: the walls' slope.</param>
    /// <param name="Depth">Depth below the ground in metres.</param>
    /// <param name="Headroom">Headroom inside in metres.</param>
    /// <param name="Help">The line the panel shows under the preset.</param>
    public sealed record CavePreset(string Name, float Width, float Soft, float Depth, float Headroom, string Help);

    /// <summary>Tunnel, Cave and Cavern, as the panel lists them.</summary>
    public static readonly CavePreset[] CavePresets =
    [
        new("Tunnel", 5, 1.5f, 6, 3.5f, "A narrow passage, just high enough to walk through."),
        new("Cave", 8, 2.5f, 7.5f, 4.5f, "A cave to explore or hide a base in."),
        new("Cavern", 14, 3.5f, 8, 5.5f, "A wide hall under the rocks."),
    ];

    /// <summary>The action as the panel lists it (also used in the status line after applying).</summary>
    public static string Label(Action a) => a switch
    {
        Action.Flatten => "Flatten to height",
        Action.Ramp => "Ramp (start → end)",
        Action.Raise => "Raise by",
        Action.Lower => "Lower by",
        Action.Smooth => "Smooth",
        Action.River => "River / canal (water)",
        Action.Cave => "Cave (dig and roof)",
        Action.PaintDirt => "Paint dirt",
        Action.PaintPaved => "Paint paved",
        Action.PaintCultivated => "Paint cultivated",
        _ => "Clear paint",
    };

    /// <summary>The action Apply does.</summary>
    public Action Act { get; set; } = Action.Flatten;
    /// <summary>The full width of the path in metres.</summary>
    public float Width { get; set; } = 8;
    /// <summary>The soft edge in metres: how far beyond the width the action fades out (0: a hard edge).</summary>
    public float Soft { get; set; } = 4;
    /// <summary>Flatten: the height to flatten to, in metres.</summary>
    public float Height { get; set; } = 32;
    /// <summary>Raise and Lower: by how much, in metres.</summary>
    public float Amount { get; set; } = 2;
    /// <summary>Ramp: the height at the line's first point, in metres.</summary>
    public float Start { get; set; } = 32;
    /// <summary>Ramp: the height at the line's last point, in metres.</summary>
    public float End { get; set; } = 32;
    /// <summary>River: how far below the water level the bed goes in the middle, in metres.</summary>
    public float Depth { get; set; } = 2;
    /// <summary>
    /// Whether the line is a smooth curve through the points (with 3 or more points) instead of straight stretches.
    /// </summary>
    public bool Curved { get; set; } = true;
    /// <summary>Cave: depth below the ground in the middle, in metres.</summary>
    public float CaveDepth { get; set; } = 7.5f;
    /// <summary>Cave: headroom inside, in metres.</summary>
    public float Headroom { get; set; } = 4.5f;
    /// <summary>Cave: the roof's boulders.</summary>
    public CaveRock Rock { get; set; }
    /// <summary>Cave: the seed that turns and sizes the roof boulders.</summary>
    public int Seed { get; set; } = Random.Shared.Next();
    /// <summary>
    /// Natural look: the width varies along the line, the edges are ragged and the surface gets bumps (from the brush
    /// noise).
    /// </summary>
    public bool Natural { get; set; }
    /// <summary>
    /// Whether the ramp's ends were typed in or picked with Alt; until then they follow the ground at the line's ends.
    /// </summary>
    public bool RampEdited { get; set; }

    /// <summary>
    /// The points the user placed, in grid coordinates of the block. Changed under its own lock: the drawing thread
    /// reads it.
    /// </summary>
    public List<Vector2> Points { get; } = [];
    /// <summary>Something about the line or the values changed (to redraw and refill the panel).</summary>
    public event System.Action? Changed;
    /// <summary>Raises <see cref="Changed"/>.</summary>
    public void Notify() => Changed?.Invoke();

    /// <summary>
    /// The dense line (about 1 m apart): a Catmull-Rom curve through the points, or straight stretches; Seg is the
    /// index of the point each stretch starts from.
    /// </summary>
    public List<(Vector2 P, int Seg)> Curve()
    {
        var p = Points;
        var outp = new List<(Vector2, int)>();
        if (p.Count < 2)
        {
            outp.AddRange(p.Select(q => (q, 0)));
            return outp;
        }
        bool smooth = Curved && p.Count >= 3;
        for (int i = 0; i < p.Count - 1; i++)
        {
            Vector2 p0 = p[Math.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Math.Min(p.Count - 1, i + 2)];
            int steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(p1, p2)));
            for (int k = 0; k < steps; k++)
            {
                float t = k / (float)steps, t2 = t * t, t3 = t2 * t;
                if (!smooth)
                {
                    outp.Add((p1 + (p2 - p1) * t, i));
                    continue;
                }
                Vector2 Cr(Vector2 a, Vector2 b, Vector2 c, Vector2 d) => 0.5f * (2 * b + (-a + c) * t + (2 * a - 5 * b + 4 * c - d) * t2 + (-a + 3 * b - 3 * c + d) * t3);
                outp.Add((Cr(p0, p1, p2, p3), i));
            }
        }
        outp.Add((p[^1], p.Count - 2));
        return outp;
    }

    /// <summary>The length of a dense line, in metres.</summary>
    public static float Length(List<(Vector2 P, int Seg)> curve)
    {
        float l = 0;
        for (int i = 1; i < curve.Count; i++)
        {
            l += Vector2.Distance(curve[i].P, curve[i - 1].P);
        }
        return l;
    }

    /// <summary>
    /// A cave along the line, worked out before the ground changes: for each point of the dense line, how far along it
    /// is, how deep the cave is (CaveDepth in the middle, an entrance slope at each end) and its floor.
    /// </summary>
    /// <param name="Curve">The dense line, as <see cref="PathTool.Curve"/> gives it.</param>
    /// <param name="Along">How far along the line each point is, in metres.</param>
    /// <param name="Depth">How deep the cave is below the ground's lie there, in metres.</param>
    /// <param name="Floor">The floor's height: the ground averaged over 6 m each way along the line (so bumps do not
    /// show in the floor), less the depth.</param>
    public sealed record CavePlan(List<(Vector2 P, int Seg)> Curve, float[] Along, float[] Depth, float[] Floor);

    /// <summary>
    /// A test of whether a point (grid metres) is within <paramref name="reach"/> of a point of the cave's line
    /// deeper than half a metre. The deep points are put once in square cells a metre wider than the reach, so a test
    /// looks only at the points of its own cell and the eight around it.
    /// </summary>
    /// <param name="cave">The cave worked out before the ground changes.</param>
    /// <param name="reach">How far from the line, metres.</param>
    internal static Func<Vector2, bool> NearDeep(CavePlan cave, float reach)
    {
        float cell = MathF.Max(reach, 0) + 1;
        var cells = new Dictionary<(int, int), List<Vector2>>();
        for (int k = 0; k < cave.Curve.Count; k++)
        {
            if (cave.Depth[k] > 0.5f)
            {
                Vector2 p = cave.Curve[k].P;
                var key = ((int)MathF.Floor(p.X / cell), (int)MathF.Floor(p.Y / cell));
                if (!cells.TryGetValue(key, out var list))
                {
                    cells[key] = list = [];
                }
                list.Add(p);
            }
        }
        return at =>
        {
            int cx = (int)MathF.Floor(at.X / cell), cz = (int)MathF.Floor(at.Y / cell);
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (cells.TryGetValue((cx + dx, cz + dz), out var list) && list.Exists(p => Vector2.Distance(p, at) <= reach))
                    {
                        return true;
                    }
                }
            }
            return false;
        };
    }

    /// <summary>Works out the cave along the current line from the ground as it is now.</summary>
    public CavePlan PlanCave(Ground g)
    {
        var curve = Curve();
        int n = curve.Count;
        float[] along = new float[n];
        for (int i = 1; i < n; i++)
        {
            along[i] = along[i - 1] + Vector2.Distance(curve[i].P, curve[i - 1].P);
        }
        float[] surface = [.. curve.Select(c => g.HeightOf(Math.Clamp((int)MathF.Round(c.P.Y), 0, g.H - 1) * g.W + Math.Clamp((int)MathF.Round(c.P.X), 0, g.W - 1)))];
        float total = n > 0 ? along[^1] : 0, entrance = MathF.Max(8, CaveDepth * 2.5f);
        float[] depth = new float[n];
        float[] floor = new float[n];
        for (int i = 0; i < n; i++)
        {
            float sum = 0;
            int k = 0;
            for (int j = 0; j < n; j++)
            {
                if (MathF.Abs(along[j] - along[i]) <= 6)
                {
                    sum += surface[j];
                    k++;
                }
            }
            float t = Math.Clamp(MathF.Min(along[i], total - along[i]) / entrance, 0, 1);
            depth[i] = CaveDepth * t * t * (3 - 2 * t);
            floor[i] = sum / k - depth[i];
        }
        return new CavePlan(curve, along, depth, floor);
    }

    /// <summary>
    /// Applies the action along the line. Returns the points changed (grid indices), the rectangle around them (one
    /// point wider each way), and whether the game's limit stopped some. Does nothing with fewer than two points.
    /// </summary>
    /// <param name="g">The ground to change.</param>
    /// <param name="b">The brush, for its noise (Natural look).</param>
    /// <param name="water">The water level, for River.</param>
    /// <param name="mask">Per grid point weight from 0 to 1 (0 leaves it alone); null changes every point.</param>
    /// <param name="cave">Cave: the cave worked out before the ground changes; worked out here when not given.</param>
    public (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, bool Clamped) Apply(Ground g, Brush b, float water, Func<int, float>? mask = null, CavePlan? cave = null)
    {
        if (Act == Action.Cave)
        {
            cave ??= PlanCave(g);
        }
        // A cave follows the line it was planned on: its floors are by that curve's points.
        var curve = cave?.Curve ?? Curve();
        var touched = new List<int>();
        bool clamped = false;
        if (curve.Count < 2)
        {
            return (touched, (0, 0, 0, 0), false);
        }
        float half = Width / 2, soft = Soft, total = Math.Max(Length(curve), 1e-6f);
        // The farthest a changed point can be from the line: the widest a natural path gets, its soft edge (or the
        // half metre a hard edge shares), and the most its ragged edge reaches out; a metre more against rounding.
        // The box looked through is as wide, so the ragged edge is never cut straight.
        float edge = soft > 0 ? soft : 0.5f;
        float cutoff = (Natural ? MathF.Abs(half) * (1 + 0.45f * Noise.FbmBound) + edge + MathF.Max(soft, 2) * 0.6f * Noise.FbmBound : half + edge) + 1;
        float[] cum = new float[curve.Count];
        for (int i = 1; i < curve.Count; i++)
        {
            cum[i] = cum[i - 1] + Vector2.Distance(curve[i].P, curve[i - 1].P);
        }
        int x0 = Math.Max(1, (int)MathF.Floor(curve.Min(c => c.P.X) - cutoff)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(curve.Max(c => c.P.X) + cutoff));
        int z0 = Math.Max(1, (int)MathF.Floor(curve.Min(c => c.P.Y) - cutoff)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(curve.Max(c => c.P.Y) + cutoff));
        float ox = g.X0 * 64 - 32, oz = g.Z0 * 64 - 32;
        float WorldNoise(float gx, float gz, float scale = 1) => b.Noise.Fbm((ox + gx) / (b.NoiseSize * scale), (oz + gz) / (b.NoiseSize * scale));
        // Smoothing reads the heights from before.
        float[]? before = null;
        if (Act == Action.Smooth)
        {
            before = new float[g.W * g.H];
            for (int i = 0; i < before.Length; i++)
            {
                before[i] = g.HeightOf(i);
            }
        }
        float[]? colour = Act switch
        {
            Action.PaintDirt => Brush.PaintOf(BrushTool.PaintDirt),
            Action.PaintPaved => Brush.PaintOf(BrushTool.PaintPaved),
            Action.PaintCultivated => Brush.PaintOf(BrushTool.PaintCultivated),
            Action.PaintClear => Brush.PaintOf(BrushTool.PaintClear),
            _ => null,
        };
        var near = NearestOnLine(curve, x0, z0, x1, z1, cutoff);
        int nw = x1 - x0 + 1;
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                if (g.Locked(gx, gz))
                {
                    continue;
                }
                // The nearest point of the line: distance, and how far along it. None within the cutoff: too far
                // to change.
                int ni = (gz - z0) * nw + (gx - x0);
                int seg = near.Seg[ni];
                if (seg == 0)
                {
                    continue;
                }
                float best = near.Dist[ni];
                float st = near.T[ni];
                float along = cum[seg - 1] + st * MathF.Sqrt(Segment(curve, seg).L2), caveFloor = 0;
                if (cave != null)
                {
                    caveFloor = cave.Floor[seg - 1] + (cave.Floor[seg] - cave.Floor[seg - 1]) * st;
                }
                float dEff = best, halfEff = half, bump = 0;
                if (Natural)
                {
                    // Width varies along the path, edges are ragged, and the surface gets bumps.
                    halfEff = half * (1 + 0.45f * b.Noise.Fbm(along / (b.NoiseSize * 1.5f) + 31.7f, 4.2f));
                    dEff = best + MathF.Max(soft, 2) * 0.6f * WorldNoise(gx, gz, 0.6f);
                    bump = b.NoiseAmp * WorldNoise(gx + 500, gz - 300);
                }
                // Without a soft edge, the points the edge crosses (within half a metre) get a share, so the
                // edge follows the line straight instead of stepping from point to point.
                if (dEff > halfEff + (soft > 0 ? soft : 0.5f))
                {
                    continue;
                }
                float f = soft > 0 ? (dEff <= halfEff ? 1 : 1 - (dEff - halfEff) / soft) : Math.Clamp(halfEff + 0.5f - dEff, 0, 1);
                int p = gz * g.W + gx;
                float w = (soft > 0 ? f * f * (3 - 2 * f) : f) * (mask?.Invoke(p) ?? 1);
                if (w <= 0)
                {
                    continue;
                }
                float h = g.HeightOf(p);
                touched.Add(p);
                switch (Act)
                {
                    case Action.Raise:
                        clamped |= g.SetHeight(p, h + (Amount + bump) * w);
                        break;
                    case Action.Lower:
                        clamped |= g.SetHeight(p, h - (Amount + bump) * w);
                        break;
                    case Action.Flatten:
                        clamped |= g.SetHeight(p, h + (Height + bump - h) * w);
                        break;
                    case Action.Ramp:
                    {
                        float target = Start + (End - Start) * (along / total) + bump;
                        clamped |= g.SetHeight(p, h + (target - h) * w);
                        break;
                    }
                    case Action.River:
                    {
                        // A U-shaped bed: Depth below sea level in the middle, at the waterline at the path's
                        // width, then the soft edge blends it into the banks. It only digs.
                        float r = MathF.Min(dEff, halfEff) / halfEff;
                        float bed = water - Depth * MathF.Max(0, 1 - r * r) + bump * 0.3f;
                        if (bed < h)
                        {
                            clamped |= g.SetHeight(p, h + (bed - h) * w);
                        }
                        break;
                    }
                    case Action.Cave:
                    {
                        // The floor across the cave's width; the soft edge makes its walls. It only digs, and keeps
                        // within the game's ±8 m unless No limit is on (bumps above the ground's lie would reach it).
                        float floor = caveFloor + bump * 0.3f;
                        if (!g.NoLimit)
                        {
                            floor = MathF.Max(floor, g.Original(p) - Editing.EditStore.MaxLevel + 0.1f);
                        }
                        if (floor < h)
                        {
                            clamped |= g.SetHeight(p, h + (floor - h) * w);
                        }
                        break;
                    }
                    case Action.Smooth:
                    {
                        float sum = 0;
                        int n = 0;
                        for (int dz = -2; dz <= 2; dz++)
                        {
                            for (int dx = -2; dx <= 2; dx++)
                            {
                                // Inside the area on both axes (a flat index would wrap into the next row).
                                int x = gx + dx, z = gz + dz;
                                if (x >= 0 && x < g.W && z >= 0 && z < g.H)
                                {
                                    sum += before![z * g.W + x];
                                    n++;
                                }
                            }
                        }
                        clamped |= g.SetHeight(p, h + (sum / n - h) * w);
                        break;
                    }
                    default:
                        if (g.PMod[p] == 0)
                        {
                            g.Paint[p * 4] = g.Paint[p * 4 + 1] = g.Paint[p * 4 + 2] = 0;
                            g.Paint[p * 4 + 3] = 1;
                            g.PMod[p] = 1;
                        }
                        for (int c = 0; c < 4; c++)
                        {
                            g.Paint[p * 4 + c] += (colour![c] - g.Paint[p * 4 + c]) * w;
                        }
                        break;
                }
            }
        }
        return (touched, (x0 - 1, z0 - 1, x1 + 1, z1 + 1), clamped);
    }

    /// <summary>
    /// The nearest points of the line to the grid points of a rectangle, row by row (x fastest).
    /// </summary>
    /// <param name="Dist">Distance to the line.</param>
    /// <param name="Seg">The nearest segment, by the index of its end point in the line; 0 where no segment is within
    /// reach.</param>
    /// <param name="T">How far along that segment, 0 to 1.</param>
    internal sealed record Nearest(float[] Dist, int[] Seg, float[] T);

    /// <summary>
    /// A segment of the dense line: its start, its run to the end, and its length squared (at least 1e-9, so a point
    /// segment still projects).
    /// </summary>
    /// <param name="curve">The dense line.</param>
    /// <param name="i">The index of the segment's end point (1 or more).</param>
    private static (Vector2 A, Vector2 D, float L2) Segment(List<(Vector2 P, int Seg)> curve, int i)
    {
        Vector2 a = curve[i - 1].P, d = curve[i].P - a;
        return (a, d, MathF.Max(d.LengthSquared(), 1e-9f));
    }

    /// <summary>
    /// For each grid point of the rectangle <paramref name="x0"/>..<paramref name="x1"/> by
    /// <paramref name="z0"/>..<paramref name="z1"/>, the nearest segment of the line within
    /// <paramref name="reach"/>. Each segment visits only the points of its box widened by the reach, in the line's
    /// order, so a point gets what testing every segment would give (ties to the earlier one) without the cost of
    /// every point times every segment.
    /// </summary>
    /// <param name="curve">The dense line (<see cref="Curve"/>).</param>
    /// <param name="x0">First column.</param>
    /// <param name="z0">First row.</param>
    /// <param name="x1">Last column.</param>
    /// <param name="z1">Last row.</param>
    /// <param name="reach">How far from a segment its points are looked at, metres.</param>
    internal static Nearest NearestOnLine(List<(Vector2 P, int Seg)> curve, int x0, int z0, int x1, int z1, float reach)
    {
        int w = Math.Max(0, x1 - x0 + 1), h = Math.Max(0, z1 - z0 + 1);
        var near = new Nearest(new float[w * h], new int[w * h], new float[w * h]);
        Array.Fill(near.Dist, float.MaxValue);
        for (int i = 1; i < curve.Count; i++)
        {
            var (a, d, l2) = Segment(curve, i);
            Vector2 b = curve[i].P;
            int sx0 = Math.Max(x0, (int)MathF.Floor(MathF.Min(a.X, b.X) - reach)), sx1 = Math.Min(x1, (int)MathF.Ceiling(MathF.Max(a.X, b.X) + reach));
            int sz0 = Math.Max(z0, (int)MathF.Floor(MathF.Min(a.Y, b.Y) - reach)), sz1 = Math.Min(z1, (int)MathF.Ceiling(MathF.Max(a.Y, b.Y) + reach));
            for (int gz = sz0; gz <= sz1; gz++)
            {
                for (int gx = sx0; gx <= sx1; gx++)
                {
                    float t = Math.Clamp(((gx - a.X) * d.X + (gz - a.Y) * d.Y) / l2, 0, 1);
                    float dist = Vector2.Distance(new Vector2(gx, gz), a + d * t);
                    int k = (gz - z0) * w + (gx - x0);
                    if (dist <= reach && dist < near.Dist[k])
                    {
                        near.Dist[k] = dist;
                        near.Seg[k] = i;
                        near.T[k] = t;
                    }
                }
            }
        }
        return near;
    }

    // ---- The mouse: click points along the route, or hold and drag; drag a point to move it, drag the
    // line to add a point there, Ctrl + click a point to remove it; Alt + click picks the height (Alt +
    // Shift: the ramp's end). screen: where a grid point is on the view (null behind the camera).
    /// <summary>The point being dragged (index into <see cref="Points"/>), or null.</summary>
    private int? _drag;

    /// <summary>A drag is drawing the line now (the view then leaves out the cursor's preview).</summary>
    public bool Drawing { get; private set; }

    /// <summary>
    /// A mouse press: picks a height with Alt, removes a point with Ctrl, starts dragging a point, inserts a point on
    /// the line, or starts a new point and drawing.
    /// </summary>
    /// <param name="hit">The grid point under the mouse; null off the ground.</param>
    /// <param name="at">The mouse on the view, in pixels.</param>
    /// <param name="screen">Where a grid point is on the view (null behind the camera).</param>
    /// <param name="ctrl">Ctrl held: remove the point under the mouse.</param>
    /// <param name="alt">Alt held: pick the height under the mouse instead.</param>
    /// <param name="shift">With Alt on a Ramp: pick the ramp's end instead of its start.</param>
    /// <param name="heightAt">The ground's height at a grid point.</param>
    public void Down(Vector2? hit, Point at, Func<Vector2, Point?> screen, bool ctrl, bool alt, bool shift, Func<Vector2, float> heightAt)
    {
        if (alt)
        {
            if (hit is { } a)
            {
                float h = MathF.Round(heightAt(a), 1);
                if (Act == Action.Ramp)
                {
                    if (shift)
                    {
                        End = h;
                    }
                    else
                    {
                        Start = h;
                    }

                    RampEdited = true;
                }
                else
                {
                    Height = h;
                }
                Notify();
            }
            return;
        }
        int pt = PointAt(at, screen);
        if (pt >= 0)
        {
            if (ctrl)
            {
                lock (Points) { Points.RemoveAt(pt); }
                Notify();
                return;
            }
            _drag = pt;
            return;
        }
        if (hit is not { } h2)
        {
            return;
        }
        int seg = Points.Count >= 2 ? SegmentAt(at, screen) : -1;
        if (seg >= 0)
        {
            lock (Points) { Points.Insert(seg + 1, h2); }
            _drag = seg + 1;
            Notify();
            return;
        }
        lock (Points) { Points.Add(h2); }
        Drawing = true;
        Notify();
    }

    /// <summary>
    /// The mouse moved: moves the dragged point, or while drawing adds a point once the mouse is 2 m from the last one.
    /// </summary>
    public void Moved(Vector2? hit)
    {
        if (hit is not { } h)
        {
            return;
        }
        if (_drag is int i && i < Points.Count)
        {
            lock (Points) { Points[i] = h; }
            Notify();
        }
        else if (Drawing && (Points.Count == 0 || Vector2.Distance(Points[^1], h) >= 2))
        {
            lock (Points) { Points.Add(h); }
            Notify();
        }
    }

    /// <summary>The mouse was let go: ends any drag or drawing.</summary>
    public void Up()
    {
        _drag = null;
        Drawing = false;
    }

    /// <summary>Removes the line, and lets the ramp ends follow the ground again.</summary>
    public void Clear()
    {
        lock (Points) { Points.Clear(); }
        RampEdited = false;
        Notify();
    }

    /// <summary>Removes the last point (Backspace).</summary>
    public void RemoveLast()
    {
        if (Points.Count > 0)
        {
            lock (Points) { Points.RemoveAt(Points.Count - 1); }
            Notify();
        }
    }

    /// <summary>The point within 10 px of the mouse, or -1.</summary>
    public int PointAt(Point at, Func<Vector2, Point?> screen)
    {
        int best = -1;
        double bd = 10;
        for (int i = 0; i < Points.Count; i++)
        {
            if (screen(Points[i]) is { } s && Point.Distance(s, at) < bd)
            {
                bd = Point.Distance(s, at);
                best = i;
            }
        }
        return best;
    }

    /// <summary>
    /// The stretch of the line within 8 px of the mouse: the index of the point it starts from, or -1.
    /// </summary>
    public int SegmentAt(Point at, Func<Vector2, Point?> screen)
    {
        var dense = Curve();
        int best = -1;
        double bd = 8;
        for (int i = 1; i < dense.Count; i++)
        {
            if (screen(dense[i - 1].P) is not { } a || screen(dense[i].P) is not { } b)
            {
                continue;
            }
            double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
            if (l2 < 1e-9)
            {
                l2 = 1e-9;
            }
            double t = Math.Clamp(((at.X - a.X) * dx + (at.Y - a.Y) * dy) / l2, 0, 1);
            double d = Point.Distance(at, new Point(a.X + dx * t, a.Y + dy * t));
            if (d < bd)
            {
                bd = d;
                best = dense[i - 1].Seg;
            }
        }
        return best;
    }
}
