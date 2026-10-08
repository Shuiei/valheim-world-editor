using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Place tool's shapes with the mouse, looking down on a flat 2 × 2 zone area: line points dragged,
// removed and inserted, freehand lines, a double-click to place, grid boxes, circles and rectangles,
// the eyedropper, picking a height, and the keys.
public class PlaceMouseTests
{
	private sealed record View(MainWindow W, EditSession S, Avalonia.Size Size)
	{
		public PlaceInput In => W.PlaceInput;
		public PlaceTool Tool => W.PlaceTool;

		// Grid point (gx, gz) on screen (the ground is at 30 m).
		public Avalonia.Point At(float gx, float gz)
		{
			var q = Vector4.Transform(new Vector4(gx - 64, 30, -(gz - 64), 1), W.View.ViewProj);
			return new Avalonia.Point((q.X / q.W + 1) / 2 * Size.Width, (1 - q.Y / q.W) / 2 * Size.Height);
		}

		public void Click(float gx, float gz, bool ctrl = false, int clicks = 1, bool alt = false)
		{
			In.Down(At(gx, gz), Size, false, ctrl, alt, clicks);
			In.Up(At(gx, gz), Size);
		}

		public void Drag(params (float X, float Z)[] path)
		{
			In.Down(At(path[0].X, path[0].Z), Size, false, false, false, 1);
			foreach (var (x, z) in path.Skip(1))
			{
				In.Moved(At(x, z), Size);
			}
			In.Up(At(path[^1].X, path[^1].Z), Size);
		}

		public string? Said;
	}

