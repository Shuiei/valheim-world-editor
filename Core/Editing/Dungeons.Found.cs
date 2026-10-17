using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public static partial class Dungeons
{
    // ---- Dungeons of a save.
    /// <summary>A dungeon object found in a save.</summary>
    /// <param name="Id">The object's id in the save.</param>
    /// <param name="Kind">Its kind.</param>
    /// <param name="Position">Where it stands.</param>
    /// <param name="Rooms">How many rooms its list has (0: none, or not readable).</param>
    public sealed record Found(int Id, Kind Kind, Vector3 Position, int Rooms);

    /// <summary>Every dungeon object of the save, with its room count.</summary>
    public static List<Found> In(WorldSave world)
    {
        var found = new List<Found>();
        foreach (var (id, prefab, position, _, _) in world.Objects)
        {
            if (KindOf(prefab) is not { } kind)
            {
                continue;
            }
            int count = 0;
            try
            {
                byte[]? data = ZdoData.Parse(world.ObjectBytes(id), world.ObjectRefs[id].File.WorldVersion).GetBytes(RoomDataKey);
                count = data == null ? 0 : BitConverter.ToInt32(data, 0);
            }
            catch (Exception e) when (e is InvalidDataException or IOException or ArgumentException or FormatException or OverflowException)
            {
                // Damaged or unreadable: found with no rooms, said in the log, and the others still listed.
                Log.CaughtBrief(e, $"reading the room list of the dungeon object {id} ({kind}) at {position}, world version {world.ObjectRefs[id].File.WorldVersion} (listed with no rooms)");
            }
            found.Add(new Found(id, kind, position, count));
        }
        return found;
    }

    /// <summary>The dungeon object's bytes with another room list.</summary>
    public static byte[] WithRooms(byte[] dungeonObject, IReadOnlyList<Placed> rooms)
    {
        var z = ZdoData.Parse(dungeonObject);
        z.SetBytes(RoomDataKey, Write(rooms));
        return z.Serialize();
    }
}
