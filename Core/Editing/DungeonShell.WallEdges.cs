using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal static partial class DungeonShell
{
    /// <summary>The tile sides a level's doorways cross, both ways round, so no wall is built across them.</summary>
    private static HashSet<((int, int), (int, int))> DoorEdges(DungeonPlan plan, DungeonPlan.Level l)
    {
        var doorEdges = new HashSet<((int, int), (int, int))>();
        foreach (var d in plan.Doors.Where(d => d.A.Level == l.Index))
        {
            foreach (var t in new[] { d.T1, d.T2 })
            {
                var n = (t.I + d.Dir.X, t.J + d.Dir.Z);
                doorEdges.Add((t, n));
                doorEdges.Add((n, t));
            }
        }
        return doorEdges;
    }

    /// <summary>
    /// The wall edges of a level: one per tile side that borders another space or the outside, skipping doorways
    /// and the sides between two corridor tiles; sunk through the floor round a stairwell.
    /// </summary>
    private static List<Edge> WallEdges(DungeonPlan plan, DungeonPlan.Level l, float f)
    {
        var doorEdges = DoorEdges(plan, l);
        var edges = new List<Edge>();
        foreach (var (t, sp) in l.Owner)
        {
            foreach (var (x, z) in Dirs)
            {
                var n = (t.Item1 + x, t.Item2 + z);
                var other = l.Owner.GetValueOrDefault(n);
                if (other == sp || sp.Corridor && other is { Corridor: true } || doorEdges.Contains((t, n)))
                {
                    continue;
                }
                if (other != null && (n.Item1 < t.Item1 || n.Item2 < t.Item2))
                {
                    continue; // the other side makes it
                }
                float y0 = f, y1 = f + Math.Max(DungeonPlan.HeightOf(sp), DungeonPlan.HeightOf(other));
                if (InWell(sp, t) || other != null && InWell(other, n))
                {
                    y0 = f - 2; // down through the floor round a stairwell
                }
                bool alongZ = x != 0;
                float line = alongZ ? 2 * Math.Max(t.Item1, n.Item1) : 2 * Math.Max(t.Item2, n.Item2);
                edges.Add(new Edge(alongZ, line, alongZ ? t.Item2 : t.Item1, y0, y1));
            }
        }
        return edges;
    }

    /// <summary>Whether tile t of space s is over its stairwell (no floor under it).</summary>
    private static bool InWell(DungeonPlan.Space s, (int, int) t) =>
        s.Room == Room.StairTop && DungeonPlanner.Depth((s.X0, s.Z0, s.W, s.H), s.Dir, t) >= 4;
}
