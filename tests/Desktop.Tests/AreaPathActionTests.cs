using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Every ground action of the Area and Path tools on a flat area (30 m): what it does inside, that the
// outside is untouched, that the Mask stops it, and the shapes' own edges (a path drawn freehand, an
// area's last point taken back, a polygon closed with Enter).
public class AreaPathActionTests
{
	private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

	private static AreaTool Box(float x0, float z0, float x1, float z1)
	{
		var a = new AreaTool { Soft = 0 };
		a.Points.Add(new Vector2(x0, z0));
		a.Points.Add(new Vector2(x1, z1));
		return a;
	}

	private static List<int> Area(EditSession s, AreaTool a, AreaTool.GroundAction act, float height = 0, float amount = 2, float[]? paint = null, Func<int, float>? mask = null) =>
		s.EditGround($"Area: {act}", g => a.Apply(g, s.Brush, act, height, amount, paint ?? new float[4], mask));

	// Bumpy ground: a 1 m ripple raised over the whole area.
	private static EditSession Bumpy()
	{
		var s = EditTests.Flat(2);
		s.Shape(64, 64, Formula.Compile("sin(x) + cos(z)", new[] { "x", "z", "d", "r", "h" }), 60, 0, "ripple");
		return s;
	}

	private static double Spread(EditSession s, int x0, int z0, int x1, int z1)
	{
		var v = new List<double>();
		for (int z = z0; z <= z1; z++)
		{
			for (int x = x0; x <= x1; x++)
			{
				v.Add(H(s, x, z));
			}
		}
		double m = v.Average();
		return Math.Sqrt(v.Average(h => (h - m) * (h - m)));
	}

	[Theory]
	[InlineData(AreaTool.GroundAction.Raise, 32f)]
	[InlineData(AreaTool.GroundAction.Lower, 28f)]
	[InlineData(AreaTool.GroundAction.Flatten, 33.5f)]
	public void AnAreaRaisesLowersAndFlattensItsInsideOnly(AreaTool.GroundAction act, float expected)
	{
		var s = EditTests.Flat(2);
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
		var s = EditTests.Flat(2);
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
		var s = EditTests.Flat(2);
		var dirt = Brush.PaintOf(BrushTool.PaintDirt)!;
		var paved = Brush.PaintOf(BrushTool.PaintPaved)!;
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
		var s = EditTests.Flat(2);
		var touched = Area(s, Box(40, 40, 60, 60), AreaTool.GroundAction.Raise, mask: _ => 0f);
		Assert.Empty(touched);
		Assert.Equal(30, H(s, 50, 50), 3);
		Assert.False(s.CanUndo);
	}

	[Fact]
	public void AnAreaWithoutAShapeDoesNothing()
	{
		var s = EditTests.Flat(2);
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

	private static PathTool Line(PathTool.Action act, float width = 8)
	{
		var p = new PathTool { Act = act, Width = width, Soft = 0, Amount = 2, Curved = false };
		p.Points.Add(new Vector2(20, 64));
		p.Points.Add(new Vector2(108, 64));
		return p;
	}

	private static List<int> Apply(EditSession s, PathTool p, Func<int, float>? mask = null) =>
		s.EditGround("Path", g => { var (t, r, _) = p.Apply(g, s.Brush, s.Scene.Water, mask); return (t, r); });

	[Theory]
	[InlineData(PathTool.Action.Raise, 32f)]
	[InlineData(PathTool.Action.Lower, 28f)]
	public void APathRaisesAndLowersAlongItsWidth(PathTool.Action act, float expected)
	{
		var s = EditTests.Flat(2);
		Apply(s, Line(act));
		Assert.Equal(expected, H(s, 64, 64), 2);
		Assert.Equal(expected, H(s, 64, 67), 2);
		Assert.Equal(30, H(s, 64, 72), 3);
		Assert.Equal(30, H(s, 10, 64), 3);
	}

	[Fact]
	public void APathSmoothsAlongItself()
	{
		var s = Bumpy();
		double along = Spread(s, 30, 62, 100, 66), beside = Spread(s, 30, 90, 100, 94);
		Apply(s, Line(PathTool.Action.Smooth));
		Assert.True(Spread(s, 30, 62, 100, 66) < along * 0.8, $"{along:0.000} → {Spread(s, 30, 62, 100, 66):0.000}");
		Assert.Equal(beside, Spread(s, 30, 90, 100, 94), 5);
	}

	[Theory]
	[InlineData(PathTool.Action.PaintDirt, BrushTool.PaintDirt)]
	[InlineData(PathTool.Action.PaintPaved, BrushTool.PaintPaved)]
	[InlineData(PathTool.Action.PaintCultivated, BrushTool.PaintCultivated)]
	public void APathPaintsItsKind(PathTool.Action act, BrushTool kind)
	{
		var s = EditTests.Flat(2);
		Apply(s, Line(act));
		int p = 64 * s.Ground.W + 64;
		Assert.Equal(1, s.Ground.PMod[p]);
		Assert.Equal(Brush.PaintOf(kind), s.Ground.Paint[(p * 4)..(p * 4 + 4)]);
		Assert.Equal(30, H(s, 64, 64), 3);
	}

	[Fact]
	public void ClearPaintAlongAPathTakesPaintAway()
	{
		var s = EditTests.Flat(2);
		Apply(s, Line(PathTool.Action.PaintDirt));
		Apply(s, Line(PathTool.Action.PaintClear));
		int p = 64 * s.Ground.W + 64;
		Assert.Equal(Brush.PaintOf(BrushTool.PaintClear), s.Ground.Paint[(p * 4)..(p * 4 + 4)]);
	}

	[Fact]
	public void APathNeedsTwoPointsAndTheMaskStopsIt()
	{
		var s = EditTests.Flat(2);
		var one = new PathTool { Act = PathTool.Action.Raise };
		one.Points.Add(new Vector2(64, 64));
		Assert.Empty(Apply(s, one));
		Assert.Empty(Apply(s, Line(PathTool.Action.Raise), _ => 0f));
		Assert.Equal(30, H(s, 64, 64), 3);
	}

	[Fact]
	public void APathIsDrawnByDraggingAPointEveryTwoMetres()
	{
		var p = new PathTool();
		var size = new Avalonia.Size(100, 100);
		p.Down(new Vector2(10, 10), new Avalonia.Point(0, 0), _ => null, false, false, false, _ => 30f);
		for (int i = 1; i <= 20; i++)
		{
			p.Moved(new Vector2(10 + i * 0.5f, 10));
		}
		p.Up();
		// 10 m dragged: the first point and one every 2 m.
		Assert.Equal(6, p.Points.Count);
		Assert.Equal(new Vector2(20, 10), p.Points[^1]);
		// After the button is up, moving adds nothing.
		p.Moved(new Vector2(40, 10));
		Assert.Equal(6, p.Points.Count);
	}
}
