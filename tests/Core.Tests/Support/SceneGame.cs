using WorldEditor.Tests.Unity;

namespace WorldEditor.Tests;

/// <summary>
/// A made-up game's main scene, as the readers of its rules find it (the SoftRef manifest names the scene's bundle and
/// the bundles it needs): ZoneSystem's vegetation and locations, LocationLists, the alt biomes' extra locations, and
/// the vegetation's prefabs with the scripts that draw random numbers when they wake.
/// </summary>
public static class SceneGame
{
    /// <summary>
    /// The scene's bundle, the bundle it needs, and the prefabs' bundle.
    /// </summary>
    public const string Main = "scene_main", Shared = "scene_shared", Plants = "plants";

    /// <summary>
    /// The asset id the manifest gives the start temple ("StartTemple"), its four parts as written in a rule.
    /// </summary>
    public static readonly uint[] TempleId = [0x11111111, 0x22222222, 0x33333333, 0x44444444];

    /// <summary>
    /// A location rule's values, its prefab by asset id (none: all zeros, named by m_prefabName).
    /// </summary>
    public static Fields Location(string name, int quantity, uint[]? id = null, bool prioritized = false) => new()
    {
        ["m_enable"] = true,
        ["m_name"] = name,
        ["m_prefabName"] = name + "_old",
        ["m_prefab"] = new Fields { ["m_assetID"] = new Fields { ["v0"] = id?[0] ?? 0u, ["v1"] = id?[1] ?? 0u, ["v2"] = id?[2] ?? 0u, ["v3"] = id?[3] ?? 0u } },
        ["m_biome"] = 1,
        ["m_quantity"] = quantity,
        ["m_prioritized"] = prioritized,
        ["m_group"] = "g",
        ["m_minDistanceFromSimilar"] = 128.0,
        ["m_exteriorRadius"] = 12.5,
        ["m_minAltitude"] = 1.0,
        ["m_maxAltitude"] = 1000.0,
    };

    /// <summary>
    /// The game: the scene with ZoneSystem (a vegetation rule of each plant, two locations), a smaller ZoneSystem in
    /// the shared bundle (not the main one), a LocationList for each (locations, vegetation) count of
    /// <paramref name="lists"/> (each list's vegetation rules on the first plant) plus one with no location, and the
    /// alt biomes.
    /// </summary>
    public static TestGame Make((int Locations, int Vegetation)[] lists)
    {
        var game = new TestGame();
        var plants = game.Bundle(Plants);
        long bush = plants.Prefab("Bush", r =>
        {
            r.Mono("LodFadeInOut");
            r.Component(UnityClass.LodGroup, UnityTypes.LodGroup);
            r.Child("bird", c => c.Mono("RandomFlyingBird"));
            r.Child("asleep", c =>
            {
                c.Active = false;
                c.Mono("MineRock5");
            });
        }).Go;
        long rock = plants.Prefab("Rock", r =>
        {
            r.Active = false;
            r.Mono("MineRock5");
            r.Mono("LodFadeInOut");
        }).Go;
        long berries = plants.Prefab("Berries", r =>
        {
            r.Mono("Pickable", new Fields { ["m_respawnTimeMinutes"] = 300.0 });
            r.Child("stalk", c => c.Mono("Pickable"));
        }).Go;

        var main = game.Bundle(Main);
        Fields Rule(TestBundle b, string name, long prefab) => new()
        {
            ["m_name"] = name,
            ["m_prefab"] = b.RefTo(plants, prefab),
            ["m_enable"] = true,
            ["m_min"] = 1.0,
            ["m_max"] = 3.0,
            ["m_biome"] = 1,
        };
        long zone = main.Reserve();
        main.Assets.Add(UnityClass.GameObject, UnityTypes.GameObject, new Fields { ["m_Name"] = "_ZoneSystem", ["m_IsActive"] = true }, pathId: zone);
        main.Mono(zone, "ZoneSystem", new Fields
        {
            ["m_zoneSize"] = 64.0,
            ["m_vegetation"] = new List<Fields> { Rule(main, "Bushes", bush), Rule(main, "Rocks", rock), Rule(main, "Berries", berries), Rule(main, "Lost", 999) },
            ["m_locations"] = new List<Fields> { Location("StartTemple", 1, TempleId, prioritized: true), Location("Crypt", 50) },
        });
        main.WithContents = false;

        var shared = game.Bundle(Shared);
        shared.WithContents = false;
        long holder = shared.Reserve();
        shared.Assets.Add(UnityClass.GameObject, UnityTypes.GameObject, new Fields { ["m_Name"] = "_Lists", ["m_IsActive"] = true }, pathId: holder);
        shared.Mono(holder, "ZoneSystem", new Fields { ["m_zoneSize"] = 64.0, ["m_locations"] = new List<Fields> { Location("Small", 1) } });
        int order = lists.Length;
        foreach (var (locations, vegetation) in lists)
        {
            shared.Mono(holder, "LocationList", new Fields
            {
                ["m_sortOrder"] = order--,
                ["m_locations"] = Enumerable.Range(0, locations).Select(i => Location($"L{locations}x{vegetation}_{i}", 2)).ToList(),
                ["m_vegetation"] = Enumerable.Range(0, vegetation).Select(i => Rule(shared, $"V{locations}x{vegetation}_{i}", bush)).ToList(),
            });
        }
        shared.Mono(holder, "LocationList", new Fields { ["m_sortOrder"] = 99, ["m_vegetation"] = new List<Fields> { Rule(shared, "Extra", rock) } });
        shared.Mono(holder, "WorldGenerator", new Fields
        {
            ["m_alts"] = new List<Fields>
            {
                new() { ["m_name"] = "DeepNorth_Ice", ["m_blockLocationNames"] = new List<object> { "Crypt" }, ["m_addLocations"] = new List<Fields> { Location("IceCave", 3) } },
            },
        });
        game.Manifest("aaaabbbbccccddddeeeeffff00001111", Main, "Assets/Scenes/main.unity");
        game.Manifest("44444444333333332222222211111111", Plants, "Assets/Locations/StartTemple.prefab");
        game.Needs(Main, Shared, "missing_bundle");
        return game;
    }
}
