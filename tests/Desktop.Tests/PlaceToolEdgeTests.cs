using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Place tool's less common paths: painting while dragging (how many per moment, spacing kept),
// erasing under the brush, turning a grid or zone and the view's turn, a ring of walls end to end, a
// closed loop, shapes too small to hold anything, a line continuing a wall already there, and picking
// the height from a piece's top.
public class PlaceToolEdgeTests
{
	private const string Wall = "woodwall";

	private static (PlaceTool T, EditSession S) Tool(params WorldScene.Thing[] things)
	{
		var s = EditTests.Flat(2, things);
		var t = new PlaceTool { Scene = () => s.Scene, Brush = s.Brush, NameOf = h => h == StableHash.Of(Wall) ? Wall : h == StableHash.Of("Beech1") ? "Beech1" : null };
		t.Chosen.Clear();
		t.Chosen.Add("Beech1");
		t.Tilt = 0;
		return (t, s);
	}

	private static WorldScene.Thing Tree(int id, float gx, float gz) => new(id, StableHash.Of("Beech1"), new Vector3(gx - 32, 30, gz - 32), Vector3.Zero, 1, false);

	[Fact]
	public void PaintingAddsAFewEachMomentAndKeepsTheSpacing()
	{
		var (t, _) = Tool();
		t.Brush.Radius = 10;
		t.Brush.Strength = 1;
		t.Density = 5;
		t.Spacing = 3;
		var all = new List<PlaceTool.Placement>();
		for (int i = 0; i < 20; i++)
		{
			all.AddRange(t.PaintStep(new Vector2(64, 64), 0.1f, all));
		}
		// About 5 per 100 m² over the brush's 314 m², a little more for the strength: the brush fills up.
		Assert.InRange(all.Count, 8, 40);
		Assert.All(all, p => Assert.True(Vector2.Distance(p.G, new Vector2(64, 64)) <= 10.01f));
		for (int i = 0; i < all.Count; i++)
		{
			for (int j = i + 1; j < all.Count; j++)
			{
				Assert.True(Vector2.Distance(all[i].G, all[j].G) >= 3 - 1e-3f);
			}
		}
	}

	[Fact]
	public void PaintingWithNothingChosenOrNoAreaAddsNothing()
	{
		var (t, _) = Tool();
		t.Chosen.Clear();
		Assert.Empty(t.PaintStep(new Vector2(64, 64), 0.1f, new()));
		var none = new PlaceTool { Scene = () => null };
		Assert.Empty(none.PaintStep(new Vector2(64, 64), 0.1f, new()));
		Assert.Empty(none.EraseAt(new Vector2(64, 64)));
	}

	[Fact]
	public void EraseTakesTheChosenKindsUnderTheBrushOnly()
	{
		var wall = new WorldScene.Thing(4, StableHash.Of(Wall), new Vector3(32, 30, 32), Vector3.Zero, 0, true);
		var gone = Tree(5, 66, 64) with { Gone = true };
		var (t, s) = Tool(Tree(1, 64, 64), Tree(2, 68, 64), Tree(3, 90, 64), wall, gone);
		t.Brush.Radius = 6;
		Assert.Equal(new[] { 0, 1 }, t.EraseAt(new Vector2(64, 64)));
		// The Mask leaves out what it does not pass.
		t.Mask = () => p => p % s.Scene.W < 66 ? 1 : 0;
		Assert.Equal(new[] { 0 }, t.EraseAt(new Vector2(64, 64)));
	}

	[Fact]
	public void AGridTurnsAroundItsMiddle()
	{
		var (t, _) = Tool();
		t.Mode = PlaceTool.Modes.Grid;
		Assert.False(t.TurnShape(90));
		t.GridA = new Vector2(40, 60);
		t.GridB = new Vector2(80, 68);
		var flat = t.Preview(null);
		Assert.True(t.TurnShape(90));
		Assert.Equal(90, t.ShapeTurn, 3);
		var turned = t.Preview(null);
		Assert.Equal(flat.Count, turned.Count);
		// Wide along x before, along z after.
		float Span(List<PlaceTool.Placement> p, Func<Vector2, float> f) => p.Max(o => f(o.G)) - p.Min(o => f(o.G));
		Assert.True(Span(flat, g => g.X) > Span(flat, g => g.Y));
		Assert.True(Span(turned, g => g.Y) > Span(turned, g => g.X));
		// A turned point taken back is where it started.
		var q = new Vector2(50, 61);
		Assert.True(Vector2.Distance(q, t.Inv(t.Xf(q))) < 1e-3f);
		// Turning wraps around.
		t.TurnShape(180);
		Assert.Equal(-90, t.ShapeTurn, 3);
		t.ClearShape();
		Assert.Equal(0, t.ShapeTurn);
	}

