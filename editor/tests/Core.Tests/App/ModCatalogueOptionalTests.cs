using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The optional parts of a mod catalogue's building piece entry: a box of six numbers and snap points of three numbers
/// each, each optional, and refused when the list has the wrong shape or a value that is not a number.
/// </summary>
public class ModCatalogueOptionalTests
{
    private static bool Optional(string name, string json) =>
        PrivateMember.Call<bool>(typeof(ModCatalogue), name, null, json == null ? null : JsonNode.Parse(json));

    [Fact]
    public void AnAbsentBoxIsFine()
    {
        Assert.True(Optional("OptionalBox", null!));
    }

    [Theory]
    [InlineData("[1,2,3,4,5,6]", true)]
    [InlineData("[1,2,3,4,5]", false)]
    [InlineData("[1,2,3,4,5,\"x\"]", false)]
    public void ABoxIsSixNumbers(string json, bool fine)
    {
        Assert.Equal(fine, Optional("OptionalBox", json));
    }

    [Fact]
    public void AnAbsentSnapListIsFine()
    {
        Assert.True(Optional("OptionalSnaps", null!));
    }

    [Theory]
    [InlineData("[[1,2,3]]", true)]
    [InlineData("[[1,2,3],[4,5,6]]", true)]
    [InlineData("[[1,2]]", false)]
    [InlineData("[1]", false)]
    public void ASnapIsAListOfPointsOfThreeNumbers(string json, bool fine)
    {
        Assert.Equal(fine, Optional("OptionalSnaps", json));
    }
}
