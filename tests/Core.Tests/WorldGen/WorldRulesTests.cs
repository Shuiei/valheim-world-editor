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
