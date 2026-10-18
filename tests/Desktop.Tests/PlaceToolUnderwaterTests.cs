using System.Reflection;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Whether a set of objects may go under the water: only when one of them is a sea plant (kelp or seaweed), in any case.
/// </summary>
public class PlaceToolUnderwaterTests
{
    /// <summary>Whether the set of names may go under the water.</summary>
    private static bool UnderwaterOk(params string[] names) => (bool)typeof(PlaceTool).GetMethod("UnderwaterOk", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [names])!;

    [Theory]
    [InlineData("Kelp_small")]
    [InlineData("SeaweedBig")]
    [InlineData("KELP")]
    public void ASeaPlantLetsItGoUnderwater(string name)
    {
        Assert.True(UnderwaterOk(name));
    }

    [Fact]
    public void ASeaPlantAmongOthersLetsTheSetGoUnderwater()
    {
        Assert.True(UnderwaterOk("stone", "KELP"));
    }

    [Fact]
    public void ASetWithNoSeaPlantDoesNotGoUnderwater()
    {
        Assert.False(UnderwaterOk("stone_wall", "wood_fence"));
    }

    [Fact]
    public void AnEmptySetDoesNotGoUnderwater()
    {
        Assert.False(UnderwaterOk());
    }
}
