using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// How a dungeon's spawners and chests are thinned or added (DungeonGen.Items): spawners stand the monster count of times
/// (or the settings' foes), and the loot keeps or adds chests beside the rooms' own.
/// </summary>
public class DungeonGenSpawnerAndChestTests
{
    private static List<DungeonGen.Item> Spawners(params string[] prefabs) =>
        [.. prefabs.Select(p => new DungeonGen.Item(p, Vector3.Zero, Quaternion.Identity))];

    [Fact]
    public void AdjustSpawnersMakesTheMonsterCountOfEachSpawner()
    {
        // Two monsters a spawner: its spawner stands twice, the first where it was.
        var items = Spawners("Spawner_Greydwarf");
        PrivateCall.Static(typeof(DungeonGen), "AdjustSpawners", items, new DungeonGen.Settings(Monsters: 2), new Random(1));
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal("Spawner_Greydwarf", i.Prefab));
        Assert.Equal(Vector3.Zero, items[0].Position);
    }

    [Fact]
    public void AdjustSpawnersTakesTheFoesWhenTheSettingsChooseThem()
    {
        var items = Spawners("Spawner_Greydwarf");
        PrivateCall.Static(typeof(DungeonGen), "AdjustSpawners", items, new DungeonGen.Settings(Monsters: 1, Foes: ["Goblin"]), new Random(1));
        var only = Assert.Single(items);
        Assert.Equal("Goblin", only.Prefab);
    }

    [Fact]
    public void AdjustSpawnersRemovesEverySpawnerWithNoMonsters()
    {
        var items = Spawners("Spawner_Greydwarf", "Spawner_Skeleton");
        PrivateCall.Static(typeof(DungeonGen), "AdjustSpawners", items, new DungeonGen.Settings(Monsters: 0), new Random(1));
        Assert.Empty(items);
    }

    [Fact]
    public void AdjustChestsAddsAChestBesideEachWhenTheLootIsAboveOne()
    {
        // With twice the loot every chest gains a copy 1.2 m along its own x axis.
        var items = new List<DungeonGen.Item> { new("TreasureChest_wood", Vector3.Zero, Quaternion.Identity) };
        PrivateCall.Static(typeof(DungeonGen), "AdjustChests", items, new DungeonGen.Settings(Loot: 2), new Random(1));
        Assert.Equal(2, items.Count);
        Assert.InRange(items[1].Position.X, 1.1999f, 1.2001f);
    }

    [Fact]
    public void AdjustChestsRemovesEveryChestWithNoLootAndKeepsTheRest()
    {
        var items = new List<DungeonGen.Item>
        {
            new("TreasureChest_wood", Vector3.Zero, Quaternion.Identity),
            new("crate", Vector3.Zero, Quaternion.Identity),
        };
        PrivateCall.Static(typeof(DungeonGen), "AdjustChests", items, new DungeonGen.Settings(Loot: 0), new Random(1));
        Assert.Equal("crate", Assert.Single(items).Prefab);
    }

    [Fact]
    public void AdjustChestsChangesNothingAtTheUsualLoot()
    {
        var items = new List<DungeonGen.Item> { new("TreasureChest_wood", Vector3.Zero, Quaternion.Identity) };
        PrivateCall.Static(typeof(DungeonGen), "AdjustChests", items, new DungeonGen.Settings(Loot: 1), new Random(1));
        Assert.Single(items);
    }
}
