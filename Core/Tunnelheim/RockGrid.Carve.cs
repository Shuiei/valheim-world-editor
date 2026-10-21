using System.Numerics;

namespace TerrainEditor.Tunnelheim;

public sealed partial class RockGrid
{
    /// <summary>Whether rock may be opened at a height: not deeper than <see cref="MaxDepth"/> below the seed ground.</summary>
    private static bool MayOpen(float y, float seed) => y >= seed - (double)MaxDepth;

    /// <summary>Whether a grid point is one an edit may write: in the clip column, or anywhere without one.</summary>
    private static bool InClip(int gx, int gz, (int X, int Z)? clip) => clip is not { } c || ColumnOf(gx, gz) == c;

    /// <summary>
    /// Carves a hole (VoxelWorld.Carve): every sample inside the edit's shape, down to <see cref="MaxDepth"/> below the
    /// seed ground, takes the shape's distance where that is lower. The chunks over the whole shape are made first,
    /// voxelising the columns it reaches even outside the clip.
    /// </summary>
    /// <param name="op">A carving edit (<see cref="DigOp.Carves"/>).</param>
    /// <param name="clip">The only column written, or null for all.</param>
    /// <param name="changed">Collects the columns whose rock changed, when given.</param>
    /// <returns>Whether a sample changed.</returns>
    public bool Carve(in DigOp op, (int X, int Z)? clip = null, ISet<(int X, int Z)>? changed = null)
    {
        var (centre, radius) = DigShapes.Bounds(op);
        int x0 = RockMath.FloorStep(centre.X - radius), x1 = RockMath.CeilStep(centre.X + radius);
        int y0 = RockMath.FloorStep(centre.Y - radius), y1 = RockMath.CeilStep(centre.Y + radius);
        int z0 = RockMath.FloorStep(centre.Z - radius), z1 = RockMath.CeilStep(centre.Z + radius);
        EnsureBox(x0, y0, z0, x1, y1, z1);
        bool chip = op.Kind is DigKind.Chip or DigKind.ChipFloor;
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
                    if (!MayOpen(p.Y, seed))
                    {
                        continue;
                    }
                    var chunk = _chunks[(FloorDiv(gx, Samples), FloorDiv(gy, Samples), FloorDiv(gz, Samples))];
                    int i = IndexIn(gx, gy, gz);
                    float old = chunk[i];
                    float d;
                    if (chip)
                    {
                        float shape = DigShapes.Chip(op, p);
                        if (shape - (double)0.1f - 0.001f >= old)
                        {
                            continue;
                        }
                        d = DigShapes.Finish(op, p, shape);
                    }
                    else
                    {
                        d = DigShapes.Distance(op, p);
                    }
                    if (!(d >= old))
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
}
