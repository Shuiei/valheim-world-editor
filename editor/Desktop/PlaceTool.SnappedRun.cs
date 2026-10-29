using System.Numerics;

namespace TerrainEditor.Desktop;

// End-to-end pieces laid along a run, each at the snap points of the last.
public sealed partial class PlaceTool
{
    /// <summary>
    /// Places pieces end to end along one run, from its start; on a slope each one is a little higher or lower than the
    /// last, touching it. Adds the leftover length to <see cref="SnapGap"/>.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="pts">The run.</param>
    /// <param name="names">The kinds, used in turn.</param>
    /// <param name="k">Pieces placed so far (picks the next kind; stops at MaxShape, as does outp, layers
    /// included).</param>
    /// <param name="outp">Where the placements go.</param>
    /// <param name="mask">The mask, or null.</param>
    /// <param name="others">Pieces already there to start from (null: no snapping).</param>
    /// <returns>The pieces laid so far, this run included (masked-out spots count; extra layers do not).</returns>
    private int SnappedRun(WorldScene s, List<Vector2> pts, List<string> names, int k, List<Placement> outp, Func<int, float>? mask, List<Other>? others)
    {
        if (pts.Count < 2)
        {
            return k;
        }
        Vector2 st = pts[0];
        int seg = 0, sSeg = 0;
        float t0 = 0;
        // Started next to a piece already there (within 1 m): the run continues it, at its level.
        var start = others != null ? NearestSnap(others, st, 1) : null;
        if (start is { } st0)
        {
            st = new Vector2(st0.X, st0.Z);
        }
        int layers = Math.Clamp(Layers, 1, 20);
        float ox = s.OriginX, oz = s.OriginZ;
        for (; k < MaxShape && outp.Count < MaxShape; k++)
        {
            string name = names[k % names.Count];
            sSeg = seg;
            if (EndsOf(name) is not { } e || AlongRun(pts, st, seg, t0, e.Len) is not var (q, hseg, ht))
            {
                break;
            }
            // Turn so that the piece's start-to-end runs along st → q (Unity yaw, clockwise seen from above).
            float yaw = (MathF.Atan2(q.X - st.X, q.Y - st.Y) - e.Heading) * 180 / MathF.PI;
            float r = yaw * MathF.PI / 180, c = MathF.Cos(r), sn = MathF.Sin(r);
            float px = st.X - (e.A.X * c + e.A.Z * sn), pz = st.Y - (-e.A.X * sn + e.A.Z * c);
            if (OnRunBlock(s, px, pz, mask))
            {
                AddRunLayers(outp, name, e, px, pz, RunBottom(s, st, q, start), yaw, layers, ox, oz);
            }
            st = q;
            seg = hseg;
            t0 = ht;
        }
        AddSnapGap(pts, sSeg, st);
        return k;
    }

    /// <summary>A run's piece as one placement per layer (a tall piece stacks them), while the placements stay under MaxShape.</summary>
    internal static void AddRunLayers(List<Placement> outp, string name, Ends e, float px, float pz, float bottom, float yaw, int layers, float ox, float oz)
    {
        for (int l = 0; l < (e.H > 0.1f ? layers : 1) && outp.Count < MaxShape; l++)
        {
            outp.Add(new Placement(name, new Vector3(ox + px, bottom - e.A.Y + l * e.H, oz + pz), new Vector3(0, (yaw % 360 + 360) % 360, 0), 0, new Vector2(px, pz)));
        }
    }

    /// <summary>The run's length that a piece did not cover (the gap left after the last piece), added to <see cref="SnapGap"/>.</summary>
    internal void AddSnapGap(List<Vector2> pts, int sSeg, Vector2 st)
    {
        for (int i = sSeg; i < pts.Count - 1; i++)
        {
            SnapGap += Vector2.Distance(i == sSeg ? st : pts[i], pts[i + 1]);
        }
    }
}
