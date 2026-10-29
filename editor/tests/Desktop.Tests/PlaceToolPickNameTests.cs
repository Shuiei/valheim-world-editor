using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// PlaceTool.PickName: a random draw in [0, 1) picks a kind by its weight, a kind's share of the draw being its weight over the
/// total (a kind with no weight counts as 1).
/// </summary>
public class PlaceToolPickNameTests
{
    [Fact]
    public void WithEqualWeightsTheDrawPicksTheKindItLandsOn()
    {
        var tool = new PlaceTool();
        string[] names = ["Alpha", "Beta"];

        Assert.Equal("Alpha", tool.PickName(names, 0.49f));
        Assert.Equal("Beta", tool.PickName(names, 0.5f));
    }

    [Fact]
    public void AHeavierKindTakesItsWeightsShareOfTheDraw()
    {
        // Alpha weighs 3 and Beta 1: Alpha covers the first three quarters of the draw.
        var tool = new PlaceTool();
        tool.Weights["Alpha"] = 3;
        string[] names = ["Alpha", "Beta"];

        Assert.Equal("Alpha", tool.PickName(names, 0.7f));
        Assert.Equal("Beta", tool.PickName(names, 0.8f));
    }

    [Fact]
    public void ASingleKindIsAlwaysPicked()
    {
        Assert.Equal("Only", new PlaceTool().PickName(["Only"], 0.3f));
    }
}
