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
        if (!BoxFits(l, box, aligned, tall))
        {
            return null;
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

    /// <summary>How often a corridor to a room bends on the way, for the style: often in catacombs, warrens and holds, seldom in a temple.</summary>
    private float BendChance => _style.Name switch
    {
        "Catacombs" or "Goblin warren" or "Dvergr hold" or "Ruins" => 0.4f,
        "Temple" => 0.1f,
        _ => 0.25f,
    };
}
