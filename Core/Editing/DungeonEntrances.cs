using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

/// <summary>
/// A dungeon's entrance on the ground: the game's location that leads into a dungeon of a kind (a burial chamber's
/// mound for a forest crypt, a Frost Cave's mouth, a sunken crypt's door...), and where the game puts the dungeon
/// and the door out of it for an entrance stood somewhere (ZoneSystem.SpawnLocation): straight above the entrance,
/// turned with it; or, for a location with a custom interior transform, at the middle of the entrance's zone, square
/// to the world, 5000 m over the entrance's ground.
/// </summary>
public static class DungeonEntrances
{
    /// <summary>A place and a turn in the world.</summary>
    /// <param name="Position">Where, in world metres.</param>
    /// <param name="Rotation">Which way it is turned.</param>
    public readonly record struct Pose(Vector3 Position, Quaternion Rotation)
    {
        /// <summary>The way it faces (its own +z).</summary>
        public Vector3 Forward => Vector3.Transform(Vector3.UnitZ, Rotation);
    }

    /// <summary>What a location is called as an entrance (<see cref="Describe"/>).</summary>
    internal enum Called
    {
        /// <summary>Its own name: no other is shown.</summary>
        OwnName,
        /// <summary>A burial chamber.</summary>
        BurialChamber,
        /// <summary>A sunken crypt.</summary>
        SunkenCrypt,
        /// <summary>A Frost Cave.</summary>
        FrostCave,
        /// <summary>An infested mine.</summary>
        InfestedMine,
        /// <summary>A Morgen hole.</summary>
        MorgenHole,
    }

    /// <summary>
    /// The locations with a dungeon (the game lays it out 5000 m above them when a player first comes near), with what
    /// each is called as an entrance (<see cref="Called.OwnName"/>: its name) and whether it is preferred as one; the
    /// preferred come first for a dungeon kind, in this order. One table for <see cref="DungeonGen.DungeonLocations"/>,
    /// <see cref="For"/> and <see cref="Describe"/>.
    /// </summary>
    internal static readonly (string Name, Called Description, bool Preferred)[] Locations =
    [
        ("Crypt2", Called.BurialChamber, true), ("Crypt3", Called.BurialChamber, true), ("Crypt4", Called.BurialChamber, true),
        ("HalfBurried_ForestCrypt", Called.BurialChamber, false), ("SunkenCrypt4", Called.SunkenCrypt, true), ("MountainCave02", Called.FrostCave, true),
        ("Hildir_crypt", Called.OwnName, false), ("Hildir_cave", Called.OwnName, false), ("Hildir_plainsfortress", Called.OwnName, false),
        ("Mistlands_DvergrTownEntrance1", Called.InfestedMine, true), ("Mistlands_DvergrTownEntrance2", Called.InfestedMine, true),
        ("Mistlands_DvergrBossEntrance1", Called.OwnName, false), ("MorgenHole1", Called.MorgenHole, true), ("MorgenHole2", Called.MorgenHole, true),
        ("MorgenHole3", Called.MorgenHole, true), ("TheHole01", Called.OwnName, false), ("TheDarkestHole", Called.OwnName, false), ("CharredFortress", Called.OwnName, false),
    ];

    /// <summary>The entrances preferred for a dungeon kind, in this order, before any other location that holds it.</summary>
    private static readonly string[] Preferred = [.. Locations.Where(l => l.Preferred).Select(l => l.Name)];

    /// <summary>Locations that never serve as an entrance: a quest's or a boss's own places.</summary>
    private static readonly string[] Never = ["Hildir_", "Mistlands_DvergrBossEntrance"];

    /// <summary>
    /// The location that leads into a dungeon of this kind (a DG_ prefab name): one with an interior, a door out of it
    /// and that dungeon among what it makes; the preferred ones first, then by name. Null when the game's files describe
    /// none.
    /// </summary>
    public static LocationPrefabs.Prefab? For(string dungeonKind) =>
        LocationPrefabs.All
            .Where(p => p.Interior && p.Exit != null && Dungeon(p) is { } dg && dg.Prefab == dungeonKind && !Never.Any(n => p.Name.StartsWith(n, StringComparison.Ordinal)))
            .OrderBy(p => Array.IndexOf(Preferred, p.Name) is int i and >= 0 ? i : Preferred.Length)
            .ThenBy(p => p.Name, StringComparer.Ordinal)
            .FirstOrDefault();

    /// <summary>The entrance of a biome's own dungeon (<see cref="DungeonGen.KindFor"/>), or null when it has none that serves.</summary>
    public static LocationPrefabs.Prefab? ForBiome(string biome) => For(DungeonGen.KindFor(biome));

    /// <summary>The dungeon object a location makes (its DG_ content), or null when it makes none.</summary>
    public static Dungeons.Content? Dungeon(LocationPrefabs.Prefab p) =>
        p.Contents.FirstOrDefault(c => Dungeons.KindOf(StableHash.Of(c.Prefab)) != null || c.Prefab.StartsWith("DG_", StringComparison.Ordinal));

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

    /// <summary>
    /// The turn (degrees about the vertical, from 0 up to 360) that makes the location's door into its interior face
    /// from <paramref name="at"/> toward <paramref name="toward"/>; 0 when it has no such door or the two are one place.
    /// </summary>
    public static float YawToFace(LocationPrefabs.Prefab p, Vector3 at, Vector3 toward)
    {
        if (p.Entrance is not { } door)
        {
            return 0f;
        }
        var f = Vector3.Transform(Vector3.UnitZ, door.Rotation);
        float dx = toward.X - at.X, dz = toward.Z - at.Z;
        if (dx * dx + dz * dz < 1e-6f || f.X * f.X + f.Z * f.Z < 1e-6f)
        {
            return 0f;
        }
        return Wrapped((MathF.Atan2(dx, dz) - MathF.Atan2(f.X, f.Z)) * 180f / MathF.PI);
    }

    /// <summary>The turn about the vertical a pose faces (degrees from 0 up to 360, 0 along +z), as YawToFace's.</summary>
    public static float YawOf(Pose pose)
    {
        var f = pose.Forward;
        return Wrapped(MathF.Atan2(f.X, f.Z) * 180f / MathF.PI);
    }

    /// <summary>An angle in degrees brought into 0 up to 360.</summary>
    private static float Wrapped(float degrees) => ((degrees % 360f) + 360f) % 360f;

    /// <summary>
    /// What a location is called as an entrance, in the language in use and in English: a burial chamber, a Frost
    /// Cave... (its own name, the same in both, when it has no other).
    /// </summary>
    public static Said Describe(LocationPrefabs.Prefab p) => Array.Find(Locations, l => l.Name == p.Name).Description switch
    {
        Called.BurialChamber => Said.Of("dungeon.entrance.burialChamber"),
        Called.SunkenCrypt => Said.Of("dungeon.entrance.sunkenCrypt"),
        Called.FrostCave => Said.Of("dungeon.kind.frostCave"),
        Called.InfestedMine => Said.Of("dungeon.entrance.infestedMine"),
        Called.MorgenHole => Said.Of("dungeon.entrance.morgenHole"),
        _ => Said.Raw(p.Name),
    };
}
