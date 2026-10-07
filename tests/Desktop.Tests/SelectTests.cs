using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Deleting and moving objects: the edit session's changes (with undo), the Select tool's keys and
// exact place, and a moved object saved into a world.
public class SelectTests
{
	private static readonly int Beech = StableHash.Of("Beech1");
	private static readonly int Wall = StableHash.Of("wood_wall_half");

	// A flat 2 × 2 zone area (world x and z from -32 to 96) with a tree and a wall.
	private static EditSession Area() => EditTests.Flat(2,
		new WorldScene.Thing(10, Beech, new Vector3(20, 30, 20), Vector3.Zero, 1, false),
		new WorldScene.Thing(11, Wall, new Vector3(40, 30, 40), new Vector3(0, 90, 0), 0, true));

	[Fact]
	public void DeletingHidesTheObjectsUntilUndone()
	{
		var s = Area();
		var changed = new List<int>();
		s.ThingsChanged += l => changed.AddRange(l);
		s.Delete(new[] { 0 });
		Assert.True(s.Scene.Things[0].Gone);
		Assert.Contains(10, s.Edits.Deleted);
		Assert.Equal((0, 1, 0, 0), s.Pending);
		Assert.Equal(new[] { 0 }, changed);
		Assert.Equal("Deleted 1", s.UndoLabel);
		s.Undo();
		Assert.False(s.Scene.Things[0].Gone);
		Assert.Empty(s.Edits.Deleted);
		s.Redo();
		Assert.True(s.Scene.Things[0].Gone);
	}

	[Fact]
	public void AMoveReplacesTheObjectWithACopyThatKeepsItsData()
	{
		var s = Area();
		var copies = s.Move(new[] { (0, new Vector3(25, 31, 20), new Vector3(0, 45, 0)) });
		Assert.Equal(new[] { 2 }, copies);
		Assert.True(s.Scene.Things[0].Gone);
		var copy = s.Scene.Things[2];
		Assert.Equal(new Vector3(25, 31, 20), copy.Position);
		Assert.True(copy.Id < 0);
		var added = Assert.Single(s.Edits.Added);
		// Written from the original's own data (chest contents, builder…), not as a fresh object.
		Assert.Equal(10, added.SourceId);
		Assert.False(added.Fresh);
		Assert.Equal((0, 1, 1, 0), s.Pending);

		// Moving the copy again still copies the original.
		var again = s.Move(new[] { (2, new Vector3(30, 31, 20), new Vector3(0, 45, 0)) });
		var second = s.Edits.FindAdded(s.Scene.Things[again[0]].Id)!;
		Assert.Equal(10, second.SourceId);
		Assert.Single(s.Edits.Added);

		s.Undo();
		s.Undo();
		Assert.False(s.Scene.Things[0].Gone);
		Assert.True(s.Scene.Things[2].Gone && s.Scene.Things[3].Gone);
		Assert.Equal((0, 0, 0, 0), s.Pending);
	}

	private static (MainWindow W, EditSession S) Window()
	{
		var w = new MainWindow(load: false) { Width = 1200, Height = 800 };
		w.Show();
		var s = Area();
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.Tools.ChooseSelect();
		return (w, s);
	}

	private static WorldScene.Thing Last(EditSession s) => s.Scene.Things[^1];

	[AvaloniaFact]
	public void TheSelectToolIsOnTheRailAndOnE()
	{
		var w = new MainWindow(load: false) { Width = 1000, Height = 700 };
		w.Show();
		w.KeyPress(Key.E, RawInputModifiers.None, PhysicalKey.E, "e");
		Assert.True(w.Tools.SelectMode);
		Assert.True(w.View.SelectMode);
		Assert.True(w.SelectPanel.Card.IsVisible);
		w.KeyPress(Key.D1, RawInputModifiers.None, PhysicalKey.Digit1, "1");
		Assert.False(w.View.SelectMode);
		Assert.False(w.SelectPanel.Card.IsVisible);
	}

	[AvaloniaFact]
	public void TurningAndLiftingWithKeysMovesTheSelectionOnceTheKeysStop()
	{
		var (w, s) = Window();
		w.View.Select(new[] { 0 });
		w.KeyPress(Key.OemPeriod, RawInputModifiers.Shift, PhysicalKey.Period, ">");
		w.KeyPress(Key.PageUp, RawInputModifiers.None, PhysicalKey.PageUp, null);
		Assert.True(w.View.SelectTool.Moving);
		// The move is written when the keys stop (or at once on a commit).
		w.View.SelectTool.Commit();
		var moved = Last(s);
		Assert.Equal(15, moved.Rotation.Y, 3);
		// On the ground (30 m) and lifted a quarter metre.
		Assert.Equal(30.25f, moved.Position.Y, 3);
		Assert.Equal(new Vector2(20, 20), new Vector2(moved.Position.X, moved.Position.Z));
		Assert.Equal(new[] { s.Scene.Things.Count - 1 }, w.View.Selected);
		// Ctrl+Z puts it back.
		w.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
		Assert.False(s.Scene.Things[0].Gone);
	}

