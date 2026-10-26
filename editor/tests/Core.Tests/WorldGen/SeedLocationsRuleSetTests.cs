using System.Text.Json;
using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's location rules as locations.json keeps them: read back from the text, and written with the nulls left out.
/// </summary>
public class SeedLocationsRuleSetTests
{
    [Fact]
    public void ARuleSetIsReadWithItsLocations()
    {
        var rules = LocationRuleSet.FromJson("{\"locations\":[{\"prefab\":\"A\",\"enable\":1,\"quantity\":2}],\"altBiomes\":[]}");

        var rule = Assert.Single(rules.Locations);
        Assert.Equal("A", rule.Prefab);
        Assert.Equal(2, rule.Quantity);
        Assert.Empty(rules.AltBiomes);
    }

    [Fact]
    public void TextWithNoRulesIsRefused()
    {
        Assert.Throws<JsonException>(() => LocationRuleSet.FromJson("null"));
    }

    [Fact]
    public void ANullFieldIsLeftOutOfTheText()
    {
        var rules = new LocationRuleSet { Locations = [new LocationRule { Prefab = "A", AltBiome = null }] };

        string text = rules.ToJson();

        Assert.Contains("\"prefab\":\"A\"", text);
        // The key of a location's alt biome, not the file's "altBiomes" list (which is written empty here).
        Assert.DoesNotContain("\"altBiome\":", text);
    }

    [Fact]
    public void TheTextReadsBackToTheSameRules()
    {
        var rules = new LocationRuleSet { Locations = [new LocationRule { Prefab = "B", Quantity = 5 }] };

        var back = LocationRuleSet.FromJson(rules.ToJson());

        Assert.Equal("B", Assert.Single(back.Locations).Prefab);
        Assert.Equal(5, back.Locations[0].Quantity);
    }
}
