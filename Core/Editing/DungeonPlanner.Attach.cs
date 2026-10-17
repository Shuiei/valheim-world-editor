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
        bool tallWanted = SpecOf(room).Tall && _rnd.NextDouble() < _style.TallChance;
        Placement? best = null;
        float bestScore = float.MinValue;
        for (int attempt = 0; attempt < 48; attempt++)
        {
            var candidate = PlaceCandidate(l, parent, room, main, forward, tallWanted && attempt < 32);
            if (candidate is { } c && c.Score > bestScore)
            {
                best = c.Place;
                bestScore = c.Score;
            }
        }
        if (best == null)
        {
            return null;
        }
        return AddPlacedRoom(l, parent, room, best, door);
    }

    /// <summary>
    /// One attempt at placing the room: a slot on a side of the parent, a size, a shape and a corridor, if the room fits there.
    /// </summary>
    /// <returns>The placement and how good it is, or null when this attempt does not fit.</returns>
    private (Placement Place, float Score)? PlaceCandidate(DungeonPlan.Level l, DungeonPlan.Space parent, Room room, bool main, bool forward, bool tall)
    {
        if (!ChooseSlot(parent, main, forward, out var dir, out var t1, out var t2))
        {
            return null;
        }
        var (w, d) = RoomSize(room, tall);
        var shape = ShapeFor(room, w, d);
        int len = CorridorLength(parent);
        int shift = _rnd.NextDouble() < 0.75 || room == Room.Cells ? 0 : _rnd.Next(-(w / 2 - 1), w / 2);
        bool bend = len > 0 && !tall && room != Room.Cells && _rnd.NextDouble() < BendChance * (main ? 0.5f : 1f);
        var p = PlaceRoomAt(l, t1, t2, dir, w, d, len, shift, bend, tall, shape);
        if (p == null)
        {
            return null;
        }
        return (p, ScorePlacement(p, parent, dir, main, tall));
    }
}
