using System.Numerics;
using TerrainEditor.Editing;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.Save;

public static partial class WorldCreator
{
    /// <summary>A location of the seed in a zone to generate.</summary>
    /// <param name="Zone">Its zone.</param>
    /// <param name="Name">Its kind's prefab name.</param>
    /// <param name="Hash">The name's stable hash: its proxy's location, the zone file's entry.</param>
    /// <param name="Position">Where it stands, world metres (the water's level when its kind snaps to the water).</param>
    /// <param name="Objects">
    /// What is written for it (its networked objects, then its proxy), each with where it is; null when its zone is left
    /// to the game.
    /// </param>
    internal sealed record Site((int X, int Z) Zone, string Name, int Hash, Vector3 Position, List<(Vector3 At, byte[] Zdo)>? Objects);

    /// <summary>
    /// The seed's locations within <paramref name="radius"/> zones of the middle, as the game would spawn them when it
    /// generates their zones (ZoneSystem.PlaceLocations and SpawnLocation): laid out by SeedLocations (none when the
    /// game's rules are not read), stood on the generator's ground or the water, turned along the slope or at random
    /// from the zone's seed as their kind asks, with their networked objects (those their random parts keep, rolled
    /// from that seed) and a location proxy. A location with an interior (a dungeon's entrance: the game lays out its
    /// rooms itself) or one the game's files do not describe has no objects: its zone is left to the game.
    /// </summary>
    /// <param name="terrain">The new world's terrain (its generator).</param>
    /// <param name="seed">The world's seed number.</param>
    /// <param name="radius">How far from the middle the zones are generated.</param>
    /// <param name="placed">The seed's laid out locations, when the caller has them; else laid out here.</param>
    internal static List<Site> Sites(ValheimGen.TerrainService terrain, int seed, int radius, List<ValheimGen.SeedLocations.Placed>? placed = null)
    {
        var sites = new List<Site>();
        if (!ValheimGen.SeedLocations.AvailableFor(terrain.Generator))
        {
            return sites;
        }
        ushort proxyFlags = Terrain.PrefabCatalog.Get(WorldSave.LocationProxyPrefab)?.Flags ?? 0x0B00;
        Rnd.RandomState was = Rnd.State;
        try
        {
            foreach (var p in placed ?? ValheimGen.SeedLocations.Place(terrain.Generator, ValheimGen.SeedLocations.Which.All))
            {
                var zone = ChunkMath.SectorOf(p.X, p.Z);
                if (Math.Abs(zone.X) > radius || Math.Abs(zone.Z) > radius)
                {
                    continue;
                }
                int hash = StableHash.Of(p.Rule.Prefab);
                // Snapped to the game's literal 30 (ZoneSystem.PlaceLocations), whatever the rules put the water at.
                var position = new Vector3(p.X, p.Rule.SnapToWater != 0 ? ValheimGen.WorldGenerator.LiteralWaterLevel : p.Y, p.Z);
                // A blueprint location, one whose changed contents the editor cannot build, or one spawned with a random
                // seed: its zone is left to the game, like a dungeon's.
                var contents = p.Rule.Contents ?? ValheimGen.LocationContents.None;
                if (LeftToGame(p.Rule, terrain) || LocationPrefabs.Get(p.Rule.Base) is not { Interior: false } prefab)
                {
                    sites.Add(new Site(zone, p.Rule.Prefab, hash, position, null));
                    continue;
                }
                int zoneSeed = LocationPrefabs.ZoneSeed(seed, zone.X, zone.Z);
                float yaw = contents.RandomCardinal ? CardinalYaw(zoneSeed) : Yaw(p.Rule, terrain, position, zoneSeed);
                var objects = Built(prefab, contents, new Placing(position, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw * MathF.PI / 180f), zoneSeed, DamageOf(p.Rule, prefab)), terrain);
                // Expand World Data's offset raises the spot before the game makes the proxy too.
                var spot = position with { Y = position.Y + contents.Offset };
                byte[] proxy = ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, proxyFlags, spot, new Vector3(0f, yaw, 0f), 0f, ProxyInts(p.Rule, zoneSeed, terrain.Generator.Rules.DataInstalled));
                objects.Add((spot, WithFields(proxy, contents.ProxyData)));
                sites.Add(new Site(zone, p.Rule.Prefab, hash, position, objects));
            }
        }
        finally
        {
            Rnd.State = was;
        }
        return sites;
    }

    /// <summary>
    /// Whether a kind's zone is left to the game: a blueprint location, one whose changed contents the editor cannot
    /// build, or any location when Expand World Data spawns them with a random seed.
    /// </summary>
    private static bool LeftToGame(ValheimGen.SeedLocations.Rule rule, ValheimGen.TerrainService terrain) =>
        rule.Blueprint == true || (rule.Custom == true && rule.Contents is not { LeftToGame: null }) || terrain.Generator.Rules.RandomLocations;

    /// <summary>
    /// A location proxy's ints: the location it spawns and the zone's seed. With Expand World Data, the location is the
    /// game prefab a clone is made of (the mod's LocationProxy.SetLocation prefix: the game spawns the prefab's look),
    /// with the clone's own name as its "locationreference" (for the mods that look it up), set first as the mod sets it.
    /// </summary>
    private static List<(int Key, int Value)> ProxyInts(ValheimGen.SeedLocations.Rule rule, int zoneSeed, bool dataInstalled) =>
        dataInstalled
            ? [(LocationReferenceKey, StableHash.Of(rule.Prefab)), (WorldSave.LocationKey, StableHash.Of(rule.Base)), (SeedKey, zoneSeed)]
            : [(WorldSave.LocationKey, StableHash.Of(rule.Prefab)), (SeedKey, zoneSeed)];

    /// <summary>The data key Expand World Data keeps a proxy's own location name under.</summary>
    private static readonly int LocationReferenceKey = StableHash.Of("locationreference");

    /// <summary>
    /// The random initial damage a kind's pieces wake with: Expand World Data's file says ("true", "all"; anything
    /// else none), else the prefab's own (Location.m_applyRandomDamage).
    /// </summary>
    private static Damage DamageOf(ValheimGen.SeedLocations.Rule rule, LocationPrefabs.Prefab prefab) => rule.RandomDamage switch
    {
        null => prefab.RandomDamage ? Damage.Random : Damage.None,
        "true" => Damage.Random,
        "all" => Damage.RandomOverData,
        _ => Damage.None,
    };
}