	[Fact]
	public void TheTurnWrapsBetweenMinus180And180()
	{
		var (t, _) = Tool();
		t.TurnBy(170);
		t.TurnBy(30);
		Assert.Equal(-160, t.Rotation, 3);
		t.TurnBy(-40);
		Assert.Equal(160, t.Rotation, 3);
	}

	[Fact]
	public void ARingOfWallsMeetsEndToEnd()
	{
		var (t, _) = Tool();
		t.Chosen.Clear();
		t.Chosen.Add(Wall);
		t.AutoSnap();
		t.Mode = PlaceTool.Modes.Line;
		t.LineShape = PlaceTool.LineShapes.Circle;
		var c = new Vector2(64, 64);
		t.FigA = c;
		t.FigB = new Vector2(72, 64);
		var e = t.EndsOf(Wall)!;
		var p = t.Preview(null);
		// The radius is fitted so whole walls go round.
		float r = Vector2.Distance(c, t.SnapFigure(c, t.FigB.Value));
		int n = Math.Max(3, (int)MathF.Round(MathF.Tau * r / e.Len));
		Assert.Equal(n, p.Count);
		// The corners of a regular polygon: every piece the same distance from the middle.
		var d = p.Select(o => Vector2.Distance(o.G, c)).ToList();
		Assert.True(d.Max() - d.Min() < 0.05f, $"{d.Min()} … {d.Max()}");
		// Closed: the last wall ends where the first began (it was missing once, a hair past the end).
		Assert.True(t.SnapGap < 0.01f, $"gap {t.SnapGap}");
	}

	[Fact]
	public void ShapesTooSmallHoldNothing()
	{
		var (t, _) = Tool();
		t.Chosen.Clear();
		t.Chosen.Add(Wall);
		t.Mode = PlaceTool.Modes.Line;
		t.LineShape = PlaceTool.LineShapes.Rect;
		t.FigA = new Vector2(40, 40);
		t.FigB = new Vector2(40.2f, 50);
		Assert.Empty(t.Preview(null));
		t.LineShape = PlaceTool.LineShapes.Circle;
		t.FigB = new Vector2(40.3f, 40);
		Assert.Empty(t.Preview(null));
	}

	[Fact]
	public void ALoopClosesTheLine()
	{
		var (t, _) = Tool();
		t.Mode = PlaceTool.Modes.Line;
		t.Curve = false;
		foreach (var p in new[] { new Vector2(40, 40), new Vector2(80, 40), new Vector2(80, 80) })
		{
			t.Points.Add(p);
		}
		int open = t.Preview(null).Count;
		t.Loop = true;
		Assert.True(t.Preview(null).Count > open);
		var run = Assert.Single(t.LineRuns());
		Assert.Equal(run[0], run[^1]);
	}

	[Fact]
	public void ALineStartedAtAWallContinuesIt()
	{
		// A wall along x at grid (60, 64): its east end at 61.
		var wall = new WorldScene.Thing(1, StableHash.Of(Wall), new Vector3(60 - 32, 30, 64 - 32), Vector3.Zero, 0, true);
		var (t, _) = Tool(wall);
		t.Chosen.Clear();
		t.Chosen.Add(Wall);
		t.AutoSnap();
		t.Mode = PlaceTool.Modes.Line;
		t.Points.Add(new Vector2(61.6f, 64.3f));
		t.Points.Add(new Vector2(75, 64.3f));
		var p = t.Preview(null);
		Assert.NotEmpty(p);
		// The first new wall starts exactly at the old one's end, on its line.
		var e = t.EndsOf(Wall)!;
		Assert.Equal(61 + e.Len / 2, p[0].G.X, 1);
		Assert.InRange(p[0].G.Y, 63.8f, 64.2f);
	}

	[Fact]
	public void TheHeightIsPickedFromAPiecesTop()
	{
		var wall = new WorldScene.Thing(1, StableHash.Of(Wall), new Vector3(0, 31, 0), Vector3.Zero, 0, true);
		var (t, _) = Tool(wall);
		string said = t.PickElevation(0, 30);
		Assert.Equal(PlaceTool.Elevations.At, t.Elevation);
		Assert.Equal(31 + PlaceTool.RotU(new Vector3(0, 1, 0), Vector3.Zero).Y, t.Elev, 2);
		Assert.Contains("the top of woodwall", said);
	}
}
