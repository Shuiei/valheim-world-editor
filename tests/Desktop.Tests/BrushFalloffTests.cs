using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Brush edge cases: every falloff's profile, the ragged and turned brushes and their outlines, a brush outside the
/// area, and erosion by rain.
/// </summary>
[Collection("World files")]
public class BrushFalloffTests
{
    [Theory]
    [InlineData(Falloff.Smooth)]
    [InlineData(Falloff.Linear)]
    [InlineData(Falloff.Dome)]
    [InlineData(Falloff.Flat)]
    [InlineData(Falloff.Peak)]
    [InlineData(Falloff.Sharp)]
    public void EveryFalloffIsFullInTheMiddleAndNothingAtTheEdge(Falloff f)
    {
        Assert.Equal(1, Brush.Fade(f, 1), 4);
        Assert.Equal(0, Brush.Fade(f, 0), 4);
        // Never weaker nearer the middle.
        float last = 0;
        for (int i = 0; i <= 20; i++)
        {
            float v = Brush.Fade(f, i / 20f);
            Assert.True(v >= last - 1e-5f, $"{f} drops at {i / 20f}");
            last = v;
        }
    }

    [Fact]
    public void TheFalloffsDifferWhereTheyShould()
    {
        Assert.Equal(0.5f, Brush.Fade(Falloff.Linear, 0.5f), 4);
        Assert.True(Brush.Fade(Falloff.Dome, 0.5f) > 0.8f);
        Assert.Equal(1, Brush.Fade(Falloff.Flat, 0.3f), 4);
        Assert.Equal(0.125f, Brush.Fade(Falloff.Peak, 0.5f), 4);
        Assert.Equal(1, Brush.Fade(Falloff.Sharp, 0.01f), 4);
    }

    [Fact]
    public void TheRaggedBrushWavesAroundTheCircle()
    {
        var b = new Brush { Radius = 10, Shape = BrushShape.Noise };
        Assert.Equal(10 * 1.5f, b.Reach, 3);
        // Along the edge, some points are in and some out: the edge is ragged.
        var w = Enumerable.Range(0, 64).Select(i => b.Weight(9.5f * MathF.Cos(i * MathF.Tau / 64), 9.5f * MathF.Sin(i * MathF.Tau / 64), 300 + 9.5f * MathF.Cos(i * MathF.Tau / 64), 200 + 9.5f * MathF.Sin(i * MathF.Tau / 64))).ToList();
        Assert.Contains(w, v => v > 0.01f);
        Assert.Contains(w, v => v == 0);
    }

    [Fact]
    public void ATurnedSquareReachesItsCornersAndItsOutlineTurns()
    {
        var b = new Brush { Radius = 10, Shape = BrushShape.Square, Turn = 45 };
        Assert.Equal(14.2f, b.Reach, 1);
        // Turned 45°: the corner now points along x, past the radius.
        Assert.True(b.Weight(13, 0) > 0);
        Assert.Equal(0, b.Weight(9, 9));
        var outline = b.Outline(8);
        Assert.Equal(8, outline.Count);
        Assert.Contains(outline, p => MathF.Abs(MathF.Abs(p.X) - 14.14f) < 0.1f && MathF.Abs(p.Z) < 0.1f);
    }

    [Fact]
    public void RainErodesTheGround()
    {
        var s = TestScenes.Flat(2);
        s.Shape(64, 64, Formula.Compile("8 * (1 - d / r)", ["x", "z", "d", "r", "h"]), 12, 0, "cone");
        float[] before = (float[])s.Scene.Heights.Clone();
        s.Brush.ErodeWater = true;
        s.Brush.Radius = 12;
        s.Brush.Strength = 1;
        s.BeginStroke(BrushTool.Erode, 64, 64);
        for (int i = 0; i < 20; i++)
        {
            s.StrokeStep(64, 64, 0.1f);
        }
        s.EndStroke();
        Assert.Equal("Erode", s.UndoLabel);
        Assert.Contains(Enumerable.Range(0, before.Length), p => MathF.Abs(before[p] - s.Scene.Heights[p]) > 0.01f);
        // Far from the brush, untouched.
        Assert.Equal(before[10 * s.Scene.W + 10], s.Scene.Heights[10 * s.Scene.W + 10]);
    }

    [Fact]
    public void ABrushEntirelyOutsideTheAreaDoesNothing()
    {
        var s = TestScenes.Flat(2);
        s.Brush.Radius = 3;
        s.BeginStroke(BrushTool.Raise, -50, -50);
        Assert.False(s.StrokeStep(-50, -50, 0.1f));
        Assert.Equal("", s.EndStroke().Shown);
        Assert.False(s.CanUndo);
    }
}
