using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// A shape for a room of this kind and size, by chance: crossings, shrines, arenas and tombs are often
    /// notched at the corners, chapels, halls and throne rooms end in an apse, store rooms and quarters lose a
    /// corner or neck in at the door; long rooms, cell blocks, stairs and the entrance stay whole.
    /// </summary>
    private DungeonPlan.Shape ShapeFor(Room room, int across, int deep)
    {
        if (across < 6 || deep < 6)
        {
            return DungeonPlan.Shape.Rect;
        }
        double roll = _rnd.NextDouble();
        return room switch
        {
            Room.Hub or Room.Shrine or Room.Totem => roll < 0.45 ? DungeonPlan.Shape.Cross : DungeonPlan.Shape.Rect,
            Room.Boss => roll < 0.6 ? DungeonPlan.Shape.Cross : DungeonPlan.Shape.Rect,
            Room.Tomb or Room.Treasury => roll < 0.35 ? DungeonPlan.Shape.Cross : DungeonPlan.Shape.Rect,
            Room.Chapel => roll < 0.45 ? DungeonPlan.Shape.Apse : roll < 0.6 ? DungeonPlan.Shape.Cross : DungeonPlan.Shape.Rect,
            Room.Hall or Room.Throne => roll < 0.35 ? DungeonPlan.Shape.Apse : DungeonPlan.Shape.Rect,
            Room.Burial or Room.Ossuary => roll < 0.3 ? DungeonPlan.Shape.L : DungeonPlan.Shape.Rect,
            Room.Store or Room.Quarters or Room.Workshop or Room.Guard or Room.Library or Room.Kitchen or Room.Armory or Room.Barracks or Room.Den or Room.Torture =>
                roll < 0.3 ? DungeonPlan.Shape.L : roll < 0.42 && across >= 8 ? DungeonPlan.Shape.T : DungeonPlan.Shape.Rect,
            _ => DungeonPlan.Shape.Rect,
        };
    }

    /// <summary>What a placement would make: the room's tiles, the corridor's, where they meet.</summary>
    /// <param name="Box">The room's box (tiles).</param>
    /// <param name="Corridor">The corridor's tiles (none: wall to wall).</param>
    /// <param name="T1">The parent's first tile of the doorway.</param>
    /// <param name="T2">The parent's second tile of the doorway.</param>
    /// <param name="Dir">The way from the parent into the room.</param>
    /// <param name="Shift">Tiles the room is shifted across from centred.</param>
    /// <param name="Length">The corridor's length (tiles).</param>
    /// <param name="Tall">The room rises through two floors.</param>
    /// <param name="Shape">The room's shape within its box.</param>
    /// <param name="Tiles">The room's tiles (its box, less what the shape cuts away).</param>
    /// <param name="E1">The corridor's first tile at its far end, where the room's doorway is (T1 when there is no corridor).</param>
    /// <param name="E2">Its second.</param>
    /// <param name="EndDir">The way into the room from there (Dir unless the corridor bends).</param>
    private sealed record Placement((int X0, int Z0, int W, int H) Box, List<(int, int)> Corridor, (int, int) T1, (int, int) T2, (int X, int Z) Dir, int Shift, int Length, bool Tall,
        DungeonPlan.Shape Shape, List<(int, int)> Tiles, (int, int) E1, (int, int) E2, (int X, int Z) EndDir);
}
