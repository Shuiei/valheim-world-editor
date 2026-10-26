using System.Text.Json.Nodes;
using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;
using static WorldEditor.Tests.Unity.UnityField;

namespace WorldEditor.Tests;

/// <summary>
/// The draws each prefab's scripts make as a new object of it wakes (GameCatalogues.WakeScan, the "wake-draws" kind):
/// in the order they wake down the prefab, runs of numbers added up, the scripts of inactive objects below the root
/// left out, what depends on a script's fields or its object told from them, locations and prefabs that draw nothing
/// left out.
/// </summary>
public class GameCataloguesWakeScanTests
{
    /// <summary>A script without fields the scan reads.</summary>
    private static readonly UnityField[] NoFields = [];

    /// <summary>The scan of a game made of one bundle that <paramref name="build"/> fills.</summary>
    private static JsonObject Scan(Action<TestBundle> build)
    {
        using var game = new TestGame();
        build(game.Bundle("prefabs"));
        game.Save();
        return JsonNode.Parse(GameCatalogues.ReadAtStart(game.Bundles(), ["wake-draws"])["wake-draws"])!.AsObject();
    }

    /// <summary>A prefab's list as text, e.g. "wear,2,lod".</summary>
    private static string Listed(JsonObject scan, string prefab) =>
        string.Join(",", scan[prefab]!.AsArray().Select(t => t!.GetValueKind() == System.Text.Json.JsonValueKind.Object ? "loot" : t.ToString()));

    [Fact]
    public void ScriptsAreListedInTheOrderTheyWake()
    {
        var scan = Scan(b => b.Prefab("Hive", r =>
        {
            r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
            r.Mono("WearNTear", new Fields { ["m_health"] = 100.0 });
            r.Child("sound", c =>
            {
                c.Mono("ZSFX", fields: NoFields);
                c.Mono("LightFlicker", fields: NoFields);
            });
            r.Child("asleep", c =>
            {
                c.Active = false;
                c.Mono("Smoke", fields: NoFields);
            });
            r.Mono("LodFadeInOut");
            r.Component(UnityClass.LodGroup, UnityTypes.LodGroup);
        }));

        // The root's scripts first (its LodFadeInOut beside a LODGroup), then its children's; the inactive child's
        // smoke sleeps; the two sounds' draws added into one.
        Assert.Equal("wear,lod,2", Listed(scan, "Hive"));
    }

    [Fact]
    public void AnInactiveRootStillWakesAndLocationsAreLeftOut()
    {
        var scan = Scan(b =>
        {
            b.Prefab("Stone", r =>
            {
                r.Active = false;
                r.Mono("MineRock5");
                // Without a LODGroup it never draws.
                r.Mono("LodFadeInOut");
            });
            b.Prefab("Ruin", r =>
            {
                r.Mono("Location");
                r.Child("torch", c => c.Mono("LightFlicker", fields: NoFields));
            });
            b.Prefab("Plain", r => r.Mono("ZNetView", new Fields { ["m_persistent"] = true }));
        });

        Assert.Equal("1", Listed(scan, "Stone"));
        Assert.False(scan.ContainsKey("Ruin"));
        Assert.False(scan.ContainsKey("Plain"));
    }

    [Fact]
    public void WhatDependsOnFieldsIsToldFromThem()
    {
        UnityField[] pickable = [Of("float", "m_respawnTimeMinutes")], monster = [Of("float", "m_wakeUpDelayMin"), Of("float", "m_wakeUpDelayMax")];
        UnityField[] vine = [Of("int", "m_forceSeed")], names = [List("m_randomStartingName", Text("data"))];
        UnityField[] item = [Ref("m_itemPrefab"), List("m_randomItemPrefabs", Struct("RandomItem", "data", Ref("m_itemPrefab")))];
        var scan = Scan(b =>
        {
            b.Prefab("Berries", r =>
            {
                r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
                r.Mono("Pickable", new Fields { ["m_respawnTimeMinutes"] = 300.0 }, fields: pickable);
                // Without a network view of its own, a child's pickable draws nothing.
                r.Child("stalk", c => c.Mono("Pickable", new Fields { ["m_respawnTimeMinutes"] = 300.0 }, fields: pickable));
            });
            b.Prefab("Troll", r =>
            {
                r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
                r.Mono("MonsterAI", new Fields { ["m_wakeUpDelayMax"] = 3.0 }, fields: monster);
                r.Mono("Tameable", new Fields { ["m_randomStartingName"] = new List<object?> { "Bob" } }, fields: names);
            });
            b.Prefab("Creeper", r =>
            {
                r.Mono("Vine", fields: vine);
                r.Mono("Radiator", enabled: false, fields: NoFields);
            });
            b.Prefab("Food", r =>
            {
                r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
                r.Mono("PickableItem", new Fields { ["m_randomItemPrefabs"] = new List<Fields> { new() { ["m_itemPrefab"] = Fields.Ref(7) } } }, fields: item);
            });
        });

        Assert.Equal("1", Listed(scan, "Berries"));
        // MonsterAI four and its wake-up delay, Tameable a starting name.
        Assert.Equal("6", Listed(scan, "Troll"));
        // Vine without a seed of its own three; the disabled Radiator none.
        Assert.Equal("3", Listed(scan, "Creeper"));
        Assert.Equal("2", Listed(scan, "Food"));
    }

    [Fact]
    public void AContainersDefaultItemsAndASnowMelterAreKept()
    {
        UnityField[] chest = [Struct("DropTable", "m_defaultItems", List("m_drops", Struct("DropData", "data", Of("float", "m_weight"))), Of("int", "m_dropMin"), Of("int", "m_dropMax"), Of("float", "m_dropChance"), Of("bool", "m_oneOfEach", align: true))];
        var scan = Scan(b =>
        {
            b.Prefab("Chest", r =>
            {
                r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
                r.Mono("Container", new Fields
                {
                    ["m_defaultItems"] = new Fields
                    {
                        ["m_drops"] = new List<Fields> { new() { ["m_weight"] = 1.0 }, new() { ["m_weight"] = 0.5 } },
                        ["m_dropMin"] = 2,
                        ["m_dropMax"] = 3,
                        ["m_dropChance"] = 0.75,
                        ["m_oneOfEach"] = true,
                    },
                }, fields: chest);
            });
            b.Prefab("EmptyChest", r =>
            {
                r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
                r.Mono("Container", fields: chest);
            });
            b.Prefab("Firepit", r =>
            {
                r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
                r.Mono("Fireplace", new Fields { ["m_snowMelter"] = Fields.Ref(9) }, fields: [Ref("m_snowMelter")]);
            });
        });

        var loot = scan["Chest"]![0]!;
        Assert.Equal(0.75, loot["chance"]!.GetValue<double>());
        Assert.Equal((2L, 3L), (loot["min"]!.GetValue<long>(), loot["max"]!.GetValue<long>()));
        Assert.True(loot["each"]!.GetValue<bool>());
        Assert.Equal([1.0, 0.5], loot["weights"]!.AsArray().Select(w => w!.GetValue<double>()));
        Assert.False(scan.ContainsKey("EmptyChest"));
        Assert.Equal("melt", Listed(scan, "Firepit"));
    }
}
