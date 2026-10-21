using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A piece's build cost in the mod catalogue: a name, and a list of item and amount pairs (ModCatalogue.IsCost).
/// </summary>
public class ModCataloguePieceCostTests
{
    [Fact]
    public void ACostWithANameAndItemAmountPairsIsACost()
    {
        Assert.True(IsCost("{\"n\":\"Coal\",\"r\":[[\"Coal\",2]]}"));
    }

    [Fact]
    public void ACostWithoutANameIsNotACost()
    {
        Assert.False(IsCost("{\"r\":[[\"Coal\",2]]}"));
    }

    [Fact]
    public void AnItemPairWithOnlyItsNameIsNotACost()
    {
        Assert.False(IsCost("{\"n\":\"Coal\",\"r\":[[\"Coal\"]]}"));
    }

    [Fact]
    public void AnAmountThatIsNotAnIntegerIsNotACost()
    {
        Assert.False(IsCost("{\"n\":\"Coal\",\"r\":[[\"Coal\",1.5]]}"));
    }

    /// <summary>Whether the JSON text is a piece cost, as the catalogue reads it.</summary>
    private static bool IsCost(string json) =>
        PrivateMember.Call<bool>(typeof(ModCatalogue), "IsCost", null, JsonNode.Parse(json)!);
}
