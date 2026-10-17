using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;
using static WorldEditor.Tests.DungeonGenSupport;

namespace WorldEditor.Tests;

/// <summary>
/// Generated dungeons (DungeonGen) in the sky: a dungeon goes above what is already up there (a rock, a crypt's entrance, a
/// cave), its arrival stands on the entrance's floor, its key chest holds the key, and its levels are all made down to the boss.
/// </summary>
public class DungeonGenSkyTests
{
    [Fact]
    public void ItGoesHigherThanWhatIsAlreadyUpThere()
    {
        var r = Make("Crypt", "Swamp", 2, 1, 4);
        var origin = new Vector3(100, 5050, 100);
        Assert.Equal(origin, DungeonGen.Clear(origin, r, DungeonGen.Taken([], [])));
        // A crypt's entrance nearby that nobody entered yet: its dungeon will come 5000 m above it.
        var crypt = (new Vector3(130, 40, 120), StableHash.Of("Crypt3"));
        var raised = DungeonGen.Clear(origin, r, DungeonGen.Taken([], [crypt]));
        Assert.True(raised.Y + r.Items.Min(i => i.Position.Y) - 6 >= 40 + 5250 - 0.01f, $"raised to {raised.Y}");
        // A cave already laid out: its dungeon object keeps the space of its rooms, not just its point.
        var cave = (StableHash.Of("DG_Cave"), new Vector3(150, 5050, 140));
        Assert.True(DungeonGen.Clear(origin, r, DungeonGen.Taken([cave], [])).Y > origin.Y);
        // Other locations take nothing.
        Assert.Equal(origin, DungeonGen.Clear(origin, r, DungeonGen.Taken([], [(new Vector3(130, 40, 120), StableHash.Of("StoneTower1"))])));
    }

    [Fact]
    public void TheKeyChestHoldsTheKey()
    {
        var r = Make("Crypt", "Swamp", 2, 2, 7);
        var chest = r.Items.Single(i => i.Data?.Any(d => d.Key == "items") == true);
        var items = InventoryData.Read(Convert.FromBase64String(chest.Data!.Single(d => d.Key == "items").Value));
        Assert.Contains(items.Items, i => i.Prefab == StableHash.Of("CryptKey"));
        Assert.Contains(chest.Data!, d => d.Key == "addedDefaultItems" && d.Value == "1");
        Assert.Contains(r.Items, i => i.Prefab == "sunken_crypt_gate");
    }

    [Fact]
    public void AnythingElseHighUpTakesItsOwnSpotAndAnEmptyDungeonStaysPut()
    {
        var r = Make("Crypt", "Swamp", 2, 1, 4);
        var origin = new Vector3(100, 5000, 100);
        // A rock (not a dungeon) right where the dungeon would go: it goes higher.
        var taken = DungeonGen.Taken([(StableHash.Of("Rock_3"), origin)], []);
        Assert.Equal(new DungeonGen.Box(origin - Vector3.One, origin + Vector3.One), Assert.Single(taken));
        Assert.True(DungeonGen.Clear(origin, r, taken).Y > origin.Y);
        var empty = new DungeonGen.Result([], Vector3.Zero, null, null, []);
        Assert.Equal(origin, DungeonGen.Clear(origin, empty, taken));
    }

    [Fact]
    public void ManyLevelsAreAllMadeDownToTheBoss()
    {
        var r = DungeonGen.Make(new DungeonGen.Settings(Size: 1, Levels: 8, Seed: 21));
        Assert.Equal(Enumerable.Range(0, 8), r.Map!.Select(m => m.Level).Distinct().Order());
        Assert.Equal(7, r.Map!.First(m => m.Name == "Arena").Level);
        // Beyond the most: the most.
        Assert.Equal(DungeonGen.MaxLevels, DungeonGen.Make(new DungeonGen.Settings(Size: 1, Levels: 99, Seed: 21)).Map!.Select(m => m.Level).Distinct().Count());
    }

    [Fact]
    public void OneArrivesOnTheEntrancesFloorFinish()
    {
        // The arrival was 5 cm over the slab whatever lay on it: a portal there stood in the boards or marble tiles.
        // Wooden walls come with boards 0.1 m thick everywhere.
        Assert.Equal(0.15f, DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Walls: "Wood", Seed: 1)).Arrival.Y, 0.001f);
        // Stone has none, but grand rooms get marble tiles (0.19 m) half the time.
        float[] heights = [.. Enumerable.Range(1, 8).Select(seed => DungeonGen.Make(new DungeonGen.Settings(Biome: "Black Forest", Size: 1, Seed: seed)).Arrival.Y)];
        Assert.All(heights, y => Assert.True(MathF.Abs(y - 0.05f) < 0.001f || MathF.Abs(y - 0.24f) < 0.001f, $"arrival at {y}"));
        Assert.Contains(heights, y => MathF.Abs(y - 0.24f) < 0.001f);
    }
}
