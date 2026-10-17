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
        var rules = SeedLocations.RuleSet.FromJson("{\"locations\":[{\"prefab\":\"A\",\"enable\":1,\"quantity\":2}],\"altBiomes\":[]}");

        var rule = Assert.Single(rules.locations);
        Assert.Equal("A", rule.prefab);
        Assert.Equal(2, rule.quantity);
        Assert.Empty(rules.altBiomes);
    }

    [Fact]
    public void TextWithNoRulesIsRefused()
    {
        Assert.Throws<JsonException>(() => SeedLocations.RuleSet.FromJson("null"));
    }

    [Fact]
    public void ANullFieldIsLeftOutOfTheText()
    {
        var rules = new SeedLocations.RuleSet { locations = [new SeedLocations.Rule { prefab = "A", altBiome = null }] };

        string text = rules.ToJson();

        Assert.Contains("\"prefab\":\"A\"", text);
        Assert.DoesNotContain("altBiome", text);
    }

    [Fact]
    public void TheTextReadsBackToTheSameRules()
    {
        var rules = new SeedLocations.RuleSet { locations = [new SeedLocations.Rule { prefab = "B", quantity = 5 }] };

        var back = SeedLocations.RuleSet.FromJson(rules.ToJson());

        Assert.Equal("B", Assert.Single(back.locations).prefab);
        Assert.Equal(5, back.locations[0].quantity);
    }
}