	[AvaloniaFact]
	public void EscapeCancelsAMove()
	{
		var (w, s) = Window();
		w.View.Select(new[] { 0 });
		w.KeyPress(Key.PageUp, RawInputModifiers.None, PhysicalKey.PageUp, null);
		w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
		Assert.False(w.View.SelectTool.Moving);
		Assert.Equal(2, s.Scene.Things.Count);
		// Still in the Select tool: Esc only cancelled the move.
		Assert.True(w.Tools.SelectMode);
	}

	[AvaloniaFact]
	public void DeleteKeyRemovesTheSelection()
	{
		var (w, s) = Window();
		w.View.Select(new[] { 0, 1 });
		w.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.Delete, null);
		Assert.True(s.Scene.Things[0].Gone && s.Scene.Things[1].Gone);
		Assert.Empty(w.View.Selected);
		Assert.Equal(2, s.Pending.Deleted);
	}

	[AvaloniaFact]
	public void ExactPlaceMovesThereOrBy()
	{
		var (w, s) = Window();
		w.View.Select(new[] { 1 });
		Avalonia.Threading.Dispatcher.UIThread.RunJobs();
		var p = w.SelectPanel;
		Assert.Equal(40, (double)p.XBox.Value!, 2);
		Assert.Equal(90, (double)p.TurnBox.Value!, 2);
		p.XBox.Value = 50;
		p.ZBox.Value = 44;
		p.TurnBox.Value = 0;
		p.ApplyButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
		var moved = Last(s);
		Assert.Equal(new Vector3(50, 30, 44), moved.Position);
		Assert.Equal(0, moved.Rotation.Y, 3);
		// By: from where it is now.
		p.ByButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
		Assert.Equal(0, (double)p.XBox.Value!);
		p.YBox.Value = 2;
		p.ApplyButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
		Assert.Equal(new Vector3(50, 32, 44), Last(s).Position);
	}

	[AvaloniaFact]
	public void SameKindAndInvert()
	{
		var (w, s) = Window();
		// Headless, nothing is drawn, so nothing counts as shown: both select nothing more.
		w.View.Select(new[] { 0 });
		w.View.SelectTool.Invert();
		Assert.Empty(w.View.Selected);
	}

	[Fact]
	public void TheZoneSelectsWhatIsInsideIt()
	{
		var square = new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10), new Vector2(0, 10) };
		Assert.True(SelectTool.Inside(square, 5, 5));
		Assert.False(SelectTool.Inside(square, 15, 5));
		Assert.False(SelectTool.Inside(square, -1, 9));
	}

	[Fact]
	public void AMovedObjectIsSavedAtItsNewPlace()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			var scene = WorldScene.Load(dir, 0, 0, 1);
			var s = scene.Session!;
			int i = scene.Things.FindIndex(t => !t.Piece);
			Assert.True(i >= 0, "the test world has objects in zone 0, 0");
			var t = scene.Things[i];
			var to = t.Position + new Vector3(3, 0.5f, -2);
			s.Move(new[] { (i, to, t.Rotation) });
			var res = s.Save();
			Assert.True(res.Saved, res.Message);
			Assert.Equal(1, res.ObjectsAdded);
			Assert.Equal(1, res.ObjectsDeleted);
			var again = WorldScene.Load(dir, 0, 0, 1);
			Assert.Contains(again.Things, o => o.Prefab == t.Prefab && Vector3.Distance(o.Position, to) < 0.01f);
			Assert.DoesNotContain(again.Things, o => o.Prefab == t.Prefab && Vector3.Distance(o.Position, t.Position) < 0.01f);
			// The scene was read again after saving: same objects, new ids.
			Assert.Equal(again.Things.Count, scene.Things.Count);
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
		}
	}
}

// The move arrows and the turning ring.
public class GizmoTests
{
	[Fact]
	public void TheClosestPointsOfTwoLines()
	{
		// A ray going down through (5, 10, 0), the x axis through 0.
		var (t, s, d) = Gizmo.Closest(new Vector3(5, 10, 0), -Vector3.UnitY, Vector3.Zero, Vector3.UnitX);
		Assert.Equal(10, t, 4);
		Assert.Equal(5, s, 4);
		Assert.Equal(0, d, 4);
		Assert.Equal(5, Gizmo.Along(new Vector3(5, 10, 0), -Vector3.UnitY, Vector3.Zero, Vector3.UnitX)!.Value, 4);
		// Looking straight down an axis: no answer.
		Assert.Null(Gizmo.Along(new Vector3(0, 10, 0), -Vector3.UnitY, Vector3.Zero, Vector3.UnitY));
	}

