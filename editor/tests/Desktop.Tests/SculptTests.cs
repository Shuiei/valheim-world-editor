using Xunit;
using static TerrainEditor.Desktop.Tests.StrokeRun;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Sculpting the ground with the brushes: what each tool does to the heights and the paint under a stroke, the game's
/// 8 m limit, and the outer line of points that never moves.
/// </summary>
[Collection("World files")]
public class SculptTests
{
    [Fact]
    public void RaiseLiftsTheMiddleMostAndLeavesTheOutside()
    {
        var s = TestScenes.Flat();
        Stroke(s, BrushTool.Raise, 64, 64);
        // 6 m a second × strength 0.4 × 0.5 s at the middle (full weight).
        Assert.Equal(30 + 6 * 0.4f * 0.5f, At(s, 64, 64), 2);
        Assert.True(At(s, 67, 64) > 30 && At(s, 67, 64) < At(s, 64, 64));
        Assert.Equal(30, At(s, 64 + 7, 64), 3);
        Assert.Equal(30, At(s, 10, 10), 3);
    }

    [Fact]
    public void AFrameThatChangesNothingHasNothingToRedraw()
    {
        // A frame the mask left all out still returned the brush rectangle, so the view refreshed and uploaded it.
        var s = TestScenes.Flat();
        var stroke = new Stroke { Tool = BrushTool.Smooth, Start = s.Ground.Snapshot() };
        Assert.Null(Sculpt.Apply(s.Ground, s.Brush, stroke, 64, 64, 0.1f, _ => 0));
        Assert.Empty(stroke.Touched);
        Assert.NotNull(Sculpt.Apply(s.Ground, s.Brush, stroke, 64, 64, 0.1f));
        Assert.NotEmpty(stroke.Touched);
    }

    [Fact]
    public void LowerDigsAndTheGameLimitStopsItAt8Metres()
    {
        var s = TestScenes.Flat();
        s.Brush.Strength = 1;
        s.BeginStroke(BrushTool.Lower, 64, 64);
        for (int i = 0; i < 300; i++)
        {
            s.StrokeStep(64, 64, 0.1f);
        }
        string msg = s.EndStroke().Shown;
        Assert.Equal(22, At(s, 64, 64), 3);
        Assert.Contains("8 m", msg);
        Assert.Equal(1, s.Scene.Limit[64 * s.Scene.W + 64]);
    }

    [Fact]
    public void TheOuterLineOfPointsNeverMoves()
    {
        var s = TestScenes.Flat(1);
        s.Brush.Radius = 10;
        Stroke(s, BrushTool.Raise, 0, 30);
        Assert.Equal(30, At(s, 0, 30), 4);
        Assert.True(At(s, 1, 30) > 30);
    }

    [Fact]
    public void FlattenLevelsToTheGroundWhereTheStrokeStarts()
    {
        var s = TestScenes.Flat();
        s.Brush.Radius = 4;
        Stroke(s, BrushTool.Raise, 70, 64, 60);
        float high = At(s, 70, 64);
        s.Brush.Radius = 8;
        s.Brush.Strength = 1;
        s.BeginStroke(BrushTool.Flatten, 64, 64);
        Assert.Equal(30, s.Brush.Target, 3);
        for (int i = 0; i < 60; i++)
        {
            s.StrokeStep(64, 64, 0.1f);
        }
        s.EndStroke();
        Assert.True(At(s, 70, 64) < high - 0.5f, $"{At(s, 70, 64)} vs {high}");
        Assert.Equal(30, At(s, 64, 64), 2);
    }

    [Fact]
    public void SmoothEvensOutABump()
    {
        var s = TestScenes.Flat();
        s.Brush.Radius = 2;
        Stroke(s, BrushTool.Raise, 64, 64, 60);
        float bump = At(s, 64, 64) - At(s, 66, 64);
        s.Brush.Radius = 6;
        Stroke(s, BrushTool.Smooth, 64, 64, 60);
        Assert.True(At(s, 64, 64) - At(s, 66, 64) < bump / 2, $"{bump} → {At(s, 64, 64) - At(s, 66, 64)}");
    }

    [Fact]
    public void NaturalizeAddsBumps()
    {
        var s = TestScenes.Flat();
        s.Brush.Noise = new Noise(12345);
        s.Brush.Strength = 1;
        s.Brush.Radius = 12;
        Stroke(s, BrushTool.Natural, 64, 64, 120);
        var values = Enumerable.Range(58, 12).Select(x => At(s, x, 64)).ToList();
        Assert.True(values.Max() - values.Min() > 0.1f, string.Join(", ", values));
    }

    [Fact]
    public void RestoreBringsTheGeneratedGroundBack()
    {
        var s = TestScenes.Flat();
        Stroke(s, BrushTool.Raise, 64, 64);
        Stroke(s, BrushTool.PaintDirt, 64, 64);
        s.Brush.Strength = 1;
        s.Brush.Radius = 10;
        Stroke(s, BrushTool.Restore, 64, 64, 120);
        int g = 64 * s.Ground.W + 64;
        Assert.Equal(0, s.Ground.Mod[g]);
        Assert.Equal(0, s.Ground.PMod[g]);
        Assert.Equal(30, At(s, 64, 64), 3);
    }

    [Fact]
    public void PaintMovesTowardsItsColourAndShowsInTheMask()
    {
        var s = TestScenes.Flat();
        s.Brush.Strength = 1;
        Stroke(s, BrushTool.PaintPaved, 64, 64, 60);
        int g = 64 * s.Ground.W + 64;
        Assert.Equal(1, s.Ground.PMod[g]);
        Assert.Equal(new[] { 0f, 0, 1, 1 }, s.Ground.Paint[(g * 4)..(g * 4 + 4)].Select(v => MathF.Round(v, 2)).ToArray());
        Assert.Equal(255, s.Scene.Mask[g * 4 + 2]);
        Assert.Equal(0, s.Scene.Mask[g * 4]);
    }

    [Fact]
    public void AFirstLightTouchStartsFromTheGeneratedMask()
    {
        // Generated dirt everywhere (as around a location); a light cultivated touch keeps it nearly all dirt.
        var g = new Ground(129, 129, 0, 0, 2);
        Array.Fill(g.Base, 30f);
        for (int p = 0; p < g.Base.Length; p++)
        {
            g.BaseMask[p * 4] = 1;
            g.BaseMask[p * 4 + 3] = 1;
        }
        var b = new Brush { Radius = 6, Strength = 0.05f };
        var stroke = new Stroke { Tool = BrushTool.PaintCultivated, Start = g.Snapshot() };
        Sculpt.Apply(g, b, stroke, 64, 64, 1 / 60f);
        int at = 64 * g.W + 64;
        Assert.Equal(1, g.PMod[at]);
        Assert.InRange(g.Paint[at * 4], 0.9f, 1);
        Assert.InRange(g.Paint[at * 4 + 1], 0.001f, 0.1f);
    }
}
