using System.Numerics;

namespace TerrainEditor.Desktop;

public sealed partial class SelectTool
{
    /// <summary>
    /// The closest snap point of the selection's pieces (where they would go) to a snap point of the pieces around,
    /// within 0.75 m.
    /// </summary>
    /// <param name="m">The move, its points around gathered.</param>
    /// <returns>The distance, the selection's point, the point around and its piece's name; null when none is that near.</returns>
    internal (float D, Vector3 M, Vector3 O, string Name)? NearestSnap(Move m)
    {
        (float D, Vector3 M, Vector3 O, string Name)? best = null;
        foreach (var it in m.Items.Where(it => !it.Linked))
        {
            var at = Target(m, it, shift: false);
            foreach (var mp in SnapsAt(it.Thing.Prefab, at, it.Thing.Rotation + new Vector3(0, m.Turn, 0)))
            {
                (float D, int K)? near = null;
                foreach (int k in PointsAround(m, mp))
                {
                    float d = Vector3.Distance(m.Others![k].P, mp);
                    if (d < 0.75f && (near == null || d < near.Value.D))
                    {
                        near = (d, k);
                    }
                }
                if (near is { } n && (best == null || n.D < best.Value.D))
                {
                    best = (n.D, mp, m.Others![n.K].P, m.Others![n.K].Name);
                }
            }
        }
        return best;
    }

    /// <summary>
    /// The indices (into the move's Others) of the snap points in the nine cells about the cell of <paramref name="mp"/>,
    /// in ascending order: the same nearest (and, between two as near, the same first) as going through the whole list.
    /// </summary>
    /// <param name="m">The move, its points gathered.</param>
    /// <param name="mp">A snap point of the selection.</param>
    internal static List<int> PointsAround(Move m, Vector3 mp)
    {
        var (mx, mz) = SnapCell(mp);
        var around = new List<int>();
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (m.OthersAt.TryGetValue((mx + dx, mz + dz), out var ks))
                {
                    around.AddRange(ks);
                }
            }
        }
        around.Sort();
        return around;
    }
}
