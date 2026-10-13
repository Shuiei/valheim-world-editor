using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace WorldEditor.Tests;

/// <summary>
/// Objects made in code, as the game sends them live (WorldEditorBridge /zone: a ZDO's bytes in the save's format), for
/// what the test world does not hold: a player's piece (with its builder), a placed location (a LocationProxy naming
/// it), a dungeon with its room list, and a ValheimRAFT vehicle's root and parts. Made-up content: only the prefab and
/// data names are the game's and the mod's.
/// </summary>
public static class MadeObjects
{
    /// <summary>
    /// A location of no kind the editor treats apart (not a disc, not a runestone).
    /// </summary>
    public static readonly int MadeUpLocation = StableHash.Of("MadeUpLocation");

    /// <summary>
    /// An object's bytes: a prefab at a place, with what <paramref name="fill"/> adds to it.
    /// </summary>
    public static byte[] Zdo(string prefab, Vector3 position, Action<ZdoData>? fill = null)
    {
        var z = new ZdoData { Prefab = StableHash.Of(prefab), Position = position };
        fill?.Invoke(z);
        return z.Serialize();
    }

    /// <summary>
    /// A piece a player built: its builder's id in the "creator" long, its name in "creatorName".
    /// </summary>
    public static byte[] Piece(string prefab, Vector3 position, long builder, string name = "Builder") => Zdo(prefab, position, z =>
    {
        z.LongList.Add((StableHash.Of("creator"), builder));
        z.StringList.Add((StableHash.Of("creatorName"), name));
    });

    /// <summary>
    /// A placed location: a LocationProxy whose "location" int names it.
    /// </summary>
    public static byte[] Location(Vector3 position, int location) => Zdo("LocationProxy", position, z => z.IntList.Add((StableHash.Of("location"), location)));

    /// <summary>
    /// A dungeon object with its room list ("roomData"): these rooms, or bytes as given.
    /// </summary>
    public static byte[] Dungeon(string prefab, Vector3 position, IReadOnlyList<Dungeons.Placed> rooms) => Dungeon(prefab, position, Dungeons.Write(rooms));

    /// <inheritdoc cref="Dungeon(string, Vector3, IReadOnlyList{Dungeons.Placed})"/>
    public static byte[] Dungeon(string prefab, Vector3 position, byte[] roomData) => Zdo(prefab, position, z => z.ByteList.Add((Dungeons.RoomDataKey, roomData)));

    /// <summary>
    /// A vehicle's root (ValheimRAFT): its PersistentID, at a place and turned (Euler degrees) as stored.
    /// </summary>
    public static byte[] VehicleRoot(string prefab, Vector3 position, Vector3 rotation, int persistentId) => Zdo(prefab, position, z =>
    {
        z.Rotation = rotation;
        z.IntList.Add((StableHash.Of("PersistentID"), persistentId));
    });

    /// <summary>
    /// A vehicle's part, stored at its root's position as the game saves it: its vehicle's PersistentID, its offset and
    /// turn in its parent's frame, and, given, the swivel it is on and its own PersistentID.
    /// </summary>
    public static byte[] VehiclePart(string prefab, Vector3 rootPosition, int parent, Vector3 offset, Vector3 euler, int swivel = 0, int persistentId = 0) =>
        Zdo(prefab, rootPosition, z =>
        {
            z.IntList.Add((StableHash.Of("MBParentId"), parent));
            if (swivel != 0)
            {
                z.IntList.Add((StableHash.Of("SwivelParentId"), swivel));
            }
            if (persistentId != 0)
            {
                z.IntList.Add((StableHash.Of("PersistentID"), persistentId));
            }
            z.Vec3List.Add((StableHash.Of("MBPosition"), offset));
            z.Vec3List.Add((StableHash.Of("MBRotationVec"), euler));
        });
}
