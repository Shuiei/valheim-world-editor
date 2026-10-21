using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

public static partial class DungeonEntrances
{
    /// <summary>The middle of the zone a point is in (ZoneSystem.GetZonePos), at height 0.</summary>
    public static Vector3 ZoneCentre(Vector3 p)
    {
        var (zx, zz) = ChunkMath.SectorOf(p.X, p.Z);
        return new(zx * 64f, 0f, zz * 64f);
    }

    /// <summary>
    /// Where the game puts the location's interior transform for an entrance at <paramref name="at"/>, whichever way it
    /// is turned: at the middle of the zone, 5000 m (its own height) over the entrance's ground, square to the world
    /// (SpawnLocation's custom interior transform). Null unless the location asks for that.
    /// </summary>
    private static Pose? CustomInterior(LocationPrefabs.Prefab p, Vector3 at)
    {
        if (!p.CustomInterior || p.InteriorAt is not { } interior)
        {
            return null;
        }
        // The game adds the interior's and the dungeon's own places to the zone's middle (the dungeon's place in the
        // prefab is the sum of the two), and keeps the interior's height over the entrance.
        var local = Dungeon(p)?.Position ?? interior.Position;
        var zone = ZoneCentre(at);
        return new Pose(new Vector3(zone.X + local.X, at.Y + interior.Position.Y, zone.Z + local.Z), Quaternion.Identity);
    }

    /// <summary>
    /// Where the game puts the location's dungeon object for an entrance at <paramref name="at"/> turned
    /// <paramref name="turn"/>: with the location, or at the custom interior's place. Null when the location makes no dungeon.
    /// </summary>
    public static Pose? DungeonPose(LocationPrefabs.Prefab p, Vector3 at, Quaternion turn)
    {
        if (Dungeon(p) is not { } dg)
        {
            return null;
        }
        if (CustomInterior(p, at) is { } interior)
        {
            // The dungeon is moved onto the interior transform (its own place in it zeroed), keeping its turn.
            return new Pose(interior.Position, dg.Rotation);
        }
        return new Pose(at + Vector3.Transform(dg.Position, turn), turn * dg.Rotation);
    }

    /// <summary>
    /// Where the game puts the location's door out of its interior for an entrance at <paramref name="at"/> turned
    /// <paramref name="turn"/>: with the location, or with the custom interior. Null when the location has no such door.
    /// </summary>
    public static Pose? ExitPose(LocationPrefabs.Prefab p, Vector3 at, Quaternion turn)
    {
        if (p.Exit is not { } exit)
        {
            return null;
        }
        if (CustomInterior(p, at) is { } interior && p.InteriorAt is { } anchor)
        {
            // Under the interior transform, which stands square to the world: the door's place and turn in it.
            var inverse = Quaternion.Inverse(anchor.Rotation);
            return new Pose(interior.Position + Vector3.Transform(exit.Position - anchor.Position, inverse), Quaternion.Normalize(inverse * exit.Rotation));
        }
        return new Pose(at + Vector3.Transform(exit.Position, turn), Quaternion.Normalize(turn * exit.Rotation));
    }

    /// <summary>Where a player stands on arriving through a door (Teleport.GetTeleportPoint): one metre ahead of it, one down.</summary>
    public static Vector3 Landing(Pose door) => door.Position + door.Forward - Vector3.UnitY;
}
