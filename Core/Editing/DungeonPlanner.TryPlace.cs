
namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Tries to place a room of w × d tiles (across, deep) off side dir of parent, at slot t1/t2, with a
    /// corridor of len tiles and the room shifted across by shift tiles from centred. Aligned: the box must
    /// cover whole cells; tall: nothing may be in its cells on the level above.
    /// </summary>
    /// <returns>Null when it does not fit.</returns>
    private Placement? TryPlace(DungeonPlan.Level l, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir, int w, int d, int len, int shift,
        bool aligned, bool tall, DungeonPlan.Shape shape = DungeonPlan.Shape.Rect)
    {
        var (x, z) = (t2.I - t1.I, t2.J - t1.J); // across, t1 to t2
        var corridor = new List<(int, int)>();
        for (int t = 1; t <= len; t++)
        {
            corridor.Add(Add(t1, dir, t));
            corridor.Add(Add(t2, dir, t));
        }
        if (corridor.Any(c => !Free(l, c)))
        {
            return null;
        }
        var row0 = Add(t1, dir, len + 1);
        int off = w / 2 - 1 + shift;
        var first = (row0.Item1 - x * off, row0.Item2 - z * off);
        var far = (first.Item1 + x * (w - 1) + dir.X * (d - 1), first.Item2 + z * (w - 1) + dir.Z * (d - 1));
        var box = (Math.Min(first.Item1, far.Item1), Math.Min(first.Item2, far.Item2), Math.Abs(far.Item1 - first.Item1) + 1, Math.Abs(far.Item2 - first.Item2) + 1);
        if (off < 0 || off > w - 2)
        {
            return null;
        }
        if (aligned && (box.Item1 % 4 != 0 || box.Item2 % 4 != 0 || box.Item3 % 4 != 0 || box.Item4 % 4 != 0))
        {
            return null;
        }
        foreach (var t in Box(box))
        {
            if (!Free(l, t))
            {
                return null;
            }
        }
        if (tall && l.Index > 0)
        {
            // Two floors high: nothing on the level above in its cells.
            var above = _plan.Levels[l.Index - 1];
            foreach (var (ci, cj) in Box(box).Select(DungeonPlan.CellOf).ToHashSet())
            {
                for (int k = 0; k < 16; k++)
                {
                    if (above.Owner.ContainsKey((4 * ci + k % 4, 4 * cj + k / 4)))
                    {
                        return null;
                    }
                }
            }
        }
        // A shaped room keeps its whole box clear (what it cuts away stays empty, walled off), its tiles being
        // what it floors; the way in must be whole.
        var tiles = shape == DungeonPlan.Shape.Rect ? [.. Box(box)] : ShapeTiles(box, dir, shape);
        if (shape != DungeonPlan.Shape.Rect && (!tiles.Contains(row0) || !tiles.Contains(Add(t2, dir, len + 1))))
        {
            return null;
        }
        return new Placement(box, corridor, t1, t2, dir, shift, len, tall, shape, tiles, Add(t1, dir, len), Add(t2, dir, len), dir);
    }

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

    /// <summary>How often a corridor to a room bends on the way, for the style: often in catacombs, warrens and holds, seldom in a temple.</summary>
    private float BendChance => _style.Name switch
    {
        "Catacombs" or "Goblin warren" or "Dvergr hold" or "Ruins" => 0.4f,
        "Temple" => 0.1f,
        _ => 0.25f,
    };
}
