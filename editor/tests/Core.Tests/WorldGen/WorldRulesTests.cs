using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The rules a world generation mod can set: the game's own values by default, told apart by their stamp, and the
/// mod's conversions between metres, altitudes and the generator's units.
/// </summary>
public class WorldRulesTests
{
    [Fact]
    public void TheDefaultsAreTheGames()
    {
        var v = WorldRules.Vanilla;
        Assert.True(v.IsVanilla);
        Assert.Equal("", v.Stamp);
        Assert.Equal(10000f, v.Radius);
        Assert.Equal(10500f, v.TotalRadius);
        Assert.Equal(10000f, v.StretchedRadius);
        Assert.Equal(10500f, v.StretchedTotalRadius);
        Assert.Equal(30f, v.WaterLevel);
        Assert.Equal(512, v.GridWidth);
        Assert.True(new WorldRules().IsVanilla);
        Assert.Equal(v, new WorldRules());
    }

    [Fact]
    public void ChangedRulesHaveAStampOfTheirOwn()
    {
        var a = new WorldRules { Radius = 20000f, Stretch = 2f };
        Assert.False(a.IsVanilla);
        Assert.NotEqual("", a.Stamp);
        Assert.Equal(a.Stamp, new WorldRules { Radius = 20000f, Stretch = 2f }.Stamp);
        Assert.NotEqual(a.Stamp, new WorldRules { Radius = 20000f }.Stamp);
        Assert.Equal(20500f, a.TotalRadius);
        Assert.Equal(10000f, a.StretchedRadius);
        Assert.Equal(10250f, a.StretchedTotalRadius);
    }

    [Fact]
    public void RulesWithListsCompareByTheirContents()
    {
        var rule = new BiomeRule { Biome = Heightmap.Biome.Swamp, MaxDistance = 3000f };
        var a = new WorldRules { World = [rule], Territories = [new Territory { Name = "t" }] };
        var b = new WorldRules { World = [rule with { }], Territories = [new Territory { Name = "t" }] };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(a.Stamp, b.Stamp);
        Assert.False(a.IsVanilla);
        Assert.NotEqual(a, new WorldRules { World = [rule with { MaxDistance = 4000f }] });
        // A copy with a change is told apart: nothing of the stamp is kept across "with".
        Assert.NotEqual(a, a with { Radius = 20000f });
        Assert.Contains("\"MaxDistance\":3000", a.Stamp);
        // An infinite half size (no rectangle limit) is written by name.
        Assert.Contains("Infinity", new WorldRules { World = [new BiomeRule()] }.Stamp);
        Assert.Contains("Lava Fields", new WorldRules { Biomes = new BiomeTable([.. BiomeTable.Vanilla.Biomes, new BiomeInfo { Value = (Heightmap.Biome)0x400, Id = "Lava Fields" }]) }.Stamp);
        // The places too: the location kinds (a clone's game prefab among them) and the vegetation rules.
        var places = new WorldRules { Locations = new SeedLocations.RuleSet { Locations = [new SeedLocations.Rule { Prefab = "Crypt2:Deep", BasePrefab = "Crypt2" }] }, Vegetation = [new VegetationRule { prefab = "Pine", prefabs = ["Pine", "Fir"] }] };
        Assert.Contains("Crypt2:Deep", places.Stamp);
        Assert.Contains("\"basePrefab\":\"Crypt2\"", places.Stamp);
        Assert.Contains("Fir", places.Stamp);
        Assert.False(places.IsVanilla);
        Assert.Equal(places, places with { });
    }

    [Fact]
    public void HeightsConvertAsTheModConvertsThem()
    {
        // 200 m a generator unit; altitudes are from the water level.
        Assert.Equal(0.15f, WorldRules.ToBaseHeight(30f));
        Assert.Equal(26f, WorldRules.Vanilla.AltitudeToHeight(-4f));
        Assert.Equal(0.05f, WorldRules.Vanilla.AltitudeToBaseHeight(-20f));
        Assert.Equal(0.4f, WorldRules.Vanilla.AltitudeToBaseHeight(50f));
        var low = new WorldRules { WaterLevel = 20f };
        Assert.Equal(16f, low.AltitudeToHeight(-4f));
        Assert.Equal(0f, low.AltitudeToBaseHeight(-20f));
    }
}
