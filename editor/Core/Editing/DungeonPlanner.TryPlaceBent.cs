namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Tries to place a room off parent's slot t1/t2 at the end of a corridor that bends: len tiles ahead, an elbow,
    /// then len2 tiles to the right (side 1) or the left (-1), the room entered that way, wall to wall.
    /// </summary>
    /// <returns>Null when it does not fit.</returns>
    private Placement? TryPlaceBent(DungeonPlan.Level l, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir, int w, int d, int len, int side, int len2, int shift,
        bool aligned, bool tall, DungeonPlan.Shape shape)
    {
        var across = (X: t2.I - t1.I, Z: t2.J - t1.J);
        var turn = side > 0 ? across : (X: -across.X, Z: -across.Z);
        var corridor = new List<(int, int)>();
        for (int k = 1; k <= len; k++)
        {
            corridor.Add(Add(t1, dir, k));
            corridor.Add(Add(t2, dir, k));
        }
        // The elbow: two rows over both columns; the leg runs on from the column on the turn's side.
        var (a1, a2, b1, b2) = (Add(t1, dir, len + 1), Add(t1, dir, len + 2), Add(t2, dir, len + 1), Add(t2, dir, len + 2));
        corridor.AddRange([a1, a2, b1, b2]);
        var (s1, s2) = side > 0 ? (b1, b2) : (a1, a2);
        for (int m = 1; m <= len2; m++)
        {
            corridor.Add(Add(s1, turn, m));
            corridor.Add(Add(s2, turn, m));
        }
        if (corridor.Any(c => !Free(l, c)))
        {
            return null;
        }
        var (e1, e2) = (Add(s1, turn, len2), Add(s2, turn, len2));
        var p = TryPlace(l, e1, e2, turn, w, d, 0, shift, aligned, tall, shape);
        if (p == null || Box(p.Box).Any(corridor.Contains))
        {
            return null;
        }
        return p with { Corridor = corridor, T1 = t1, T2 = t2, Dir = dir, Length = len + 2 + len2, E1 = e1, E2 = e2, EndDir = turn };
    }
}