	[Fact]
	public void RaysHitTheArrowsAndTheRing()
	{
		var down = -Vector3.UnitY;
		Assert.Equal(Gizmo.Handle.X, Gizmo.Hit(new Vector3(0.7f, 10, 0), down, Vector3.Zero, 1));
		Assert.Equal(Gizmo.Handle.Z, Gizmo.Hit(new Vector3(0, 10, -0.7f), down, Vector3.Zero, 1));
		Assert.Equal(Gizmo.Handle.Ring, Gizmo.Hit(new Vector3(1.45f, 10, 0.05f), down, Vector3.Zero, 1));
		// The middle (the object itself) is not a handle; scaled up, the arrows are further out.
		Assert.Null(Gizmo.Hit(new Vector3(0.05f, 10, 0.05f), down, Vector3.Zero, 1));
		Assert.Equal(Gizmo.Handle.X, Gizmo.Hit(new Vector3(7, 10, 0), down, Vector3.Zero, 10));
		Assert.Equal(Gizmo.Handle.Y, Gizmo.Hit(new Vector3(-10, 0.7f, 0), Vector3.UnitX, Vector3.Zero, 1));
	}

	[Fact]
	public void HeadingsAreClockwiseFromNorth()
	{
		// View space: north is -z, east +x.
		Assert.Equal(0, Gizmo.Heading(new Vector3(0, 10, -1), -Vector3.UnitY, Vector3.Zero)!.Value, 3);
		Assert.Equal(90, Gizmo.Heading(new Vector3(1, 10, 0), -Vector3.UnitY, Vector3.Zero)!.Value, 3);
	}

	private static (MainWindow W, EditSession S, Func<Vector3, Avalonia.Point> Screen) Open()
	{
		var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2, new WorldScene.Thing(11, StableHash.Of("wood_wall_half"), new Vector3(40, 30, 40), Vector3.Zero, 0, true));
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.Tools.ChooseSelect();
		w.View.Select(new[] { 0 });
		// Looking down at the wall from above (a little to the south, so north is up on screen).
		var c = new Vector3(40 - s.Scene.Cx, 30, -(40 - s.Scene.Cz));
		w.View.SetCamera(c + new Vector3(0, 30, 3), c, 1);
		var size = w.View.Bounds.Size;
		Avalonia.Point Screen(Vector3 p)
		{
			var q = Vector4.Transform(new Vector4(p, 1), w.View.ViewProj);
			return new Avalonia.Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
		}
		return (w, s, Screen);
	}

	[AvaloniaFact]
	public void DraggingTheEastArrowMovesTheSelectionEast()
	{
		var (w, s, screen) = Open();
		var (c, scale) = w.View.GizmoAt()!.Value;
		var from = screen(c + new Vector3(0.7f * scale, 0, 0));
		var to = screen(c + new Vector3(0.7f * scale + 4, 0, 0));
		w.MouseMove(from);
		Assert.Equal(Gizmo.Handle.X, w.View.SelectTool.HotHandle);
		w.MouseDown(from, MouseButton.Left);
		w.MouseMove(to);
		w.MouseUp(to, MouseButton.Left);
		var moved = s.Scene.Things[^1];
		Assert.True(s.Scene.Things[0].Gone);
		Assert.Equal(44, moved.Position.X, 1);
		Assert.Equal(40, moved.Position.Z, 1);
	}

	[AvaloniaFact]
	public void DraggingTheRingTurnsTheSelection()
	{
		var (w, s, screen) = Open();
		var (c, scale) = w.View.GizmoAt()!.Value;
		float r = Gizmo.RingRadius * scale;
		// From the north of the ring to its east: a quarter turn clockwise.
		var from = screen(c + new Vector3(0, 0, -r));
		var to = screen(c + new Vector3(r, 0, 0));
		w.MouseDown(from, MouseButton.Left, RawInputModifiers.None);
		w.MouseMove(to, RawInputModifiers.Control);
		w.MouseUp(to, MouseButton.Left);
		Assert.Equal(90, s.Scene.Things[^1].Rotation.Y, 1);
		Assert.Equal(new Vector2(40, 40), new Vector2(s.Scene.Things[^1].Position.X, s.Scene.Things[^1].Position.Z));
	}
}
