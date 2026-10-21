using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Dungeons
{
    /// <summary>
    /// A room's box (Room.m_size, centred on the room's position, turned with it), for picking and for
    /// the game's overlap test (DungeonGenerator.TestCollision: the new room's box 0.1 m smaller). An
    /// unknown room gets a 4 m cube.
    /// </summary>
    public static (Vector3 Center, Vector3 Half, Quaternion Rotation) Box(Placed p)
    {
        Vector3 size = p.Room?.Size ?? new Vector3(4);
        return (p.Position, size / 2f, p.Rotation);
    }

    /// <summary>
    /// Indexes of the placed rooms the box of <paramref name="p"/> (at index <paramref name="skip"/>, -1 for
    /// a new one) overlaps; unknown rooms and rooms without a floor size never count.
    /// </summary>
    public static List<int> Overlaps(IReadOnlyList<Placed> rooms, Placed p, int skip = -1)
    {
        var hits = new List<int>();
        Room? room = p.Room;
        if (room == null || room.Size.X == 0 || room.Size.Z == 0)
        {
            return hits;
        }
        var a = (p.Position, (room.Size - new Vector3(0.1f)) / 2f, p.Rotation);
        for (int i = 0; i < rooms.Count; i++)
        {
            if (i == skip || rooms[i].Room is not { } other || other.Size.X == 0 || other.Size.Z == 0)
            {
                continue;
            }
            if (BoxesOverlap(a, (rooms[i].Position, other.Size / 2f, rooms[i].Rotation)))
            {
                hits.Add(i);
            }
        }
        return hits;
    }
}
