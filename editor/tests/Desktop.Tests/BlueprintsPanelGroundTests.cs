using System.Text.Json.Nodes;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Whether a copy holds ground: a height other than the "none" marker (-32768) in its list of heights. A copy with
/// ground gets the sentence that its ground is not kept; one without gets nothing.
/// </summary>
public class BlueprintsPanelGroundTests
{
    [Fact]
    public void ACopyWithOnlyNoneMarkersHoldsNoGround()
    {
        var clip = new JsonObject { ["rel"] = new JsonArray(-32768, -32768) };
        Assert.False(BlueprintsPanel.HasGround(clip));
    }

    [Fact]
    public void AHeightAmongTheMarkersIsGround()
    {
        // A height of 0 is ground (the marker is only -32768).
        var clip = new JsonObject { ["rel"] = new JsonArray(-32768, 0) };
        Assert.True(BlueprintsPanel.HasGround(clip));
    }

    [Fact]
    public void ACopyWithoutAHeightListHoldsNoGround()
    {
        JsonObject clip = [];
        Assert.False(BlueprintsPanel.HasGround(clip));
    }

    [Fact]
    public void ACopyWithGroundSaysItsGroundIsNotKept()
    {
        var clip = new JsonObject { ["rel"] = new JsonArray(0) };
        Assert.Equal(Said.Of("blueprints.msg.groundNotKept"), BlueprintsPanel.GroundNotKept(clip));
    }

    [Fact]
    public void ACopyWithoutGroundSaysNothing()
    {
        var clip = new JsonObject { ["rel"] = new JsonArray(-32768) };
        Assert.Equal(Said.Empty, BlueprintsPanel.GroundNotKept(clip));
    }
}
