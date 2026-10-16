using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Area tool's selection, like the web editor's (editor/area.js): a box (drag) or a polygon (click
/// points; double-click or Enter closes it) of grid points, with a soft edge inwards; and the ground
/// actions done inside it. Points are grid points of the block (x east, z north, one per metre).
/// </summary>
public sealed class AreaTool
{
    /// <summary>A box dragged on the ground (true) or a polygon clicked point by point (false).</summary>
    public bool Box { get; set; } = true;
    /// <summary>The soft edge in metres: how far inwards from the outline the actions fade in (0: a hard edge).</summary>
    public float Soft { get; set; } = 3;
    /// <summary>
    /// The points clicked: for a box its two opposite corners, for a polygon its corners in order. Lock it to
    /// read or change it from another thread.
    /// </summary>
    public List<Vector2> Points { get; } = [];
    /// <summary>Whether the polygon is closed: the next click then starts a new one.</summary>
    public bool Closed { get; private set; }
    /// <summary>Whether a box is being dragged (the mouse button held since <see cref="Down"/>).</summary>
    public bool Dragging { get; private set; }
    /// <summary>The selection changed.</summary>
    public event Action? Changed;
    /// <summary>Raises <see cref="Changed"/>.</summary>
    public void Notify() => Changed?.Invoke();

    /// <summary>The kinds of objects the object actions work on.</summary>
    public HashSet<ObjectKind> Kinds { get; } = [ObjectKind.Trees, ObjectKind.Rocks, ObjectKind.Bushes, ObjectKind.Pickables];

    /// <summary>The selection's outline, or null while there is none.</summary>
    public List<Vector2>? Polygon()
    {
        lock (Points)
        {
            if (Box)
            {
                if (Points.Count < 2)
                {
                    return null;
                }
                Vector2 a = Points[0], b = Points[1];
                return [a, new(b.X, a.Y), b, new(a.X, b.Y)];
            }
            return Points.Count >= 3 ? [.. Points] : null;
        }
    }

    /// <summary>Drops the selection.</summary>
    public void Clear()
    {
        lock (Points)
        {
            Points.Clear();
        }
        Closed = false;
        Dragging = false;
        Notify();
    }

    // ---- The mouse and keys.
    /// <summary>
    /// A mouse button pressed on the ground: a box starts there; a polygon gets a point (a new polygon after a
    /// closed one), or closes on a double-click.
    /// </summary>
    /// <param name="hit">The grid point under the mouse, null when off the ground.</param>
    /// <param name="clicks">The click count (2 for a double-click).</param>
    public void Down(Vector2? hit, int clicks)
    {
        if (hit is not { } h)
        {
            return;
        }
        if (Box)
        {
            lock (Points)
            {
                Points.Clear();
                Points.Add(h);
                Points.Add(h);
            }
            Dragging = true;
        }
        else if (clicks >= 2 && Points.Count >= 3)
        {
            // The double-click's first click already added its point.
            Closed = true;
        }
        else
        {
            lock (Points)
            {
                if (Closed)
                {
                    Points.Clear();
                    Closed = false;
                }
                Points.Add(h);
            }
        }
        Notify();
    }

    /// <summary>The mouse moved: a box being dragged follows it to its far corner.</summary>
    public void Moved(Vector2? hit)
    {
        if (Dragging && hit is { } h)
        {
            lock (Points)
            {
                Points[1] = h;
            }
            Notify();
        }
    }

    /// <summary>
    /// The mouse button released: a box ends there, or is dropped if less than a metre across either way (a line has
    /// no area to work on).
    /// </summary>
    public void Up()
    {
        if (!Dragging)
        {
            return;
        }
        Dragging = false;
        if (MathF.Abs(Points[1].X - Points[0].X) < 1 || MathF.Abs(Points[1].Y - Points[0].Y) < 1)
        {
            Clear();
        }
        else
        {
            Notify();
        }
    }

    /// <summary>Enter on an open polygon closes it (true); otherwise Enter applies the action.</summary>
    public bool CloseWithEnter()
    {
        if (!Box && !Closed && Points.Count >= 3)
        {
            Closed = true;
            Notify();
            return true;
        }
        return false;
    }

    /// <summary>Backspace: removes the polygon's last point.</summary>
    public void RemoveLast()
    {
        if (!Box && Points.Count > 0)
        {
            lock (Points)
            {
                Points.RemoveAt(Points.Count - 1);
            }
            Notify();
        }
    }

    // ---- Geometry.
    /// <summary>Whether a point is inside an outline.</summary>
    public static bool Inside(List<Vector2> poly, float x, float z) => SelectTool.Inside(poly, x, z);

    /// <summary>The distance from a point to an outline's nearest edge, in the outline's units.</summary>
    public static float EdgeDistance(List<Vector2> poly, float x, float z)
    {
        float best = float.MaxValue;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = poly[j], d = poly[i] - a;
            float l2 = MathF.Max(d.LengthSquared(), 1e-9f);
            float t = Math.Clamp(((x - a.X) * d.X + (z - a.Y) * d.Y) / l2, 0, 1);
            best = MathF.Min(best, Vector2.Distance(new Vector2(x, z), a + d * t));
        }
        return best;
    }

    /// <summary>
    /// The area <see cref="Inside"/> picks within an outline, in square units of its points (m² for grid points). An
    /// outline that crosses itself counts each part the even-odd way, as the selection does (a figure eight both
    /// loops, a crossed star not its middle), where the shoelace formula let crossed parts cancel out.
    /// </summary>
    public static float Area(List<Vector2> poly)
    {
        int n = poly.Count;
        // Between the heights where the outline turns or crosses itself, the inside's width changes linearly: the
        // width halfway up a slab gives its area exactly.
        List<double> ys = [.. poly.Select(p => (double)p.Y)];
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1)
                {
                    continue;
                }
                if (Crossing(poly[i], poly[(i + 1) % n], poly[j], poly[(j + 1) % n]) is double y)
                {
                    ys.Add(y);
                }
            }
        }
        ys.Sort();
        double area = 0;
        List<double> xs = [];
        for (int k = 0; k + 1 < ys.Count; k++)
        {
            double y0 = ys[k], y1 = ys[k + 1];
            if (y1 <= y0)
            {
                continue;
            }
            double mid = (y0 + y1) / 2;
            xs.Clear();
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.Y > mid) != (b.Y > mid))
                {
                    xs.Add(a.X + (mid - a.Y) * (b.X - a.X) / (b.Y - a.Y));
                }
            }
            xs.Sort();
            for (int i = 0; i + 1 < xs.Count; i += 2)
            {
                area += (xs[i + 1] - xs[i]) * (y1 - y0);
            }
        }
        return (float)area;
    }

    /// <summary>The height at which two edges cross, if they do (not where they only touch end to end).</summary>
    private static double? Crossing(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        double rx = b.X - a.X, ry = b.Y - a.Y, sx = d.X - c.X, sy = d.Y - c.Y;
        double den = rx * sy - ry * sx;
        if (Math.Abs(den) < 1e-12)
        {
            return null;
        }
        double t = ((c.X - a.X) * sy - (c.Y - a.Y) * sx) / den, u = ((c.X - a.X) * ry - (c.Y - a.Y) * rx) / den;
        return t is > 0 and < 1 && u is > 0 and < 1 ? a.Y + t * ry : null;
    }

    /// <summary>The grid points an action works on.</summary>
    /// <param name="Poly">The selection's outline.</param>
    /// <param name="X0">The box around it (grid points, one more all round, clamped to the block): west.</param>
    /// <param name="Z0">South edge of that box.</param>
    /// <param name="X1">East edge of that box.</param>
    /// <param name="Z1">North edge of that box.</param>
    /// <param name="Cells">The grid points (index z * width + x) with their weights, 0 to 1.</param>
    public sealed record Weights(List<Vector2> Poly, int X0, int Z0, int X1, int Z1, List<(int G, float W)> Cells);

    /// <summary>
    /// The selection's own box on the grid: the whole points round its outline, clamped to the block (every point the
    /// selection weighs is in it).
    /// </summary>
    /// <param name="w">The block's width in grid points.</param>
    /// <param name="h">The block's height in grid points.</param>
    /// <returns>West, south, east and north grid points; null while there is no selection.</returns>
    public (int X0, int Z0, int X1, int Z1)? GridBox(int w, int h)
    {
        if (Polygon() is not { } poly)
        {
            return null;
        }
        return (Math.Max(0, (int)MathF.Floor(poly.Min(p => p.X))), Math.Max(0, (int)MathF.Floor(poly.Min(p => p.Y))),
            Math.Min(w - 1, (int)MathF.Ceiling(poly.Max(p => p.X))), Math.Min(h - 1, (int)MathF.Ceiling(poly.Max(p => p.Y))));
    }

    /// <summary>
    /// The grid points inside the selection and their weights (the soft edge going inwards).
    /// </summary>
    /// <param name="w">The block's width in grid points.</param>
    /// <param name="h">The block's height in grid points.</param>
    /// <param name="mask">Also times the Mask (null: not).</param>
    /// <returns>The points, or null while there is no selection.</returns>
    public Weights? WeightsIn(int w, int h, Func<int, float>? mask = null)
    {
        if (Polygon() is not { } poly || GridBox(w, h) is not { } box)
        {
            return null;
        }
        int x0 = Math.Max(0, box.X0 - 1), x1 = Math.Min(w - 1, box.X1 + 1);
        int z0 = Math.Max(0, box.Z0 - 1), z1 = Math.Min(h - 1, box.Z1 + 1);
        var cells = new List<(int, float)>();
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                bool inPoly = Inside(poly, gx, gz);
                float wt;
                if (Soft > 0)
                {
                    if (!inPoly)
                    {
                        continue;
                    }
                    float t = MathF.Min(1, EdgeDistance(poly, gx, gz) / Soft);
                    wt = t * t * (3 - 2 * t);
                }
                else
                {
                    // No soft edge: the points the outline crosses (within half a metre) get a share, so the
                    // edge follows the outline straight instead of stepping from point to point.
                    float d = EdgeDistance(poly, gx, gz);
                    wt = inPoly ? MathF.Min(1, 0.5f + d) : 0.5f - d;
                }
                wt *= mask?.Invoke(gz * w + gx) ?? 1;
                if (wt > 0)
                {
                    cells.Add((gz * w + gx, wt));
                }
            }
        }
        return new Weights(poly, x0, z0, x1, z1, cells);
    }

    // ---- Ground actions.
    /// <summary>The actions that change the ground inside the selection.</summary>
    public enum GroundAction
    {
        /// <summary>Towards one height.</summary>
        Flatten,
        /// <summary>Up by an amount.</summary>
        Raise,
        /// <summary>Down by an amount.</summary>
        Lower,
        /// <summary>Bumps evened out.</summary>
        Smooth,
        /// <summary>Natural-looking bumps (Naturalize).</summary>
        Natural,
        /// <summary>Back to the generated ground, paint removed.</summary>
        Restore,
        /// <summary>Weathered: slopes settle and rain cuts gullies.</summary>
        Erode,
        /// <summary>Painted with a ground colour.</summary>
        Paint,
    }

    /// <summary>An action's name as shown to the player.</summary>
    public static string Label(GroundAction a) => a switch
    {
        GroundAction.Natural => "Naturalize",
        GroundAction.Restore => "Restore the ground",
        _ => a.ToString(),
    };

    /// <summary>The action inside the selection, faded by the soft edge; locked points are left alone.</summary>
    /// <param name="g">The ground to change.</param>
    /// <param name="b">The brush: Naturalize's bumps and size, Erode's rest angle and random.</param>
    /// <param name="act">The action.</param>
    /// <param name="height">Flatten's height, in metres.</param>
    /// <param name="amount">Raise and Lower's amount, in metres.</param>
    /// <param name="paint">The colour (four weights, as the paint brushes put down).</param>
    /// <param name="mask">The Mask's weight per grid point (null: none).</param>
    /// <returns>The grid points changed, the box to redraw, and whether a point reached the game's ±8 m limit.</returns>
    public (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, bool Clamped) Apply(Ground g, Brush b, GroundAction act, float height, float amount, float[] paint, Func<int, float>? mask = null)
    {
        var touched = new List<int>();
        if (WeightsIn(g.W, g.H, mask) is not { } a)
        {
            return (touched, (0, 0, 0, 0), false);
        }
        if (act == GroundAction.Erode)
        {
            var set = new HashSet<int>();
            var ((x0, z0, x1, z1), eroded) = Erosion.Area(g, a, b.RestAngle, b.Random, set);
            return (set.ToList(), (x0 - 1, z0 - 1, x1 + 1, z1 + 1), eroded);
        }
        int rNat = Math.Max(2, Math.Min(8, (int)MathF.Round(b.NoiseSize / 3)));
        // The heights before the action, for Smooth and Naturalize's blur only: over the selection's box and the
        // blur's reach round it (every point a blur reads).
        int reach = act == GroundAction.Smooth ? 3 : act == GroundAction.Natural ? rNat : -1;
        int bx0 = Math.Max(0, a.X0 - reach), bz0 = Math.Max(0, a.Z0 - reach), bw = 0;
        float[] before = [];
        if (reach >= 0)
        {
            int bx1 = Math.Min(g.W - 1, a.X1 + reach), bz1 = Math.Min(g.H - 1, a.Z1 + reach);
            bw = bx1 - bx0 + 1;
            before = new float[bw * (bz1 - bz0 + 1)];
            for (int z = bz0; z <= bz1; z++)
            {
                for (int x = bx0; x <= bx1; x++)
                {
                    before[(z - bz0) * bw + x - bx0] = g.HeightOf(z * g.W + x);
                }
            }
        }
        float Blur(int p, int r)
        {
            int gx = p % g.W, gz = p / g.W, n = 0;
            float s = 0;
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = gx + dx, z = gz + dz;
                    if (x >= 0 && z >= 0 && x < g.W && z < g.H)
                    {
                        s += before[(z - bz0) * bw + x - bx0];
                        n++;
                    }
                }
            }
            return s / n;
        }
        float ox = g.OriginX, oz = g.OriginZ;
        bool clamped = false;
        foreach (var (p, w) in a.Cells)
        {
            int gx = p % g.W, gz = p / g.W;
            if (g.Locked(gx, gz))
            {
                continue;
            }
            float h = g.HeightOf(p);
            touched.Add(p);
            switch (act)
            {
                case GroundAction.Flatten:
                    clamped |= g.SetHeight(p, h + (height - h) * w);
                    break;
                case GroundAction.Raise:
                    clamped |= g.SetHeight(p, h + amount * w);
                    break;
                case GroundAction.Lower:
                    clamped |= g.SetHeight(p, h - amount * w);
                    break;
                case GroundAction.Smooth:
                    clamped |= g.SetHeight(p, h + (Blur(p, 3) - h) * w);
                    break;
                case GroundAction.Natural:
                    clamped |= g.SetHeight(p, h + (Blur(p, rNat) + b.NoiseAmp * b.Noise.Fbm((ox + gx) / b.NoiseSize, (oz + gz) / b.NoiseSize) - h) * w);
                    break;
                case GroundAction.Restore:
                    if (g.Mod[p] != 0)
                    {
                        g.Level[p] *= 1 - w;
                        g.Smooth[p] *= 1 - w;
                        if (w > 0.98f || MathF.Abs(g.Level[p]) + MathF.Abs(g.Smooth[p]) < 0.01f)
                        {
                            g.Level[p] = g.Smooth[p] = 0;
                            g.Mod[p] = 0;
                        }
                    }
                    // Also back down from a No limit lift (a mountain, a deep paste), as the Restore brush.
                    g.Lift[p] = w > 0.98f || MathF.Abs(g.Lift[p] * (1 - w)) < 0.01f ? 0 : g.Lift[p] * (1 - w);
                    if (g.PMod[p] != 0 && w > 0.5f)
                    {
                        g.PMod[p] = 0;
                    }
                    break;
                case GroundAction.Paint:
                    g.StartPaint(p);
                    for (int c = 0; c < 4; c++)
                    {
                        g.Paint[p * 4 + c] += (paint[c] - g.Paint[p * 4 + c]) * w;
                    }
                    break;
            }
        }
        return (touched, (a.X0 - 1, a.Z0 - 1, a.X1 + 1, a.Z1 + 1), clamped);
    }

    /// <summary>
    /// Cut and fill (each point stands for 1 m²): what Flatten, Raise or Lower would move within the
    /// game's ±8 m limit (and what is out of its reach; with No limit on, within the lift's ±400 m of the
    /// generated ground, as <see cref="Ground.SetHeight"/> keeps it), and how much the ground inside has
    /// been raised and dug since it was generated.
    /// </summary>
    /// <returns>The text for the panel, empty while there is no selection.</returns>
    public string Volume(Ground g, GroundAction act, float height, float amount, Func<int, float>? mask = null)
    {
        if (WeightsIn(g.W, g.H, mask) is not { } a)
        {
            return "";
        }
        static string M3(double v) => v >= 1000 ? $"{v / 1000:0.0}k m³" : $"{Math.Round(v)} m³";
        double up = 0, down = 0, cut = 0, fill = 0, outOfReach = 0, raise = 0, lower = 0;
        foreach (var (p, w) in a.Cells)
        {
            if (g.Locked(p % g.W, p / g.W))
            {
                continue;
            }
            float h = g.HeightOf(p), b = g.Original(p), d = (h - b) * w;
            if (d > 0)
            {
                up += d;
            }
            else
            {
                down -= d;
            }

            // What SetHeight can reach: No limit moves the lift (±MaxLift round the generated ground, the
            // edit within the limit kept as it is), the limit the level part (±MaxLevel round the lifted
            // ground, the smoothing part kept: it takes that much of the reach on its side).
            float smooth = g.Mod[p] != 0 ? g.Smooth[p] : 0;
            float lo = g.NoLimit ? g.Base[p] + (h - b) - EditStore.MaxLift : b - EditStore.MaxLevel + MathF.Max(0, smooth);
            float hi = g.NoLimit ? g.Base[p] + (h - b) + EditStore.MaxLift : b + EditStore.MaxLevel + MathF.Min(0, smooth);
            float want = h + (height - h) * w, got = Math.Clamp(want, lo, hi);
            if (got > h)
            {
                fill += got - h;
            }
            else
            {
                cut += h - got;
            }

            outOfReach += MathF.Abs(want - got);
            raise += MathF.Max(0, MathF.Min(hi, h + amount * w) - h);
            lower += MathF.Max(0, h - MathF.Max(lo, h - amount * w));
        }
        string first = act switch
        {
            GroundAction.Flatten => $"Flatten to {height:0.#} m: dig {M3(cut)}, fill {M3(fill)}{(outOfReach >= 1 ? $" ({M3(outOfReach)} out of reach: {(g.NoLimit ? "±400 m lift" : "±8 m")} limit)" : "")}.",
            GroundAction.Raise => $"Raise {amount:0.#} m: fill {M3(raise)}.",
            GroundAction.Lower => $"Lower {amount:0.#} m: dig {M3(lower)}.",
            _ => "",
        };
        return (first == "" ? "" : first + "\n") + $"Ground inside: {M3(up)} raised, {M3(down)} dug since generated.";
    }

    /// <summary>The average height of the ground inside (for Flatten's "avg"), null without a selection.</summary>
    public float? Average(Ground g)
    {
        if (WeightsIn(g.W, g.H) is not { Cells.Count: > 0 } a)
        {
            return null;
        }
        return a.Cells.Average(c => g.HeightOf(c.G));
    }

    /// <summary>
    /// The zones under the selection (world zone coordinates): those with a grid point inside the outline, whatever
    /// the soft edge (a point on a zone's edge belongs to its neighbours too, as <see cref="Ground.ZonesOf"/> has
    /// it); a point just outside the outline does not count.
    /// </summary>
    public List<(int X, int Z)> ZonesUnder(Ground g)
    {
        if (WeightsIn(g.W, g.H) is not { } a)
        {
            return [];
        }
        var inside = new List<int>();
        for (int gz = a.Z0; gz <= a.Z1; gz++)
        {
            for (int gx = a.X0; gx <= a.X1; gx++)
            {
                if (Inside(a.Poly, gx, gz))
                {
                    inside.Add(gz * g.W + gx);
                }
            }
        }
        return [.. g.ZonesOf(inside).Select(z => (g.X0 + z.X, g.Z0 + z.Z))];
    }
}
