using System.Numerics;

namespace Tunnelheim.WorldEditor;

public sealed partial class RockGrid
{
    /// <summary>
    /// Added rock held to the building height (VoxelWorld.CapAdded): never above <see cref="MaxBuildHeight"/> over the
    /// seed ground, never below what was there.
    /// </summary>
    private float CapAdded(float d, float existing, float y, float seed) =>
        RockMath.Max(existing, RockMath.Min(d, (float)((double)seed + MaxBuildHeight - y)));

    /// <summary>
    /// Adds a ball of rock (VoxelWorld.ApplyFill, the hoe's Add Earth): every sample inside it takes the ball's depth
    /// where that is higher, held to the building height.
    /// </summary>
    /// <param name="op">A <see cref="DigKind.Fill"/> edit.</param>
    /// <param name="clip">The only column written, or null for all.</param>
    /// <param name="changed">Collects the columns whose rock changed, when given.</param>
    /// <returns>Whether a sample changed.</returns>
    public bool Fill(in DigOp op, (int X, int Z)? clip = null, ISet<(int X, int Z)>? changed = null)
    {
        var c = op.Position;
        float r = op.Value;
        int x0 = RockMath.FloorStep(c.X - r), x1 = RockMath.CeilStep(c.X + r);
        int y0 = RockMath.FloorStep(c.Y - r), y1 = RockMath.CeilStep(c.Y + r);
        int z0 = RockMath.FloorStep(c.Z - r), z1 = RockMath.CeilStep(c.Z + r);
        EnsureBox(x0, y0, z0, x1, y1, z1);
        bool any = false;
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                if (!InClip(gx, gz, clip))
                {
                    continue;
                }
                float seed = SeedAt(gx, gz);
                for (int gy = y0; gy <= y1; gy++)
                {
                    var p = new Vector3(gx, gy, gz) * Step;
                    float d = r - RockMath.Length(p - c);
                    var chunk = _chunks[(FloorDiv(gx, Samples), FloorDiv(gy, Samples), FloorDiv(gz, Samples))];
                    int i = IndexIn(gx, gy, gz);
                    float old = chunk[i];
                    d = CapAdded(d, old, p.Y, seed);
                    if (!(d <= old))
                    {
                        chunk[i] = d;
                        any = true;
                        changed?.Add(ColumnOf(gx, gz));
                    }
                }
            }
        }
        return any;
    }

    /// <summary>
    /// Smooths rock towards a plane (VoxelWorld.ApplyPlane: Smooth Surface, Level Wall, Level Ceiling): within the
    /// edit's radius and 1.5 m of the plane, each sample moves towards its signed distance from the plane (rock behind
    /// the aimed normal), fully near the middle and fading out towards the edges.
    /// </summary>
    /// <param name="op">A <see cref="DigKind.Plane"/> edit.</param>
    /// <param name="clip">The only column written, or null for all.</param>
    /// <param name="changed">Collects the columns whose rock changed, when given.</param>
    /// <returns>Whether a sample changed.</returns>
    public bool Plane(in DigOp op, (int X, int Z)? clip = null, ISet<(int X, int Z)>? changed = null)
    {
        var c = op.Position;
        var n = DigShapes.Direction(op);
        float r = op.Value;
        float reach = (float)(r + 1.0 + 0.5);
        int x0 = RockMath.FloorStep(c.X - reach), x1 = RockMath.CeilStep(c.X + reach);
        int y0 = RockMath.FloorStep(c.Y - reach), y1 = RockMath.CeilStep(c.Y + reach);
        int z0 = RockMath.FloorStep(c.Z - reach), z1 = RockMath.CeilStep(c.Z + reach);
        EnsureBox(x0, y0, z0, x1, y1, z1);
        bool any = false;
        for (int gz = z0; gz <= z1; gz++)
        {
            for (int gx = x0; gx <= x1; gx++)
            {
                if (!InClip(gx, gz, clip))
                {
                    continue;
                }
                float seed = SeedAt(gx, gz);
                for (int gy = y0; gy <= y1; gy++)
                {
                    any |= PlaneSample(c, n, r, seed, gx, gy, gz, changed);
                }
            }
        }
        return any;
    }

    private bool PlaneSample(Vector3 c, Vector3 n, float r, float seed, int gx, int gy, int gz, ISet<(int X, int Z)>? changed)
    {
        var p = new Vector3(gx, gy, gz) * Step;
        var v = p - c;
        float t = RockMath.Dot(v, n);
        float off = RockMath.Length(v - n * t);
        if (off > r || FloatMath.Abs(t) > 1.5f || !MayOpen(p.Y, seed))
        {
            return false;
        }
        float w = (float)((1.0 - RockMath.SmoothStep01(RockMath.InverseLerp(r * 0.6f, r, off)))
            * (1.0 - RockMath.SmoothStep01(RockMath.InverseLerp(1f, 1.5f, FloatMath.Abs(t)))));
        var chunk = _chunks[(FloorDiv(gx, Samples), FloorDiv(gy, Samples), FloorDiv(gz, Samples))];
        int i = IndexIn(gx, gy, gz);
        float old = chunk[i];
        float nv = RockMath.Lerp(old, -t, w);
        if (nv > old)
        {
            nv = CapAdded(nv, old, p.Y, seed);
        }
        if (FloatMath.Abs(nv - old) < 0.0001f)
        {
            return false;
        }
        chunk[i] = nv;
        changed?.Add(ColumnOf(gx, gz));
        return true;
    }
}
