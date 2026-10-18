using System.Numerics;
using Xunit;
using static TerrainEditor.Desktop.Tests.AreaBoxes;
using static TerrainEditor.Desktop.Tests.GroundActionsSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Every ground action of the Area tool on a flat area (30 m): what it does inside, that the outside is
/// untouched, that the Mask stops it, and the shapes' own edges (an area's last point taken back, a polygon closed with Enter).
/// </summary>
public class GroundActionsTests
{
    /// <summary>
    /// Runs a ground action of the Area tool as one edit step; returns what EditGround returns.
    /// </summary>
    private static List<int> Area(EditSession s, AreaTool a, AreaTool.GroundAction act, float height = 0, float amount = 2, float[]? paint = null, Func<int, float>? mask = null) =>
        s.EditGround($"Area: {act}", g =>
        {
            var (t, rect, _) = a.Apply(g, s.Brush, act, height, amount, paint ?? new float[4], mask);
            return (t, rect);
        });

    [Theory]
    [InlineData(AreaTool.GroundAction.Raise, 32f)]
    [InlineData(AreaTool.GroundAction.Lower, 28f)]
    [InlineData(AreaTool.GroundAction.Flatten, 33.5f)]
    public void AnAreaRaisesLowersAndFlattensItsInsideOnly(AreaTool.GroundAction act, float expected)
    {
        var s = TestScenes.Flat(2);
        var touched = Area(s, Box(40, 40, 60, 60), act, height: 33.5f);
        Assert.NotEmpty(touched);
        Assert.Equal(expected, H(s, 50, 50), 2);
        Assert.Equal(30, H(s, 30, 50), 3);
        Assert.Equal(30, H(s, 70, 70), 3);
        s.Undo();
        Assert.Equal(30, H(s, 50, 50), 3);
    }

    [Fact]
    public void AnAreaSmoothsBumpsInsideOnly()
    {
        var s = Bumpy();
        double inside = Spread(s, 45, 45, 55, 55), outside = Spread(s, 70, 70, 80, 80);
        Area(s, Box(40, 40, 60, 60), AreaTool.GroundAction.Smooth);
        Assert.True(Spread(s, 45, 45, 55, 55) < inside * 0.7, $"{inside:0.000} → {Spread(s, 45, 45, 55, 55):0.000}");
        Assert.Equal(outside, Spread(s, 70, 70, 80, 80), 5);
    }

    [Fact]
    public void AnAreaNaturalizesFlatGroundWithinTheNoise()
    {
        var s = TestScenes.Flat(2);
        Area(s, Box(40, 40, 60, 60), AreaTool.GroundAction.Natural);
        double spread = Spread(s, 42, 42, 58, 58);
        Assert.True(spread > 0.02, $"still flat ({spread:0.000})");
        for (int z = 40; z <= 60; z++)
        {
            for (int x = 40; x <= 60; x++)
            {
                Assert.InRange(H(s, x, z), 30 - s.Brush.NoiseAmp - 0.01f, 30 + s.Brush.NoiseAmp + 0.01f);
            }
        }
        Assert.Equal(30, H(s, 70, 70), 3);
    }

    [Fact]
    public void AnAreaPaintsInsideAndBlendsOverOldPaint()
    {
        var s = TestScenes.Flat(2);
        float[] dirt = Brush.PaintOf(BrushTool.PaintDirt)!;
        float[] paved = Brush.PaintOf(BrushTool.PaintPaved)!;
        Area(s, Box(40, 40, 60, 60), AreaTool.GroundAction.Paint, paint: dirt);
        int p = 50 * s.Ground.W + 50, outside = 70 * s.Ground.W + 70;
        Assert.Equal(1, s.Ground.PMod[p]);
        Assert.Equal(dirt, s.Ground.Paint[(p * 4)..(p * 4 + 4)]);
        Assert.Equal(0, s.Ground.PMod[outside]);
        // Paved over the dirt: the new paint wins inside the hard-edged box.
        Area(s, Box(40, 40, 60, 60), AreaTool.GroundAction.Paint, paint: paved);
        Assert.Equal(paved, s.Ground.Paint[(p * 4)..(p * 4 + 4)]);
    }

    [Fact]
    public void TheMaskStopsAnAreaAction()
    {
        var s = TestScenes.Flat(2);
        var touched = Area(s, Box(40, 40, 60, 60), AreaTool.GroundAction.Raise, mask: _ => 0f);
        Assert.Empty(touched);
        Assert.Equal(30, H(s, 50, 50), 3);
        Assert.False(s.CanUndo);
    }

    [Fact]
    public void AnAreaWithoutAShapeDoesNothing()
    {
        var s = TestScenes.Flat(2);
        Assert.Empty(Area(s, new AreaTool(), AreaTool.GroundAction.Raise));
    }

    [Fact]
    public void APolygonClosesWithEnterAndLosesItsLastPoint()
    {
        var a = new AreaTool { Box = false };
        foreach (var p in new[] { new Vector2(10, 10), new Vector2(30, 10), new Vector2(30, 30) })
        {
            a.Down(p, 1);
            a.Up();
        }
        a.RemoveLast();
        Assert.Equal(2, a.Points.Count);
        // Two points are not a polygon yet: Enter does not close it.
        Assert.False(a.CloseWithEnter());
        a.Down(new Vector2(10, 30), 1);
        a.Up();
        Assert.True(a.CloseWithEnter());
        Assert.True(a.Closed);
        Assert.False(a.CloseWithEnter());
        // A box has no points to take back one by one.
        var box = Box(0, 0, 10, 10);
        box.RemoveLast();
        Assert.Equal(2, box.Points.Count);
    }
}
