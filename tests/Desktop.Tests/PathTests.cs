using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Path tool: a line, and an action along it with a width and a soft edge.
public class PathTests
{
	private static PathTool Line(params (float X, float Z)[] pts)
	{
		var p = new PathTool();
		foreach (var (x, z) in pts)
		{
			p.Points.Add(new Vector2(x, z));
		}
		return p;
	}

	private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

	private static List<int> Apply(EditSession s, PathTool p) =>
		s.EditGround("Path", g => { var (t, r, _) = p.Apply(g, s.Brush, s.Scene.Water); return (t, r); });

	[Fact]
	public void TheCurveGoesThroughThePointsAboutAMetreApart()
	{
		var p = Line((10, 10), (30, 10), (30, 30));
		var c = p.Curve();
		Assert.Equal(new Vector2(10, 10), c[0].P);
		Assert.Equal(new Vector2(30, 30), c[^1].P);
		Assert.Contains(c, q => Vector2.Distance(q.P, new Vector2(30, 10)) < 1e-4f);
		Assert.InRange(c.Count, 40, 42);
		// Smooth: it bulges past the corner; straight: exactly 40 m.
		Assert.True(PathTool.Length(c) > 40);
		p.Curved = false;
		Assert.Equal(40, PathTool.Length(p.Curve()), 3);
	}

	[Fact]
	public void FlattenLevelsTheWidthAndBlendsTheSoftEdge()
	{
		var s = EditTests.Flat(2);
		var p = Line((20, 64), (100, 64));
		p.Height = 34;
		p.Width = 8;
		p.Soft = 4;
		Apply(s, p);
		Assert.Equal(34, H(s, 60, 64), 3);
		Assert.Equal(34, H(s, 60, 68), 3);
		float edge = H(s, 60, 70);
		Assert.True(edge > 30 && edge < 34, $"{edge}");
		Assert.Equal(30, H(s, 60, 73), 3);
		Assert.Equal("Path", s.UndoLabel);
	}

	// Smoothing along the area's west edge only looks at ground there, not at the east edge (the 5 × 5
	// kernel wrapped into the row before, at the far side of the area).
	[Fact]
	public void SmoothingAtTheEdgeDoesNotWrapAround()
	{
		var s = EditTests.Flat(2);
		int w = s.Ground.W, hgt = s.Ground.H;
		s.EditGround("east up", g =>
		{
			var touched = new List<int>();
			for (int z = 0; z < hgt; z++)
			{
				for (int x = w - 2; x < w; x++)
				{
					g.SetHeight(z * w + x, 38);
					touched.Add(z * w + x);
				}
			}
			return (touched, (w - 2, 0, w - 1, hgt - 1));
		});
		var p = Line((1, 10), (1, 100));
		p.Act = PathTool.Action.Smooth;
		p.Width = 2;
		p.Soft = 0;
		Apply(s, p);
		Assert.Equal(30, H(s, 1, 60), 3);
		Assert.Equal(30, H(s, 0, 60), 3);
	}

	[Fact]
	public void ARampGoesFromStartToEnd()
	{
		var s = EditTests.Flat(2);
		var p = Line((20, 64), (100, 64));
		p.Act = PathTool.Action.Ramp;
		p.Start = 30;
		p.End = 36;
		p.Soft = 0;
		Apply(s, p);
		Assert.Equal(30, H(s, 20, 64), 2);
		Assert.Equal(33, H(s, 60, 64), 2);
		Assert.Equal(36, H(s, 100, 64), 2);
	}

	[Fact]
	public void ARiverDigsBelowTheSeaAndNeverRaises()
	{
		var s = EditTests.Flat(2);
		var p = Line((20, 64), (100, 64));
		p.Act = PathTool.Action.River;
		p.Depth = 2;
		Apply(s, p);
		// Sea level is 30 here: the middle of the bed is 2 m below it.
		Assert.Equal(s.Scene.Water - 2, H(s, 60, 64), 2);
		Assert.True(H(s, 60, 66) <= 30);
		Assert.Equal(30, H(s, 60, 80), 3);
	}

	[Fact]
	public void PaintingFollowsTheLine()
	{
		var s = EditTests.Flat(2);
		var p = Line((20, 64), (100, 64));
		p.Act = PathTool.Action.PaintPaved;
		Apply(s, p);
		int g = 64 * s.Ground.W + 60;
		Assert.Equal(1, s.Ground.PMod[g]);
		Assert.Equal(1, s.Ground.Paint[g * 4 + 2], 3);
		Assert.Equal(0, s.Ground.PMod[80 * s.Ground.W + 60]);
	}

