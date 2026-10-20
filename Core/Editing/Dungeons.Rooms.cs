using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Editing;

public static partial class Dungeons
{
    // ---- The room list.
    /// <summary>One entry of a dungeon's room list: a room where it stands.</summary>
    /// <param name="Hash">The stable hash of the room prefab's name.</param>
    /// <param name="Position">World space.</param>
    /// <param name="Rotation">World space.</param>
    public sealed record Placed(int Hash, Vector3 Position, Quaternion Rotation)
    {
        /// <summary>The room, or null when the hash is not a known room.</summary>
        public Room? Room => RoomOf(Hash);

        /// <summary>The hash a room list keeps of it: its room's game room's (<see cref="Room.Saved"/>), or its own.</summary>
        public int Saved => Room?.Saved ?? Hash;

        /// <summary>The room's name, or its hash when it is not known.</summary>
        public string Name => Room?.Name ?? Hash.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>An opening in world space: where it is and which way it faces.</summary>
        public (Vector3 Position, Quaternion Rotation) At(Opening o) => (Position + Vector3.Transform(o.Position, Rotation), Rotation * o.Rotation);

        /// <summary>A point of the room's own frame in world space.</summary>
        public Vector3 ToWorld(Vector3 local) => Position + Vector3.Transform(local, Rotation);
    }

    /// <summary>
    /// Reads a room list as DungeonGenerator.Save writes it: an int count, then per room its hash (int),
    /// position (3 floats) and rotation (Euler angles, 3 floats).
    /// </summary>
    /// <exception cref="InvalidDataException">The count does not fit the bytes.</exception>
    public static List<Placed> Read(byte[] data)
    {
        using var r = new BinaryReader(new MemoryStream(data));
        int n = r.ReadInt32();
        if (n < 0 || n > (data.Length - 4) / 28)
        {
            throw new InvalidDataException($"The dungeon's room list is damaged ({n} rooms in {data.Length} bytes).");
        }
        var list = new List<Placed>(n);
        for (int i = 0; i < n; i++)
        {
            int hash = r.ReadInt32();
            var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var euler = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            list.Add(new Placed(hash, pos, BlueprintFormats.FromEuler(euler)));
        }
        return list;
    }

    /// <summary>
    /// Writes a room list the way the game saves it (see <see cref="Read"/>): a copy of a game room (Expand World Data's
    /// "name:suffix") as its game room, as the mod saves it.
    /// </summary>
    public static byte[] Write(IReadOnlyList<Placed> rooms)
    {
        var ms = new MemoryStream(4 + rooms.Count * 28);
        using (var w = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(rooms.Count);
            foreach (Placed p in rooms)
            {
                Vector3 e = BlueprintFormats.ToEuler(p.Rotation);
                w.Write(p.Saved);
                w.Write(p.Position.X);
                w.Write(p.Position.Y);
                w.Write(p.Position.Z);
                w.Write(e.X);
                w.Write(e.Y);
                w.Write(e.Z);
            }
        }
        return ms.ToArray();
    }

    // ---- Joining rooms.
    /// <summary>
    /// Joins a room the game's way (DungeonGenerator.PlaceRoom + CalculateRoomPosRot): the new room is
    /// turned so that its opening <paramref name="mine"/> faces the other way from the open one, and moved
    /// so the two meet.
    /// </summary>
    public static Placed Attach(Room room, Opening mine, Vector3 openPosition, Quaternion openRotation)
    {
        Quaternion exit = openRotation * Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        Quaternion rot = exit * Quaternion.Inverse(mine.Rotation);
        Vector3 pos = openPosition - Vector3.Transform(mine.Position, rot);
        return new Placed(room.Hash, pos, Quaternion.Normalize(rot));
    }

    /// <summary>
    /// Places a dungeon's first room the game's way (DungeonGenerator.PlaceStartRoom): its way in (the entrance
    /// opening, else its first opening, else its own origin) at the dungeon object, facing the way the object does.
    /// </summary>
    /// <param name="room">The first room.</param>
    /// <param name="position">The dungeon object's place.</param>
    /// <param name="rotation">The dungeon object's turn.</param>
    public static Placed PlaceStart(Room room, Vector3 position, Quaternion rotation)
    {
        var way = room.Openings.FirstOrDefault(o => o.Entrance) ?? room.Openings.FirstOrDefault();
        if (way == null)
        {
            return new Placed(room.Hash, position, Quaternion.Normalize(rotation));
        }
        Quaternion rot = rotation * Quaternion.Inverse(way.Rotation);
        return new Placed(room.Hash, position - Vector3.Transform(way.Position, rot), Quaternion.Normalize(rot));
    }

    /// <summary>
    /// The openings a room can join an open one of this type with: those of the same type (an end cap's
    /// only opening too), never the entrance's way in nor an exit-only one.
    /// </summary>
    public static IEnumerable<Opening> Matching(Room room, string type) => room.Openings.Where(o => o.Joins(type));
}
