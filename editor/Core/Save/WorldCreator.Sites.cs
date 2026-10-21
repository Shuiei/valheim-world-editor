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
    /// <param name="Position">
    /// Where the zone file has it, world metres (the water's level when its kind snaps to the water); it spawns at
    /// <see cref="GroundHeight"/>.
    /// </param>
    /// <param name="Objects">
    /// What is written for it (its networked objects, then its proxy), each with where it is; null when its zone is left
    /// to the game.
    /// </param>
    /// <param name="Ground">
    /// Its zone's ground as Expand World Data levels and paints it round the location (and its blueprint's snapshots);
    /// null for none.
    /// </param>
    internal sealed record Site((int X, int Z) Zone, string Name, int Hash, Vector3 Position, List<(Vector3 At, byte[] Zdo)>? Objects, ZoneEdit? Ground = null);

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
    /// <param name="grounds">
    /// Where the zones whose ground the locations change are gathered (a zone two of them change, changed by both);
    /// null when the caller has no need of them.
    /// </param>
    internal static List<Site> Sites(ValheimGen.TerrainService terrain, int seed, int radius, List<ValheimGen.SeedLocations.Placed>? placed = null,
        Dictionary<(int X, int Z), ZoneEdit>? grounds = null)
    {
        grounds ??= [];
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
                // One whose changed contents (a blueprint location's included) the editor cannot build, or one spawned with
                // a random seed: its zone is left to the game, like a dungeon's.
                var contents = p.Rule.Contents ?? ValheimGen.LocationContents.None;
                if (!Builds(p.Rule, contents, terrain, out var prefab))
                {
                    sites.Add(new Site(zone, p.Rule.Prefab, hash, position, null));
                    continue;
                }
                int zoneSeed = LocationPrefabs.ZoneSeed(seed, zone.X, zone.Z);
                var spawn = p.Rule.SnapToWater != 0 ? position : position with { Y = GroundHeight(terrain, position) };
                float yaw = contents.RandomCardinal ? CardinalYaw(zoneSeed) : Yaw(p.Rule, terrain, spawn, zoneSeed);
                // Expand World Data's offset raises the spot before the game makes the location and its proxy.
                var spot = spawn with { Y = spawn.Y + contents.Offset };
                // The mod changes the ground before it spawns the file's objects: levelled and painted, then a blueprint's
                // terrain snapshots.
                var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw * MathF.PI / 180f);
                Gathered(grounds, GroundOf(contents, spawn, zone, terrain.Generator));
                ApplySnapshots(grounds, contents, spot, turn, terrain.Generator);
                var ground = grounds.GetValueOrDefault(zone);
                var levelled = LevelledOf(ground, terrain, StableHash.Of(p.Rule.Base), spot, yaw);
                var objects = Built(prefab, contents, new Placing(spawn, turn, zoneSeed, DamageOf(p.Rule, prefab), levelled), terrain);
                objects.Add((spot, Proxy(p.Rule, proxyFlags, spot, yaw, zoneSeed, terrain.Generator.Rules.DataInstalled)));
                sites.Add(new Site(zone, p.Rule.Prefab, hash, position, objects, ground));
            }
        }
        finally
        {
            Rnd.State = was;
        }
        return sites;
    }

    /// <summary>
    /// A location's changed zone gathered with the others: kept as it is when no other location changed that zone, else
    /// its changed vertices laid over the zone's (the later location's ground over the earlier's); nothing for none.
    /// </summary>
    private static void Gathered(Dictionary<(int X, int Z), ZoneEdit> grounds, ZoneEdit? ground)
    {
        if (ground == null)
        {
            return;
        }
        if (!grounds.TryAdd((ground.ZoneX, ground.ZoneZ), ground))
        {
            LayOver(grounds[(ground.ZoneX, ground.ZoneZ)], ground);
        }
    }

    /// <summary>A zone's changed vertices (height and paint) laid over another's of the same zone.</summary>
    private static void LayOver(ZoneEdit earlier, ZoneEdit ground)
    {
        for (int i = 0; i < EditStore.Cells; i++)
        {
            if (ground.Modified[i])
            {
                (earlier.Modified[i], earlier.Level[i], earlier.Smooth[i]) = (true, ground.Level[i], ground.Smooth[i]);
            }
            if (ground.PaintModified[i])
            {
                earlier.PaintModified[i] = true;
                Array.Copy(ground.Paint, i * 4, earlier.Paint, i * 4, 4);
            }
        }
    }

    /// <summary>
    /// The ground under a point as the game finds it while it spawns a location and the objects its file snaps
    /// (ZoneSystem.PlaceLocations, GetGroundHeight): on the zone's terrain as generated, between the terrain's points
    /// as its mesh is; not the generator's own height there.
    /// </summary>
    private static float GroundHeight(ValheimGen.TerrainService terrain, Vector3 at)
    {
        var (zx, zz) = ChunkMath.SectorOf(at.X, at.Z);
        return App.Regrow.GridHeight(terrain.BaseZone(zx, zz), EditStore.Grid, zx * 64f - 32f, zz * 64f - 32f, at.X, at.Z);
    }

    /// <summary>
    /// Whether New world builds a kind's locations, with the location prefab it is made of (null for a blueprint
    /// location, made of its blueprint): not when its zone is left to the game, nor for a location with an interior or
    /// one the game's files do not describe.
    /// </summary>
    private static bool Builds(ValheimGen.LocationRule rule, ValheimGen.LocationContents contents, ValheimGen.TerrainService terrain, out LocationPrefabs.Prefab? prefab)
    {
        prefab = contents.Blueprint == null ? LocationPrefabs.Get(rule.Base) : null;
        return !LeftToGame(rule, terrain) && (contents.Blueprint != null || prefab is { Interior: false });
    }

    /// <summary>
    /// Whether a kind's zone is left to the game: one whose changed contents (a blueprint location's included) the
    /// editor cannot build, or any location when Expand World Data spawns them with a random seed.
    /// </summary>
    private static bool LeftToGame(ValheimGen.LocationRule rule, ValheimGen.TerrainService terrain) =>
        ((rule.Blueprint == true || rule.Custom == true) && rule.Contents is not { LeftToGame: null }) || terrain.Generator.Rules.RandomLocations;

    /// <summary>
    /// A location's proxy, turned with it, with its ints and the file's data for it (<see cref="ProxyInts"/>). A blueprint
    /// location's is the mod's own (CreateBlueprintProxy): unturned, with only its name as its "locationreference" (the
    /// game has no location of it) and no data.
    /// </summary>
    private static byte[] Proxy(ValheimGen.LocationRule rule, ushort flags, Vector3 spot, float yaw, int zoneSeed, bool dataInstalled)
    {
        if (rule.Contents?.Blueprint != null)
        {
            return ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, flags, spot, Vector3.Zero, 0f, [(LocationReferenceKey, StableHash.Of(rule.Prefab))]);
        }
        byte[] proxy = ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, flags, spot, new Vector3(0f, yaw, 0f), 0f, ProxyInts(rule, zoneSeed, dataInstalled));
        return WithFields(proxy, rule.Contents?.ProxyData);
    }

    /// <summary>
    /// A location proxy's ints: the location it spawns and the zone's seed. With Expand World Data, the location is the
    /// game prefab a clone is made of (the mod's LocationProxy.SetLocation prefix: the game spawns the prefab's look),
    /// with the clone's own name as its "locationreference" (for the mods that look it up), set first as the mod sets it.
    /// </summary>
    private static List<(int Key, int Value)> ProxyInts(ValheimGen.LocationRule rule, int zoneSeed, bool dataInstalled) =>
        dataInstalled
            ? [(LocationReferenceKey, StableHash.Of(rule.Prefab)), (WorldSave.LocationKey, StableHash.Of(rule.Base)), (SeedKey, zoneSeed)]
            : [(WorldSave.LocationKey, StableHash.Of(rule.Prefab)), (SeedKey, zoneSeed)];

    /// <summary>The data key Expand World Data keeps a proxy's own location name under.</summary>
    private static readonly int LocationReferenceKey = StableHash.Of("locationreference");

    /// <summary>
    /// The random initial damage a kind's pieces wake with: Expand World Data's file says ("true", "all"; anything
    /// else none), else the prefab's own (Location.m_applyRandomDamage).
    /// </summary>
    private static Damage DamageOf(ValheimGen.LocationRule rule, LocationPrefabs.Prefab? prefab) => rule.RandomDamage switch
    {
        null => prefab?.RandomDamage == true ? Damage.Random : Damage.None,
        "true" => Damage.Random,
        "all" => Damage.RandomOverData,
        _ => Damage.None,
    };
}
