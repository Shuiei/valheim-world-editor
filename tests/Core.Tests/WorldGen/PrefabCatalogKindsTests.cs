using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What an item and a spawner are, as the catalogue describes them: an item's durability when new, a spawner's respawn
/// and levels, and the one-line account of what a spawner makes.
/// </summary>
public class PrefabCatalogKindsTests
{
    [Fact]
    public void AnItemThatWearsStartsAtItsDurabilityAndGainsPerQuality()
    {
        var item = new PrefabCatalog.ItemKind(Type: 1, AttachOverride: 0, Variants: 0, Attach: false, AttachSkin: false, Durability: 50, DurabilityPerLevel: 10);

        Assert.Equal(50f, item.NewDurability(1));
        Assert.Equal(70f, item.NewDurability(3));
    }

    [Fact]
    public void AnItemThatDoesNotWearIsAHundred()
    {
        var item = new PrefabCatalog.ItemKind(Type: 1, AttachOverride: 0, Variants: 0, Attach: false, AttachSkin: false);

        Assert.Equal(100f, item.NewDurability(5));
    }

    [Fact]
    public void ASpawnPointRespawnsOnlyWhenItHasATime()
    {
        var spawn = Spawner(respawn: 60);
        Assert.True(spawn.Respawns);
        Assert.False(Spawner(respawn: 0).Respawns);
        Assert.False(Spawner(respawn: 60, nest: true).Respawns);
        Assert.False(Spawner(respawn: 60, trigger: true).Respawns);
    }

    [Fact]
    public void ALevelRangeIsTheLowestToTheHighestOrOneWhenNoStars()
    {
        Assert.Equal((1, 3), Spawner(min: 1, max: 3).Levels);
        Assert.Equal((1, 1), Spawner(min: 1, max: 1).Levels);
        Assert.Equal((1, 1), Spawner(min: 3, max: 1).Levels);
    }

    [Fact]
    public void ASpawnPointSummaryNamesTheCreatureAndItsRespawn()
    {
        string text = Spawner(respawn: 60, creatures: ["Boar"]).Summary(english: true);

        Assert.Contains("Boar", text);
        Assert.Contains("60", text);
    }

    [Fact]
    public void ANestSummaryNamesItsCreatureAndItsCountAndRadius()
    {
        var nest = new PrefabCatalog.SpawnKind(["Greydwarf"], Nest: true, Trigger: false, RespawnMinutes: 0, MinLevel: 1, MaxLevel: 1,
            Day: true, Night: true, Radius: 5, MaxNear: 3);

        string text = nest.Summary(english: true);

        Assert.Contains("Greydwarf", text);
        Assert.Contains("3", text);
        Assert.Contains("5", text);
    }

    /// <summary>A spawn point of a boar, with these settings.</summary>
    private static PrefabCatalog.SpawnKind Spawner(float respawn = 0, bool nest = false, bool trigger = false, int min = 1, int max = 1, string[]? creatures = null) =>
        new(creatures ?? ["Boar"], Nest: nest, Trigger: trigger, RespawnMinutes: respawn, MinLevel: min, MaxLevel: max,
            Day: true, Night: true, Radius: 0, MaxNear: 0);
}
