using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The brushes' remaining branches: the cultivated paint, a stamp with no picture, Flatten to the typed height,
/// Naturalize over ground already edited and Restore bringing a No limit lift back down.
/// </summary>
public class SculptBranchesTests
{
    /// <summary>
    /// A stroke held for a number of frames of 0.1 s at grid point (x, z).
    /// </summary>
    private static void Stroke(EditSession s, BrushTool tool, float x, float z, int frames)
    {
        s.BeginStroke(tool, x, z);
        for (int i = 0; i < frames; i++)
        {
            s.StrokeStep(x, z, 0.1f);
        }
        s.EndStroke();
    }

    [Fact]
    public void EachPaintHasItsColour()
    {
        Assert.Equal([0f, 1, 0, 1], Brush.PaintOf(BrushTool.PaintCultivated)!);
        Assert.Null(Brush.PaintOf(BrushTool.Raise));
    }

    [Fact]
    public void AStampWithNoPictureWeighsNothing()
    {
        var b = new Brush { Radius = 5, Shape = BrushShape.Stamp, StampData = null };
        Assert.Equal(0, b.Weight(0, 0));
    }

    [Fact]
    public void FlattenGoesToTheTypedHeightWhenNotTakenFromTheClick()
    {
        var s = TestScenes.Flat(2);
        s.Brush.TargetFromClick = false;
        s.Brush.Target = 34;
        s.Brush.Radius = 6;
        s.Brush.Strength = 1;
        Stroke(s, BrushTool.Flatten, 64, 64, 20);
        Assert.Equal(34, s.Ground.HeightOf(64 * s.Ground.W + 64), 1);
    }

    [Fact]
    public void NaturalizeWorksOverGroundAlreadyEdited()
    {
        var s = TestScenes.Flat(2);
        s.Shape(64, 64, _ => 3, 10, 0, "Shape");
        s.Brush.Radius = 8;
        s.Brush.Strength = 1;
        Stroke(s, BrushTool.Natural, 64, 64, 10);
        Assert.Equal("Naturalize", s.UndoLabel);
    }

    [Fact]
    public void RestoreBringsALiftBackDown()
    {
        var s = TestScenes.Flat(5);
        Assert.NotNull(s.Mountain(160, 160, new MountainSpec(MountainKind.Peak, 40, 30, 0.6f, 0, 7), clear: false, "Mountain"));
        int p = 160 * s.Ground.W + 160;
        Assert.True(s.Ground.Lift[p] > 1);
        s.Brush.Radius = 10;
        s.Brush.Strength = 1;
        // One frame takes part of it away; many take it all.
        Stroke(s, BrushTool.Restore, 160, 160, 1);
        Assert.True(s.Ground.Lift[p] > 0);
        Stroke(s, BrushTool.Restore, 160, 160, 40);
        Assert.Equal(0, s.Ground.Lift[p]);
    }
}
