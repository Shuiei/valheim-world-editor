using static TerrainEditor.Editing.DungeonKit;

namespace TerrainEditor.Editing;

internal sealed partial class DungeonPlanner
{
    /// <summary>
    /// The room's box size: its kind's, sometimes turned to enter on the long side, and rounded to whole cells of four
    /// when tall.
    /// </summary>
    /// <param name="room">The kind of room.</param>
    /// <param name="tall">The room rises through two floors.</param>
    /// <returns>The width and depth in tiles.</returns>
    private (int W, int D) RoomSize(Room room, bool tall)
    {
        var spec = SpecOf(room);
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
        return (w, d);
    }

    /// <summary>The corridor's length in tiles: none when the room sits wall to wall, sometimes, else within the style's range.</summary>
    /// <param name="parent">The space the room opens off (a stair foot never gets a wall join).</param>
    /// <returns>The length; 0 for a wall to wall join.</returns>
    private int CorridorLength(DungeonPlan.Space parent)
    {
        bool wall = _rnd.NextDouble() < 0.25 && _style.CorridorMin <= 1 && parent.Room != Room.StairBottom;
        return wall ? 0 : _rnd.Next(_style.CorridorMin, _style.CorridorMax + 1);
    }
}
