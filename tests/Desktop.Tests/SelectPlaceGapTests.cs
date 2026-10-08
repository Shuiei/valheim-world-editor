using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The rest of the Select and Place tools' mouse and keys: the up and north arrows (Ctrl: half-metre
// steps), a click on one of several selected objects, the tools' calls with nothing to work on; the
// Place brush painting while it is dragged, Shift turning the preview into the eraser, a click on a
// zone's edge adding a point, and turning the brush's pieces with the keys.
[Collection("World files")]
public class SelectPlaceGapTests
{
	private static (MainWindow W, EditSession S, Func<Vector3, Avalonia.Point> Screen) Open(Vector3 eyeFromWall, params WorldScene.Thing[] things)
	{
		var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2, things.Length > 0 ? things : new[] { new WorldScene.Thing(11, StableHash.Of("wood_wall_half"), new Vector3(40, 30, 40), Vector3.Zero, 0, true) });
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.Tools.ChooseSelect();
		w.View.Select(new[] { 0 });
		var c = new Vector3(40 - s.Scene.Cx, 30, -(40 - s.Scene.Cz));
		w.View.SetCamera(c + eyeFromWall, c, 1);
		var size = w.View.Bounds.Size;
		Avalonia.Point Screen(Vector3 p)
		{
			var q = Vector4.Transform(new Vector4(p, 1), w.View.ViewProj);
			return new Avalonia.Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
		}
		return (w, s, Screen);
	}

	[AvaloniaFact]
	public void TheNorthArrowWithCtrlMovesByHalfMetres()
	{
		var (w, s, screen) = Open(new Vector3(0, 30, 3));
		var (c, scale) = w.View.GizmoAt()!.Value;
		// North is -z in view space.
		var from = screen(c + new Vector3(0, 0, -0.7f * scale));
		var to = screen(c + new Vector3(0, 0, -0.7f * scale - 3.3f));
		w.MouseMove(from);
		Assert.Equal(Gizmo.Handle.Z, w.View.SelectTool.HotHandle);
		w.MouseDown(from, MouseButton.Left);
		w.MouseMove(to, RawInputModifiers.Control);
		w.MouseUp(to, MouseButton.Left, RawInputModifiers.Control);
		var moved = s.Scene.Things[^1];
		Assert.Equal(43.5f, moved.Position.Z, 2);
		Assert.Equal(40, moved.Position.X, 2);
	}

	[AvaloniaFact]
	public void TheUpArrowLiftsTheSelection()
	{
		var (w, s, screen) = Open(new Vector3(25, 4, 25));
		w.View.SelectTool.OnGround = false;
		var (c, scale) = w.View.GizmoAt()!.Value;
		var from = screen(c + new Vector3(0, 0.7f * scale, 0));
		var to = screen(c + new Vector3(0, 0.7f * scale + 2, 0));
		w.MouseMove(from);
		Assert.Equal(Gizmo.Handle.Y, w.View.SelectTool.HotHandle);
		w.MouseDown(from, MouseButton.Left);
		w.MouseMove(to);
		w.MouseUp(to, MouseButton.Left);
		Assert.Equal(32, s.Scene.Things[^1].Position.Y, 1);
	}

	[AvaloniaFact]
	public void ToolCallsWithNothingToWorkOnDoNothing()
	{
		var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
		w.Show();
		var t = w.View.SelectTool;
		// No area open: nothing to turn, lift, drop, select or delete.
		t.Turn(15);
		t.Lift(1);
		t.Drop();
		t.SameKind();
		t.Invert();
		t.WholeBuilding();
		t.Delete();
		t.Commit();
		Assert.False(t.Cancel());
		Assert.Null(t.Where());
		Assert.False(t.Moving);
		Assert.Empty(w.View.Selected);
	}

	[AvaloniaFact]
	public void TheBrushPaintsWhileDraggedAndShiftShowsTheEraser()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.Tools.ChooseMode(ToolMode.Place);
		w.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1.6f);
		w.PlaceTool.Chosen.Clear();
		w.PlaceTool.Chosen.Add("Beech1");
		w.PlaceTool.Brush.Radius = 12;
		w.PlaceTool.Density = 5;
		var size = new Avalonia.Size(1600, 1000);
		Avalonia.Point At(float gx, float gz)
		{
			var q = Vector4.Transform(new Vector4(gx - 64, 30, -(gz - 64), 1), w.View.ViewProj);
			return new Avalonia.Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
		}
		var input = w.PlaceInput;
		input.Moved(At(64, 64), size);
		Assert.NotEmpty(input.Shown);
		// Shift held: the preview goes (a drag would remove instead).
		input.ShiftHeld(true);
		Assert.Empty(input.Shown);
		input.ShiftHeld(false);
		Assert.NotEmpty(input.Shown);
		int stamp = input.Shown.Length;
		input.Down(At(64, 64), size, false, false, false, 1);
		var paint = typeof(PlaceInput).GetMethod("Paint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
		foreach (float x in new[] { 70f, 76f, 82f })
		{
			input.Moved(At(x, 64), size);
			Thread.Sleep(60);
			paint.Invoke(input, null);
		}
		input.Up(At(82, 64), size);
		// The click's own layout and more along the drag, in one step.
		Assert.True(s.Pending.Added > stamp, $"{s.Pending.Added} placed, {stamp} at the click");
		Assert.StartsWith("Placed", s.UndoLabel);
		// The keys turn the pieces placed with the brush.
		Assert.True(input.Key(Key.OemPeriod, false, false));
		Assert.Equal(1, w.PlaceTool.Rotation, 3);
		Assert.True(input.Key(Key.OemComma, true, false));
		Assert.Equal(-14, w.PlaceTool.Rotation, 3);
	}

	[AvaloniaFact]
	public void AClickOnAZonesEdgeAddsAPoint()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.Tools.ChooseMode(ToolMode.Place);
		w.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1.6f);
		w.PlaceTool.Mode = PlaceTool.Modes.Zone;
		var size = new Avalonia.Size(1600, 1000);
		Avalonia.Point At(float gx, float gz)
		{
			var q = Vector4.Transform(new Vector4(gx - 64, 30, -(gz - 64), 1), w.View.ViewProj);
			return new Avalonia.Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
		}
		var input = w.PlaceInput;
		foreach (var (x, z) in new[] { (40f, 40f), (90f, 40f), (90f, 90f) })
		{
			input.Down(At(x, z), size, false, false, false, 1);
			input.Up(At(x, z), size);
		}
		Assert.Equal(3, w.PlaceTool.Points.Count);
		// On the edge from the first point to the second.
		input.Down(At(65, 40), size, false, false, false, 1);
		input.Up(At(65, 40), size);
		Assert.Equal(4, w.PlaceTool.Points.Count);
		Assert.Equal(65, w.PlaceTool.Points[1].X, 0);
	}
}
