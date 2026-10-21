using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The items the cost entries of the catalogue take: the first name of each requirement, every entry's, once each.
/// </summary>
public class ModCatalogueRequiredTests
{
    private static readonly string[] Expected2 = ["Resin", "Stone", "Wood"];
    private static readonly string[] Expected1 = ["Wood"];
    [Fact]
    public void TheItemsOfEveryCostEntryAreRequired()
    {
        var costs = new List<JsonNode>
        {
            JsonNode.Parse("""{"r":[["Wood",2],["Stone",1]]}""")!,
            JsonNode.Parse("""{"r":[["Resin",3]]}""")!,
        };

        var required = (HashSet<string>)PrivateCall.Static(typeof(ModCatalogue), "Required", costs)!;

        Assert.Equal(Expected2, required.Order().ToArray());
    }

    [Fact]
    public void AnItemTakenByTwoEntriesIsRequiredOnce()
    {
        var costs = new List<JsonNode>
        {
            JsonNode.Parse("""{"r":[["Wood",2]]}""")!,
            JsonNode.Parse("""{"r":[["Wood",1],["Wood",4]]}""")!,
        };

        var required = (HashSet<string>)PrivateCall.Static(typeof(ModCatalogue), "Required", costs)!;

        Assert.Equal(Expected1, required.ToArray());
    }

    [Fact]
    public void NoCostEntriesRequireNothing()
    {
        var required = (HashSet<string>)PrivateCall.Static(typeof(ModCatalogue), "Required", new List<JsonNode>())!;

        Assert.Empty(required);
    }
}
