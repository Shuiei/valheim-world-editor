using System.Numerics;
using TerrainEditor.Save;

namespace TerrainEditor.Editing;

/// <summary>
/// A dungeon's entrance on the ground: the game's location that leads into a dungeon of a kind (a burial chamber's
/// mound for a forest crypt, a Frost Cave's mouth, a sunken crypt's door...), and where the game puts the dungeon
/// and the door out of it for an entrance stood somewhere (ZoneSystem.SpawnLocation): straight above the entrance,
/// turned with it; or, for a location with a custom interior transform, at the middle of the entrance's zone, square
/// to the world, 5000 m over the entrance's ground.
/// </summary>
public static partial class DungeonEntrances
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
}