	[Fact]
	public void ANaturalPathIsRaggedButStillThere()
	{
		var s = EditTests.Flat(2);
		s.Brush.Noise = new Noise(7);
		var p = Line((20, 64), (100, 64));
		p.Act = PathTool.Action.Raise;
		p.Natural = true;
		Apply(s, p);
		var middle = Enumerable.Range(30, 60).Select(x => H(s, x, 64)).ToList();
		Assert.All(middle, h => Assert.True(h > 30));
		Assert.True(middle.Max() - middle.Min() > 0.1f, "bumps");
	}

	private static (MainWindow W, EditSession S, Func<float, float, Avalonia.Point> Screen) Open()
	{
		// Wide, and the line kept near the middle: the tool panels cover the sides.
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.KeyPress(Key.P, RawInputModifiers.None, PhysicalKey.P, "p");
		w.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1.6f);
		// Grid point → screen.
		Avalonia.Point Screen(float gx, float gz)
		{
			var q = Vector4.Transform(new Vector4(gx - 64, 30, -(gz - 64), 1), w.View.ViewProj);
			return new Avalonia.Point((q.X / q.W + 1) / 2 * 1600, (1 - q.Y / q.W) / 2 * 1000);
		}
		return (w, s, Screen);
	}

	private static void Click(MainWindow w, Avalonia.Point at, RawInputModifiers mods = RawInputModifiers.None)
	{
		w.MouseDown(at, MouseButton.Left, mods);
		w.MouseUp(at, MouseButton.Left, mods);
	}

	[AvaloniaFact]
	public void ClicksMakeTheLineAndEnterAppliesIt()
	{
		var (w, s, screen) = Open();
		Assert.Equal(ToolMode.Path, w.View.Mode);
		Assert.True(w.PathPanel.Card.IsVisible);
		Click(w, screen(40, 64));
		Click(w, screen(64, 64));
		Click(w, screen(88, 64));
		Assert.Equal(3, w.View.Path.Points.Count);
		Assert.Equal(64, w.View.Path.Points[1].X, 0);
		Assert.Equal("3 point(s), 48 m long.", w.PathPanel.Info.Text);
		w.PathPanel.HeightBox.Value = 33;
		w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
		Assert.Equal(33, H(s, 64, 64), 2);
		Assert.StartsWith("Applied", w.MessageText.Text);
		// The line is kept; Backspace takes off the last point, Esc clears it.
		w.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
		Assert.Equal(2, w.View.Path.Points.Count);
		w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
		Assert.Empty(w.View.Path.Points);
		Assert.Equal(ToolMode.Path, w.View.Mode);
	}

	[AvaloniaFact]
	public void PointsAreDraggedAddedOnTheLineAndRemovedWithCtrl()
	{
		var (w, _, screen) = Open();
		var p = w.View.Path;
		p.Curved = false;
		Click(w, screen(40, 64));
		Click(w, screen(88, 64));
		// Drag the second point.
		w.MouseDown(screen(88, 64), MouseButton.Left);
		w.MouseMove(screen(88, 80));
		w.MouseUp(screen(88, 80), MouseButton.Left);
		Assert.Equal(2, p.Points.Count);
		Assert.Equal(80, p.Points[1].Y, 0);
		// A drag on the line between them adds a point there.
		var mid = screen(64, 72);
		w.MouseDown(mid, MouseButton.Left);
		w.MouseMove(screen(64, 50));
		w.MouseUp(screen(64, 50), MouseButton.Left);
		Assert.Equal(3, p.Points.Count);
		Assert.Equal(new Vector2(64, 50), new Vector2(MathF.Round(p.Points[1].X), MathF.Round(p.Points[1].Y)));
		// Ctrl + click takes it away again.
		Click(w, screen(64, 50), RawInputModifiers.Control);
		Assert.Equal(2, p.Points.Count);
	}

	[AvaloniaFact]
	public void TheRampFollowsTheGroundUntilTypedAndAltPicksHeights()
	{
		var (w, s, screen) = Open();
		w.PathPanel.ActionBox.SelectedIndex = 1;
		Assert.Equal(PathTool.Action.Ramp, w.View.Path.Act);
		Click(w, screen(40, 64));
		Click(w, screen(88, 64));
		Assert.Equal(30, (double)w.PathPanel.StartBox.Value!, 2);
		w.PathPanel.EndBox.Value = 37;
		Assert.True(w.View.Path.RampEdited);
		Assert.Equal(37, w.View.Path.End);
		// Alt + Shift + click: the ground's height for the end.
		s.Shape(64, 20, Formula.Compile("2", new string[0]), 3, 0, "x");
		Click(w, screen(64, 20), RawInputModifiers.Alt | RawInputModifiers.Shift);
		Assert.Equal(32, w.View.Path.End, 1);
		Assert.Equal(2, w.View.Path.Points.Count);
	}
}
