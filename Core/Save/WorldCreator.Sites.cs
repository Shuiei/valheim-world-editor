using System.Numerics;
using TerrainEditor.App;
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
    /// <param name="Clear">How far around it the vegetation is cleared (0: not cleared).</param>
    /// <param name="Objects">
    /// What is written for it (its networked objects, then its proxy), each with where it is; null when its zone is left
    /// to the game.
    /// </param>
    internal sealed record Site((int X, int Z) Zone, string Name, int Hash, Vector3 Position, float Clear, List<(Vector3 At, byte[] Zdo)>? Objects);

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
    internal static List<Site> Sites(ValheimGen.TerrainService terrain, int seed, int radius)
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
            foreach (var p in ValheimGen.SeedLocations.Place(terrain.Generator, ValheimGen.SeedLocations.Which.All))
            {
                var zone = ChunkMath.SectorOf(p.X, p.Z);
                if (Math.Abs(zone.X) > radius || Math.Abs(zone.Z) > radius)
                {
                    continue;
                }
                int hash = StableHash.Of(p.Rule.Prefab);
                // Snapped to the game's literal 30 (ZoneSystem.PlaceLocations), whatever the rules put the water at.
                var position = new Vector3(p.X, p.Rule.SnapToWater != 0 ? ValheimGen.WorldGenerator.LiteralWaterLevel : p.Y, p.Z);
                // A blueprint location, one whose contents Expand World Data changes, or one spawned with a random seed:
                // its zone is left to the game, like a dungeon's.
                bool unknown = p.Rule.Blueprint == true || p.Rule.Custom == true || terrain.Generator.Rules.RandomLocations;
                if (unknown || LocationPrefabs.Get(p.Rule.Base) is not { Interior: false } prefab)
                {
                    sites.Add(new Site(zone, p.Rule.Prefab, hash, position, 0f, null));
                    continue;
                }
                int zoneSeed = LocationPrefabs.ZoneSeed(seed, zone.X, zone.Z);
                float yaw = Yaw(p.Rule, terrain, position, zoneSeed);
                var objects = new List<(Vector3 At, byte[] Zdo)>();
                foreach (var m in LocationPrefabs.Made(prefab, position, Quaternion.CreateFromAxisAngle(Vector3.UnitY, yaw * MathF.PI / 180f), zoneSeed))
                {
                    int made = StableHash.Of(m.Prefab);
                    if (Terrain.PrefabCatalog.Get(made) is { } info)
                    {
                        objects.Add((m.Position, ZdoBuilder.Blank(made, info.Flags, m.Position, BlueprintFormats.ToEuler(m.Rotation), 0f)));
                    }
                }
                objects.Add((position, ZdoBuilder.Blank(WorldSave.LocationProxyPrefab, proxyFlags, position, new Vector3(0f, yaw, 0f), 0f, [(WorldSave.LocationKey, hash), (SeedKey, zoneSeed)])));
                sites.Add(new Site(zone, p.Rule.Prefab, hash, position, prefab.ClearArea ? p.Rule.ExteriorRadius : 0f, objects));
            }
        }
        finally
        {
            Rnd.State = was;
        }
        return sites;
    }
}
