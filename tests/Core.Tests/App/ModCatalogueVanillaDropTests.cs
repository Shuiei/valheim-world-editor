using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Dropping the prefabs that are the game's own (a mod's name that became vanilla in a game update) from everything the
/// catalogue knows of them, and reporting whether any was there.
/// </summary>
public class ModCatalogueVanillaDropTests : ModCatalogueTestBase
{
    /// <summary>
    /// Makes the catalogue's data record over these dictionaries (it keeps them, so a test reads them back).
    /// </summary>
    private static object MakeData(Dictionary<string, JsonNode> prefabs, Dictionary<string, JsonNode> pieces, Dictionary<string, JsonNode> costs,
        Dictionary<string, string> items, Dictionary<string, string> mods, Dictionary<string, JsonNode> models)
    {
        var type = typeof(ModCatalogue).GetNestedType("Data", System.Reflection.BindingFlags.NonPublic)!;
        return PrivateCall.Create(type,
            [typeof(Dictionary<string, JsonNode>), typeof(Dictionary<string, JsonNode>), typeof(Dictionary<string, JsonNode>),
                typeof(Dictionary<string, string>), typeof(Dictionary<string, string>), typeof(Dictionary<string, JsonNode>)],
            prefabs, pieces, costs, items, mods, models);
    }

    [Fact]
    public void AVanillaNameIsDroppedFromEveryPartAndReported()
    {
        var prefabs = new Dictionary<string, JsonNode> { ["wood_fence"] = JsonNode.Parse("{}")!, ["MyChair"] = JsonNode.Parse("{}")! };
        var pieces = new Dictionary<string, JsonNode> { ["wood_fence"] = JsonNode.Parse("{}")! };
        var costs = new Dictionary<string, JsonNode> { ["wood_fence"] = JsonNode.Parse("{}")! };
        var mods = new Dictionary<string, string> { ["wood_fence"] = "Mod" };
        var models = new Dictionary<string, JsonNode> { ["wood_fence"] = JsonNode.Parse("{}")! };
        object data = MakeData(prefabs, pieces, costs, [], mods, models);

        bool dropped = (bool)PrivateCall.Static(typeof(ModCatalogue), "DropVanilla", data, Vanilla)!;

        Assert.True(dropped);
        Assert.False(prefabs.ContainsKey("wood_fence"));
        Assert.False(pieces.ContainsKey("wood_fence"));
        Assert.False(costs.ContainsKey("wood_fence"));
        Assert.False(mods.ContainsKey("wood_fence"));
        Assert.False(models.ContainsKey("wood_fence"));
        Assert.True(prefabs.ContainsKey("MyChair"));
    }

    [Fact]
    public void WithNoVanillaNameNothingIsDroppedOrReported()
    {
        var prefabs = new Dictionary<string, JsonNode> { ["MyChair"] = JsonNode.Parse("{}")! };
        object data = MakeData(prefabs, [], [], [], [], []);

        bool dropped = (bool)PrivateCall.Static(typeof(ModCatalogue), "DropVanilla", data, Vanilla)!;

        Assert.False(dropped);
        Assert.Single(prefabs);
    }
}
