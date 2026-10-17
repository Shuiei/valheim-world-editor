using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// Places a room of a kind off parent, trying many sides, slots, sizes and corridors and keeping the best
    /// (near the dungeon's middle, centred, straight ahead on the main way, tall when wanted, short corridor).
    /// </summary>
    /// <param name="l">The level.</param>
    /// <param name="parent">The space it opens off.</param>
    /// <param name="room">The kind of room.</param>
    /// <param name="main">On the main way: straight ahead is favoured.</param>
    /// <param name="forward">Only straight ahead (the parent's way in).</param>
    /// <param name="door">What stands in the doorway (null: decided later); Secret hides the way from the parent.</param>
    /// <returns>The new room, or null when it fits nowhere or parent cannot lead on.</returns>
    private DungeonPlan.Space? Attach(DungeonPlan.Level l, DungeonPlan.Space parent, Room room, bool main, bool forward = false, Door? door = null)
    {
        if (!CanParent(parent, room))
        {
            return null;
        }
        var spec = SpecOf(room);
        bool tallWanted = spec.Tall && _rnd.NextDouble() < _style.TallChance;
        Placement? best = null;
        float bestScore = float.MinValue;
        for (int attempt = 0; attempt < 48; attempt++)
        {
            bool tall = tallWanted && attempt < 32;
            var dirs = Dirs.Where(dd => dd != (-parent.Dir.X, -parent.Dir.Z) || parent.Room == Room.Hub).ToList();
            var dir = forward ? parent.Dir : Pick(dirs.Select(dd => (dd, dd == parent.Dir ? (main ? 5f : 0.7f) : (main ? 1f : 3f))).ToList());
            var side = SideTiles(parent, dir);
            if (side.Count < 2)
            {
                continue;
            }
            // The slot: in the middle of the side most of the time.
            int mid = side.Count / 2 - 1, at = _rnd.NextDouble() < 0.7 ? mid : _rnd.Next(side.Count - 1);
            var (t1, t2) = (side[at], side[at + 1]);
            if (!IsSlot(t1, t2, parent, dir))
            {
                continue;
            }
            int w = Even(spec.WMin, spec.WMax), d = Even(spec.DMin, spec.DMax);
            // Now and then a room is entered on its long side.
            if (spec.WMax != spec.WMin && spec.DMax > spec.WMax && room is not (Room.Gallery or Room.Mine or Room.Cells or Room.Feast or Room.Entrance) && _rnd.NextDouble() < 0.25)
            {
                (w, d) = (d, w);
            }
            if (tall)
            {
                w = Math.Max(8, (w + 3) / 4 * 4);
                d = Math.Max(8, (d + 3) / 4 * 4);
            }
            var shape = ShapeFor(room, w, d);
            bool wall = _rnd.NextDouble() < 0.25 && _style.CorridorMin <= 1 && parent.Room != Room.StairBottom;
            int len = wall ? 0 : _rnd.Next(_style.CorridorMin, _style.CorridorMax + 1);
            int shift = _rnd.NextDouble() < 0.75 || room == Room.Cells ? 0 : _rnd.Next(-(w / 2 - 1), w / 2);
            bool bend = len > 0 && !tall && room != Room.Cells && _rnd.NextDouble() < BendChance * (main ? 0.5f : 1f);
            Placement? p = null;
            if (bend)
            {
                p = TryPlaceBent(l, t1, t2, dir, w, d, Math.Max(1, len), _rnd.Next(2) * 2 - 1, _rnd.Next(2, 6), shift, aligned: false, tall: false, shape);
            }
            else if (tall)
            {
                // Aligned to cells: try nearby shifts and lengths.
                for (int dl = 0; dl < 4 && p == null; dl++)
                {
                    for (int sh = -(w / 2 - 1); sh <= w / 2 - 1 && p == null; sh++)
                    {
                        p = TryPlace(l, t1, t2, dir, w, d, len + dl, sh, aligned: true, tall: true, shape);
                    }
                }
            }
            else
            {
                p = TryPlace(l, t1, t2, dir, w, d, len, shift, aligned: false, tall: false, shape);
            }
            if (p == null)
            {
                continue;
            }
            float cx = (p.Box.X0 + p.Box.W / 2f) * 2, cz = (p.Box.Z0 + p.Box.H / 2f) * 2;
            float score = (float)_rnd.NextDouble() * 2 - 0.004f * (cx * cx + (cz - 40) * (cz - 40)) / 10 + (p.Shift == 0 ? 1 : 0) + (dir == parent.Dir && main ? 2 : 0)
                + (tall ? 3 : 0) - p.Length * 0.15f;
            if (score > bestScore)
            {
                best = p;
                bestScore = score;
            }
        }
        if (best == null)
        {
            return null;
        }
        var n = NewSpace(l, room, best.EndDir, best.Box, best.Shape == DungeonPlan.Shape.Rect ? null : best.Tiles);
        n.Shape = best.Shape;
        n.Parent = parent;
        n.Tall = best.Tall;
        if (n.Tall)
        {
            foreach (var t in n.Tiles)
            {
                l.TallCells.Add(DungeonPlan.CellOf(t));
            }
        }
        Join(l, parent, n, best, door);
        if (!n.Tall && room is Room.Gallery or Room.Burial or Room.Ossuary or Room.Chapel or Room.Hall or Room.Tomb or Room.Throne or Room.Armory or Room.Library && _rnd.NextDouble() < 0.4)
        {
            Recesses(l, n, 2, _rnd.Next(1, 3));
        }
        return n;
    }
}
