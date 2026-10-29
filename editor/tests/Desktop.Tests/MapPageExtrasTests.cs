using TerrainEditor.Desktop;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// <see cref="MapPage.Extras"/>: what the map's cursor line adds for a point (its territory, alt biomes and status
/// effects, each after " · ").
/// </summary>
public class MapPageExtrasTests
{
    [Fact]
    public void APointWithNothingAddsNothing()
    {
        Assert.Equal("", MapPage.Extras(null, null, []));
        Assert.Equal("", MapPage.Extras(null, [], [new StatusEffect(" ", true, true)]));
    }

    [Fact]
    public void ThePointsTerritoryAltBiomesAndEffectsFollowOneAnother()
    {
        var alts = new List<AltBiomeRule> { new() { Name = "Dark Meadows" }, new() { Name = "Glade" } };
        StatusEffect[] effects = [new("Freezing", false, true), new("Rested", true, false), new("Wet", true, true)];
        Assert.Equal(" · territory cold · alt biome Dark Meadows, Glade · effects Freezing (night), Rested (day), Wet",
            MapPage.Extras(new Territory { Name = "cold" }, alts, effects));
    }
}
