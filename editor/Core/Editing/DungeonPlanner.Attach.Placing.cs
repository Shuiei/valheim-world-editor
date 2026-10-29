using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Picks the way out of the parent and the slot on its side for a new room: a side by weight (straight ahead when
    /// <paramref name="forward"/>), the slot in the middle of that side most of the time.
    /// </summary>
    /// <param name="parent">The space the room opens off.</param>
    /// <param name="main">On the main way: straight ahead is favoured.</param>
    /// <param name="forward">Straight ahead only.</param>
    /// <param name="dir">The way out.</param>
    /// <param name="t1">The slot's first tile.</param>
    /// <param name="t2">The slot's second tile.</param>
    /// <returns>True when the side has a slot a doorway may open at.</returns>
    private bool ChooseSlot(DungeonPlan.Space parent, bool main, bool forward, out (int X, int Z) dir, out (int I, int J) t1, out (int I, int J) t2)
    {
        var dirs = Dirs.Where(dd => dd != (-parent.Dir.X, -parent.Dir.Z) || parent.Room == Room.Hub).ToList();
        dir = forward ? parent.Dir : Pick(dirs.Select(dd => (dd, dd == parent.Dir ? (main ? 5f : 0.7f) : (main ? 1f : 3f))).ToList());
        var side = SideTiles(parent, dir);
        t1 = default;
        t2 = default;
        if (side.Count < 2)
        {
            return false;
        }
        int mid = side.Count / 2 - 1, at = _rnd.NextDouble() < 0.7 ? mid : _rnd.Next(side.Count - 1);
        (t1, t2) = (side[at], side[at + 1]);
        return IsSlot(t1, t2, parent, dir);
    }

    /// <summary>
    /// Tries to fit a room of the given size and shape at a slot: bent when <paramref name="bend"/>, aligned to the cells
    /// of the level below when tall, else straight.
    /// </summary>
    /// <returns>The placement, or null when it does not fit.</returns>
    private Placement? PlaceRoomAt(DungeonPlan.Level l, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir, int w, int d, int len, int shift, bool bend,
        bool tall, DungeonPlan.Shape shape)
    {
        if (bend)
        {
            return TryPlaceBent(l, t1, t2, dir, w, d, Math.Max(1, len), _rnd.Next(2) * 2 - 1, _rnd.Next(2, 6), shift, aligned: false, tall: false, shape);
        }
        if (tall)
        {
            return TryPlaceTallAligned(l, t1, t2, dir, w, d, len, shape);
        }
        return TryPlace(l, t1, t2, dir, w, d, len, shift, aligned: false, tall: false, shape);
    }

    /// <summary>A tall room aligned to the cells: the first placement found over a few longer corridors and nearby shifts.</summary>
    /// <returns>The placement, or null when none fits.</returns>
    private Placement? TryPlaceTallAligned(DungeonPlan.Level l, (int I, int J) t1, (int I, int J) t2, (int X, int Z) dir, int w, int d, int len,
        DungeonPlan.Shape shape)
    {
        Placement? p = null;
        for (int dl = 0; dl < 4 && p == null; dl++)
        {
            for (int sh = -(w / 2 - 1); sh <= w / 2 - 1 && p == null; sh++)
            {
                p = TryPlace(l, t1, t2, dir, w, d, len + dl, sh, aligned: true, tall: true, shape);
            }
        }
        return p;
    }
}