	private static View Open(PlaceTool.Modes mode, params WorldScene.Thing[] things)
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2, things);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.Tools.ChooseMode(ToolMode.Place);
		w.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1.6f);
		w.PlaceTool.Chosen.Clear();
		w.PlaceTool.Chosen.Add("Beech1");
		w.PlaceTool.Mode = mode;
		var v = new View(w, s, new Avalonia.Size(1600, 1000));
		w.PlaceInput.Message += t => v.Said = t;
		return v;
	}

	private static Vector2 Pt(View v, int i) => v.Tool.Points[i];

	[AvaloniaFact]
	public void LinePointsAreDraggedRemovedAndInserted()
	{
		var v = Open(PlaceTool.Modes.Line);
		v.Click(40, 64);
		v.Click(64, 64);
		v.Click(88, 64);
		Assert.Equal(3, v.Tool.Points.Count);
		var middle = Pt(v, 1);
		// Dragging the middle point moves it.
		v.Drag((64, 64), (64, 70), (64, 76));
		Assert.Equal(3, v.Tool.Points.Count);
		Assert.True(Vector2.Distance(Pt(v, 1), middle + new Vector2(0, 12)) < 1, $"{Pt(v, 1)}");
		// A click on the line between two points adds one there.
		v.Click(52, 70);
		Assert.Equal(4, v.Tool.Points.Count);
		// Ctrl + click on a point removes it.
		v.Click(88, 64, ctrl: true);
		Assert.Equal(3, v.Tool.Points.Count);
		Assert.True(v.In.Shown.Length > 0);
	}

	[AvaloniaFact]
	public void ADoubleClickOnTheLastPointPlacesTheLine()
	{
		var v = Open(PlaceTool.Modes.Line);
		v.Click(40, 64);
		v.Click(88, 64);
		int shown = v.In.Shown.Length;
		Assert.True(shown > 0);
		v.Click(88, 64, clicks: 2);
		Assert.Equal(shown, v.S.Pending.Added);
		Assert.StartsWith("Placed", v.S.UndoLabel);
	}

	[AvaloniaFact]
	public void ADragDrawsAFreehandLine()
	{
		var v = Open(PlaceTool.Modes.Line);
		v.Drag((30, 40), (34, 44), (38, 48), (42, 52), (46, 56), (50, 60));
		// A point every 2 m or more along the drag.
		Assert.InRange(v.Tool.Points.Count, 4, 12);
		Assert.True(Vector2.Distance(Pt(v, 0), Pt(v, v.Tool.Points.Count - 1)) > 20);
	}

	[AvaloniaFact]
	public void PlacingWithoutAShapeSaysWhatToDo()
	{
		foreach (var (mode, text) in new[] { (PlaceTool.Modes.Line, "Draw a line first"), (PlaceTool.Modes.Zone, "Draw a zone first"), (PlaceTool.Modes.Grid, "Drag a box on the ground first") })
		{
			var v = Open(mode);
			v.In.PlaceShape();
			Assert.StartsWith(text, v.Said);
			Assert.Equal(0, v.S.Pending.Added);
		}
	}

	[AvaloniaFact]
	public void AGridIsDraggedTurnedAndPlaced()
	{
		var v = Open(PlaceTool.Modes.Grid);
		// Turning before there is a box says so.
		Assert.True(v.In.Key(Avalonia.Input.Key.OemPeriod, false, false));
		Assert.StartsWith("Drag a box first", v.Said);
		v.Drag((50, 50), (60, 60), (70, 66));
		Assert.NotNull(v.Tool.GridA);
		Assert.NotNull(v.Tool.GridB);
		int shown = v.In.Shown.Length;
		Assert.True(shown > 1);
		Assert.True(v.In.Key(Avalonia.Input.Key.OemPeriod, true, false));
		Assert.True(v.In.Key(Avalonia.Input.Key.Enter, false, false));
		Assert.True(v.S.Pending.Added > 1);
		// A click without dragging clears the box.
		v.Click(30, 30);
		Assert.Null(v.Tool.GridA);
	}

	[AvaloniaFact]
	public void CirclesAndRectanglesAreDraggedOut()
	{
		foreach (var shape in new[] { PlaceTool.LineShapes.Circle, PlaceTool.LineShapes.Rect })
		{
			var v = Open(PlaceTool.Modes.Line);
			v.Tool.LineShape = shape;
			v.Tool.Chosen.Clear();
			v.Tool.Chosen.Add("wood_wall_half");
			// A click without dragging explains how.
			v.Click(64, 64);
			Assert.StartsWith(shape == PlaceTool.LineShapes.Circle ? "Press at the centre" : "Press at a corner", v.Said);
			Assert.Null(v.Tool.FigA);
			v.Drag((64, 64), (70, 68), (74, 72));
			Assert.NotNull(v.Tool.FigA);
			Assert.NotNull(v.Tool.FigB);
			Assert.True(v.In.Shown.Length >= 4, $"{shape}: {v.In.Shown.Length} pieces");
		}
	}

	[AvaloniaFact]
	public void TheEyedropperPicksTheKindUnderTheCursor()
	{
		var v = Open(PlaceTool.Modes.Brush, new WorldScene.Thing(1, StableHash.Of("Oak1"), new Vector3(64 - 32, 30, 64 - 32), Vector3.Zero, 1, false));
		var s = v.S.Scene;
		v.W.View.SetBoxes(new[] { (0, new Vector3(64 - 32 - 1 - s.Cx, 30, -(64 - 32 + 1 - s.Cz)), new Vector3(64 - 32 + 1 - s.Cx, 36, -(64 - 32 - 1 - s.Cz))) });
		string? picked = null;
		v.In.PickOnce = n => picked = n;
		v.Click(10, 10);
		Assert.Null(picked);
		Assert.StartsWith("No object there", v.Said);
		Assert.Null(v.In.PickOnce);
		v.In.PickOnce = n => picked = n;
		v.Click(64, 64);
		Assert.Equal("Oak1", picked);
	}

	[AvaloniaFact]
	public void AltClickPicksTheHeightAndPageKeysNudgeIt()
	{
		var v = Open(PlaceTool.Modes.Brush);
		v.Click(64, 64, alt: true);
		Assert.StartsWith("Elevation: everything at 30.00 m (the ground there)", v.Said);
		Assert.Equal(PlaceTool.Elevations.At, v.Tool.Elevation);
		Assert.True(v.In.Key(Avalonia.Input.Key.PageUp, false, false));
		Assert.Equal("Elevation: at 30.5 m.", v.Said);
		Assert.True(v.In.Key(Avalonia.Input.Key.PageDown, true, false));
		Assert.Equal("Elevation: at 30.4 m.", v.Said);
	}

	[AvaloniaFact]
	public void PageUpFromTheGroundLiftsAboveIt()
	{
		var v = Open(PlaceTool.Modes.Brush);
		Assert.Equal(PlaceTool.Elevations.Ground, v.Tool.Elevation);
		v.In.Key(Avalonia.Input.Key.PageUp, false, false);
		Assert.Equal(PlaceTool.Elevations.Above, v.Tool.Elevation);
		Assert.Equal("Elevation: above the ground by 0.5 m.", v.Said);
	}

	[AvaloniaFact]
	public void RAndCommaAndPeriodChangeTheLayout()
	{
		var v = Open(PlaceTool.Modes.Brush);
		v.In.Moved(v.At(64, 64), v.Size);
		var before = v.In.Shown.Select(t => t.Position).ToList();
		Assert.NotEmpty(before);
		Assert.True(v.In.Key(Avalonia.Input.Key.R, false, false));
		v.In.Moved(v.At(64, 64), v.Size);
		Assert.NotEqual(before, v.In.Shown.Select(t => t.Position).ToList());
		Assert.False(v.In.Key(Avalonia.Input.Key.Q, false, false));
	}
}
