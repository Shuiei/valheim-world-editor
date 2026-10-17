using System.Numerics;

namespace TerrainEditor.Editing;

public static partial class Dungeons
{
    /// <summary>The door types a joint of this type can take in this kind of dungeon.</summary>
    public static IEnumerable<(string Prefab, string Type, float Chance)> DoorsFor(Kind kind, string type) => kind.Doors.Where(d => d.Type == type);

    /// <summary>The game's roll for a door at a joint (FindDoorType, then its chance).</summary>
    /// <param name="kind">The dungeon kind, for its door types.</param>
    /// <param name="joint">Where the rooms meet.</param>
    /// <param name="random">Numbers from 0 to 1; called twice when a door can stand there.</param>
    /// <returns>The door's prefab, or null.</returns>
    public static string? RollDoor(Kind kind, Joint joint, Func<float> random)
    {
        var types = DoorsFor(kind, joint.Type).ToList();
        if (!joint.DoorAllowed || types.Count == 0)
        {
            return null;
        }
        var door = types[Math.Min(types.Count - 1, (int)(random() * types.Count))];
        float chance = door.Chance > 0 ? door.Chance : kind.DoorChance;
        return random() <= chance ? door.Prefab : null;
    }

    // ---- Room boxes.
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

    /// <summary>Separating axis test of two turned boxes (centre, half size, rotation); touching faces do not count.</summary>
    public static bool BoxesOverlap((Vector3 C, Vector3 H, Quaternion R) a, (Vector3 C, Vector3 H, Quaternion R) b)
    {
        Vector3[] ax = [Vector3.Transform(Vector3.UnitX, a.R), Vector3.Transform(Vector3.UnitY, a.R), Vector3.Transform(Vector3.UnitZ, a.R)];
        Vector3[] bx = [Vector3.Transform(Vector3.UnitX, b.R), Vector3.Transform(Vector3.UnitY, b.R), Vector3.Transform(Vector3.UnitZ, b.R)];
        Vector3 d = b.C - a.C;
        float[] ha = [a.H.X, a.H.Y, a.H.Z], hb = [b.H.X, b.H.Y, b.H.Z];
        bool Separated(Vector3 axis)
        {
            if (axis.LengthSquared() < 1e-8f)
            {
                return false;
            }
            float ra = 0, rb = 0;
            for (int i = 0; i < 3; i++)
            {
                ra += ha[i] * MathF.Abs(Vector3.Dot(ax[i], axis));
                rb += hb[i] * MathF.Abs(Vector3.Dot(bx[i], axis));
            }
            // Touching faces do not overlap (rooms that join share a wall plane).
            return MathF.Abs(Vector3.Dot(d, axis)) >= ra + rb - 1e-3f * axis.Length();
        }
        for (int i = 0; i < 3; i++)
        {
            if (Separated(ax[i]) || Separated(bx[i]))
            {
                return false;
            }
        }
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                if (Separated(Vector3.Cross(ax[i], bx[j])))
                {
                    return false;
                }
            }
        }
        return true;
    }
}
