using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Reading terrain-modifiers.json: a prefab's modifiers by its name's hash, with its location flag, and a paint the editor
/// does not know refused.
/// </summary>
public class TerrainModifiersParseTests
{
    /// <summary>A location with one modifier of the given paint type.</summary>
    private static string Json(int paint) =>
        "{\"rock\":{\"location\":true,\"modifiers\":[{\"pos\":[1,2,3],\"level\":1,\"levelOffset\":0,\"levelRadius\":2,\"square\":0," +
        "\"smooth\":0,\"smoothRadius\":0,\"smoothPower\":1,\"paintCleared\":0,\"paintRadius\":0,\"paintType\":" + paint +
        ",\"paintStrength\":1,\"paintHeightCheck\":0,\"sortOrder\":3,\"player\":0}]}}";

    [Fact]
    public void APrefabIsReadUnderItsNameHashWithItsLocationFlagAndModifiers()
    {
        var parsed = TerrainModifiers.Parse(Json(0));

        var (name, location, mods) = parsed[StableHash.Of("rock")];
        Assert.Equal("rock", name);
        Assert.True(location);
        var mod = Assert.Single(mods);
        Assert.Equal(new System.Numerics.Vector3(1, 2, 3), mod.Offset);
        Assert.Equal(2f, mod.LevelRadius);
        Assert.Equal(3, mod.SortOrder);
    }

    [Fact]
    public void APaintTheEditorDoesNotKnowIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => TerrainModifiers.Parse(Json(99)));
    }
}
