using System.Reflection;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The height a naturalize stroke pulls a point towards: worked out once per point and kept for the rest of the stroke,
/// so a point already aimed at keeps its aim.
/// </summary>
public class SculptNaturalTargetTests
{
    /// <summary>The naturalize aim of a point, through the sculpt's private step.</summary>
    private static float NaturalTarget(Ground g, Brush b, Stroke s, int p) => (float)typeof(Sculpt).GetMethod("NaturalTarget", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [g, b, s, p])!;

    [Fact]
    public void APointAlreadyAimedAtKeepsItsAim()
    {
        var g = new Ground(5, 5, 0, 0, 1, 1);
        var s = new Stroke { Tool = BrushTool.Raise, Start = g.Snapshot() };
        s.Natural[12] = 7.5f;
        Assert.Equal(7.5f, NaturalTarget(g, new Brush(), s, 12));
    }

    [Fact]
    public void APointNotYetAimedAtGetsItsAimKeptForTheStroke()
    {
        var g = new Ground(5, 5, 0, 0, 1, 1);
        var s = new Stroke { Tool = BrushTool.Raise, Start = g.Snapshot() };
        float aim = NaturalTarget(g, new Brush(), s, 12);
        Assert.True(s.Natural.ContainsKey(12));
        Assert.Equal(aim, s.Natural[12]);
    }
}
