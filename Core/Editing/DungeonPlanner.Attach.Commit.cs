using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// How good a placement is: near the dungeon's middle, centred, straight ahead on the main way, tall when wanted,
    /// with a short corridor, with a random tiebreak.
    /// </summary>
    /// <param name="p">The placement.</param>
    /// <param name="parent">The space it opens off.</param>
    /// <param name="dir">The way it leaves the parent by.</param>
    /// <param name="main">On the main way.</param>
    /// <param name="tall">The room rises through two floors.</param>
    /// <returns>The score; higher is better.</returns>
    private float ScorePlacement(Placement p, DungeonPlan.Space parent, (int X, int Z) dir, bool main, bool tall)
    {
        float cx = (p.Box.X0 + p.Box.W / 2f) * 2, cz = (p.Box.Z0 + p.Box.H / 2f) * 2;
        return (float)_rnd.NextDouble() * 2 - 0.004f * (cx * cx + (cz - 40) * (cz - 40)) / 10 + (p.Shift == 0 ? 1 : 0) + (dir == parent.Dir && main ? 2 : 0)
            + (tall ? 3 : 0) - p.Length * 0.15f;
    }

    /// <summary>Adds the chosen placement to the level and the plan as a room off parent, with its tall cells, and joins them.</summary>
    /// <returns>The new room.</returns>
    private DungeonPlan.Space AddPlacedRoom(DungeonPlan.Level l, DungeonPlan.Space parent, Room room, Placement best, Door? door)
    {
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
