using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Stroke.StartHeight: a point's height as the stroke found it: its base and lift, plus its edit's level and smooth held to the
/// game's ±8 m limit, when the point was edited.
/// </summary>
public class StrokeStartHeightTests
{
    // A 2 × 2 ground whose first point is at 5 m, and a stroke whose start has point 0 lifted by 2 m with the given edit.
    private static (Ground Ground, Stroke Stroke) Start(float level, float smooth, byte mod)
    {
        var ground = new Ground(2, 2, 0, 0, 1, 1);
        ground.Base[0] = 5f;
        var state = new Ground.State(
            Level: [level, 0, 0, 0],
            Smooth: [smooth, 0, 0, 0],
            Mod: [mod, 0, 0, 0],
            Paint: new float[16],
            PMod: new byte[4],
            Lift: [2f, 0, 0, 0]);
        return (ground, new Stroke { Tool = BrushTool.Erode, Start = state });
    }

    [Fact]
    public void AnUneditedPointStartsAtItsBasePlusLift()
    {
        var (ground, stroke) = Start(level: 9f, smooth: 9f, mod: 0);

        Assert.Equal(7f, stroke.StartHeight(ground, 0));
    }

    [Fact]
    public void AnEditedPointAddsItsLevelAndSmooth()
    {
        var (ground, stroke) = Start(level: 1f, smooth: 2f, mod: 1);

        Assert.Equal(10f, stroke.StartHeight(ground, 0));
    }

    [Fact]
    public void AnEditsLevelAndSmoothAreHeldToTheUpperLimit()
    {
        var (ground, stroke) = Start(level: 20f, smooth: 0f, mod: 1);

        Assert.Equal(5f + 2f + 8f, stroke.StartHeight(ground, 0));
    }

    [Fact]
    public void AnEditsLevelAndSmoothAreHeldToTheLowerLimit()
    {
        var (ground, stroke) = Start(level: -20f, smooth: 0f, mod: 1);

        Assert.Equal(5f + 2f - 8f, stroke.StartHeight(ground, 0));
    }
}
