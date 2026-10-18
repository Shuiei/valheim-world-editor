using System.Text.Json.Nodes;
using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;
using static WorldEditor.Tests.Unity.UnityField;

namespace WorldEditor.Tests;

/// <summary>
/// The spawners among the game's prefabs, as the catalogue reads them (GameCatalogues.PrefabScan.ReadSpawn): a creature
/// spawner's respawn time, levels, day and night and its creature; a trigger spawner's levels and creatures; a nest's
/// radius, limit and what it spawns.
/// </summary>
public class GameCataloguesPrefabScanComponentsTests
{
    [Fact]
    public void SpawnersTriggersAndNestsTellWhatTheyMakeAndWhen()
    {
        using var game = new TestGame();
        var b = game.Bundle("spawns");
        static void Net(PrefabNode r) => r.Mono("ZNetView", new Fields { ["m_persistent"] = true });
        var boar = b.Prefab("Boar", r =>
        {
            Net(r);
            r.Mono("Character", new Fields { ["m_runSpeed"] = 5.0 });
        });
        var neck = b.Prefab("Neck", r =>
        {
            Net(r);
            r.Mono("Character", new Fields { ["m_runSpeed"] = 4.0 });
        });
        b.Prefab("Spawner_Boar", r =>
        {
            Net(r);
            // The scripts the made-up game does not know: their fields given with them, in every width of whole number
            // and both floating-point widths a script may use (each read as a number).
            r.Mono("CreatureSpawner", new Fields
            {
                ["m_creaturePrefab"] = Fields.Ref(boar.Go),
                ["m_respawnTimeMinuts"] = 20.0,
                ["m_minLevel"] = 1,
                ["m_maxLevel"] = 3,
                ["m_spawnAtDay"] = true,
                ["m_spawnAtNight"] = 0,
            }, fields: [Ref("m_creaturePrefab"), Of("float", "m_respawnTimeMinuts"), Of("SInt16", "m_minLevel"), Of("UInt16", "m_maxLevel"), Of("bool", "m_spawnAtDay"), Of("SInt8", "m_spawnAtNight", align: true)]);
        });
        b.Prefab("Trigger_Pack", r =>
        {
            Net(r);
            r.Mono("TriggerSpawner", new Fields
            {
                ["m_creaturePrefabs"] = new List<Fields> { Fields.Ref(boar.Go), Fields.Ref(neck.Go) },
                ["m_spawnChance"] = 50.0,
                ["m_minLevel"] = 2,
                ["m_maxLevel"] = 2,
            }, fields: [List("m_creaturePrefabs", Ref("data")), Of("float", "m_spawnChance"), Of("SInt64", "m_minLevel"), Of("UInt64", "m_maxLevel")]);
        });
        b.Prefab("Nest_Neck", r =>
        {
            Net(r);
            r.Mono("SpawnArea", new Fields
            {
                ["m_prefabs"] = new List<Fields> { new() { ["m_prefab"] = Fields.Ref(neck.Go), ["m_weight"] = 1.0 } },
                ["m_spawnIntervalSec"] = 10.0,
                ["m_spawnRadius"] = 4.0,
                ["m_maxNear"] = 3,
            }, fields: [List("m_prefabs", Struct("SpawnData", "data", Ref("m_prefab"), Of("float", "m_weight"))), Of("float", "m_spawnIntervalSec"), Of("double", "m_spawnRadius"), Of("UInt8", "m_maxNear", align: true)]);
        });
        game.Save();

        var prefabs = JsonNode.Parse(GameCatalogues.ReadAtStart(game.Bundles(), ["prefabs"])["prefabs"])!.AsObject();

        var spawner = prefabs["Spawner_Boar"]!.AsObject();
        Assert.Equal(20.0, (double)spawner["rt"]!);
        Assert.Equal("[1,3]", spawner["lv"]!.ToJsonString());
        Assert.Equal((1L, 0L), ((long)spawner["day"]!, (long)spawner["night"]!));
        Assert.Equal("[\"Boar\"]", spawner["cs"]!.ToJsonString());
        var trigger = prefabs["Trigger_Pack"]!.AsObject();
        Assert.Equal(1L, (long)trigger["trig"]!);
        Assert.Equal("[2,2]", trigger["lv"]!.ToJsonString());
        Assert.Equal("[\"Boar\",\"Neck\"]", trigger["cs"]!.ToJsonString());
        var nest = prefabs["Nest_Neck"]!.AsObject();
        Assert.Equal(4.0, (double)nest["sr"]!);
        Assert.Equal(3L, (long)nest["max"]!);
        Assert.Equal("[\"Neck\"]", nest["sa"]!.ToJsonString());
    }
}
