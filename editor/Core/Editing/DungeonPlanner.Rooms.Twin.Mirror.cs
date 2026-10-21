namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// The tile mirrored across the parent's middle from the room's: across the width when the room lies along x, else
    /// across the depth.
    /// </summary>
    /// <param name="parent">The space the room opens off.</param>
    /// <param name="n">The room.</param>
    /// <param name="t">The tile to mirror.</param>
    /// <returns>The mirrored tile.</returns>
    private static (int, int) MirrorAcross(DungeonPlan.Space parent, DungeonPlan.Space n, (int I, int J) t)
    {
        var (x0, z0, w, h) = parent.Own;
        return n.Dir.X != 0 ? (2 * x0 + w - 1 - t.I, t.J) : (t.I, 2 * z0 + h - 1 - t.J);
    }

    /// <summary>
    /// The twin's tiles when the room is shaped: the room's own tiles mirrored, kept only when they fit the twin's box and
    /// meet the doorway's tiles one step out; otherwise null (the twin is a plain box).
    /// </summary>
    /// <returns>The twin's tiles, or null for a plain box.</returns>
    private static List<(int, int)>? MirroredTiles(DungeonPlan.Space parent, DungeonPlan.Space n, Placement p, (int I, int J) o1, (int I, int J) o2, (int X, int Z) dir)
    {
        var tiles = n.Shape == DungeonPlan.Shape.Rect ? null : n.Tiles.Select(t => MirrorAcross(parent, n, t)).ToList();
        if (tiles != null && (tiles.Any(t => !Box(p.Box).Contains(t)) || !tiles.Contains(Add(o1, dir, p.Length + 1)) || !tiles.Contains(Add(o2, dir, p.Length + 1))))
        {
            tiles = null;
        }
        return tiles;
    }

    /// <summary>
    /// Finds the doorway a twin would open at: the parent's doorway tiles mirrored across its middle, on the side opposite
    /// the room, in the side's order, where a doorway is allowed.
    /// </summary>
    /// <param name="parent">The space the room opens off.</param>
    /// <param name="n">The room.</param>
    /// <param name="first">The room's doorway into the parent.</param>
    /// <param name="dir">The way the twin leaves the parent by.</param>
    /// <param name="o1">The twin's doorway's first tile.</param>
    /// <param name="o2">Its second.</param>
    /// <returns>True when the mirrored doorway fits.</returns>
    private static bool MirroredSlot(DungeonPlan.Space parent, DungeonPlan.Space n, DungeonPlan.Doorway first, out (int X, int Z) dir, out (int I, int J) o1,
        out (int I, int J) o2)
    {
        dir = (-n.Dir.X, -n.Dir.Z);
        var side = SideTiles(parent, dir);
        var m1 = MirrorAcross(parent, n, first.T1);
        var m2 = MirrorAcross(parent, n, first.T2);
        o1 = default;
        o2 = default;
        if (!side.Contains(m1) || !side.Contains(m2))
        {
            return false;
        }
        (o1, o2) = side.IndexOf(m1) < side.IndexOf(m2) ? (m1, m2) : (m2, m1);
        return SlotAllowed(parent, o1, o2, dir);
    }
}
