using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading a mod model from the game's report: the root scale, then each part with its mesh (its name without the
/// instance suffix), vertex count, submesh, material when it has one, matrix, and bounds when it has them.
/// </summary>
public class ModCatalogueModelReadTests
{
    /// <summary>A model read from its report, as the code under test reads it.</summary>
    private static ModCatalogue.Model ToModel(string json) => (ModCatalogue.Model)PrivateCall.Static(typeof(ModCatalogue), "ToModel", JsonNode.Parse(json)!.AsObject())!;

    private const string Identity = "[1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]";

    [Fact]
    public void TheRootScaleAndEachPartAreRead()
    {
        string json = "{\"s\":[2,3,4],\"p\":[{\"mesh\":\"Wood Chest Instance\",\"v\":6,\"sub\":1,\"mat\":\"Oak (Instance)\",\"m\":" + Identity + ",\"b\":[0,0,0,1,1,1]}]}";
        var model = ToModel(json);
        Assert.Equal(new[] { 2f, 3f, 4f }, model.RootScale);
        var part = Assert.Single(model.Parts);
        Assert.Equal("Wood Chest", part.Mesh);
        Assert.Equal(6, part.Vertices);
        Assert.Equal(1, part.Sub);
        Assert.Equal("Oak", part.Material);
        Assert.Equal(new[] { 1f, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }, part.Matrix);
        Assert.Equal(new[] { 0f, 0, 0, 1, 1, 1 }, part.Bounds);
    }

    [Fact]
    public void APartWithNoMaterialOrBoundsHasNeither()
    {
        string json = "{\"s\":[1,1,1],\"p\":[{\"mesh\":\"Crate\",\"v\":3,\"sub\":0,\"m\":" + Identity + "}]}";
        var part = Assert.Single(ToModel(json).Parts);
        Assert.Null(part.Material);
        Assert.Null(part.Bounds);
    }
}
