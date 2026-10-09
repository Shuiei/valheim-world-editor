using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Select tool with the mouse, looking straight down on a flat area: clicking and drawing zones,
// dragging, double-clicking a building, Same kind, Drop and snapping. The view's object boxes come
// from the pieces' own boxes (headless tests draw nothing, so the view is given them).
[Collection("World files")]
public class SelectMouseTests
{
	private static readonly int Wall = StableHash.Of("wood_wall_half"), Floor = StableHash.Of("wood_floor"), Beech = StableHash.Of("Beech1");

	private sealed record View(MainWindow W, EditSession S, Avalonia.Size Size)
	{
		public SelectTool Tool => W.View.SelectTool;

		// Where a world point is on screen.
		public Avalonia.Point At(float x, float y, float z)
		{
			var q = Vector4.Transform(new Vector4(x - S.Scene.Cx, y, -(z - S.Scene.Cz), 1), W.View.ViewProj);
			return new Avalonia.Point((q.X / q.W + 1) / 2 * Size.Width, (1 - q.Y / q.W) / 2 * Size.Height);
		}

		public void Click(float x, float z, bool shift = false, int clicks = 1)
		{
			var p = At(x, 30, z);
			Tool.Down(p, Size, shift, false, clicks);
			Tool.Up(p, Size, shift);
		}

		public void Drag(bool shift, params (float X, float Z)[] path)
		{
			Tool.Down(At(path[0].X, 30, path[0].Z), Size, shift, false, 1);
			foreach (var (x, z) in path.Skip(1))
			{
				Tool.Moved(At(x, 30, z), Size);
			}
			Tool.Up(At(path[^1].X, 30, path[^1].Z), Size, shift);
		}

		public HashSet<int> Selected => W.View.Selected.ToHashSet();

		public WorldScene.Thing Last => S.Scene.Things[^1];
	}

	// A box around a thing in view space: a piece's own box, or 1 × 5 × 1 m for a tree.
	private static (int, Vector3, Vector3) Box(WorldScene s, int i)
	{
		var t = s.Things[i];
		var info = PieceCatalog.Get(t.Prefab);
		var (x0, x1, z0, z1, y0, y1) = info != null ? (info.MinX, info.MaxX, info.MinZ, info.MaxZ, info.MinY, info.MaxY) : (-0.5f, 0.5f, -0.5f, 0.5f, 0f, 5f);
		var p = t.Position;
		return (i, new Vector3(p.X + x0 - s.Cx, p.Y + y0, -(p.Z + z1 - s.Cz)), new Vector3(p.X + x1 - s.Cx, p.Y + y1, -(p.Z + z0 - s.Cz)));
	}

	private static View Open(params WorldScene.Thing[] things)
	{
		var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2, things);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.Tools.ChooseSelect();
		w.View.SetBoxes(Enumerable.Range(0, things.Length).Select(i => Box(s.Scene, i)));
		// Straight down on the middle of the area (a little south, so north is up), 40 m around.
		var c = new Vector3(40 - s.Scene.Cx, 30, -(40 - s.Scene.Cz));
		w.View.SetCamera(c + new Vector3(0, 40, 0.01f), c, 1);
		return new View(w, s, w.View.Bounds.Size);
	}

	private static WorldScene.Thing T(int id, int prefab, float x, float y, float z, bool piece = true) => new(id, prefab, new Vector3(x, y, z), Vector3.Zero, piece ? 0 : 1, piece);

	// Shift + click: the row from the last object clicked (w1, Shift + click w3: w1, w2, w3), not what
	// stands off it; Ctrl + click adds one.
	[AvaloniaFact]
	public void ShiftClickTakesTheRowAndCtrlClickAddsOne()
	{
		var v = Open(T(1, Wall, 36, 30, 40), T(2, Wall, 38, 30, 40), T(3, Wall, 40, 30, 40), T(4, Wall, 38, 30, 33), T(5, Wall, 46, 30, 46));
		// From the east end (the move arrows point east and north of the first one, not over the others).
		v.Click(40, 40);
		Assert.Equal(new HashSet<int> { 2 }, v.Selected);
		v.Tool.Range = true;
		v.Click(36, 40, shift: true);
		v.Tool.Range = false;
		Assert.Equal(new HashSet<int> { 0, 1, 2 }, v.Selected);
		// Ctrl (add, no row): one more.
		v.Click(38, 33, shift: true);
		Assert.Equal(new HashSet<int> { 0, 1, 2, 3 }, v.Selected);
		// A plain click: that one alone.
		v.Click(46, 46);
		Assert.Equal(new HashSet<int> { 4 }, v.Selected);
	}

	[AvaloniaFact]
	public void AClickPicksAndAClickOnEmptyGroundDeselects()
	{
		var v = Open(T(1, Wall, 40, 30, 40), T(2, Beech, 50, 30, 50, false));
		v.Click(40, 40);
		Assert.Equal(new HashSet<int> { 0 }, v.Selected);
		v.Click(50, 50, shift: true);
		Assert.Equal(new HashSet<int> { 0, 1 }, v.Selected);
		// Shift + click on a selected one takes it out.
		v.Click(50, 50, shift: true);
		Assert.Equal(new HashSet<int> { 0 }, v.Selected);
		v.Click(30, 30);
		Assert.Empty(v.Selected);
	}

	[AvaloniaFact]
	public void AZoneDrawnOnTheGroundSelectsWhatIsInside()
	{
		var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 42, 30, 40), T(3, Wall, 55, 30, 55));
		v.Drag(false, (36, 36), (46, 36), (46, 44), (36, 44), (36, 37));
		Assert.Equal(new HashSet<int> { 0, 1 }, v.Selected);
		Assert.Null(v.Tool.Lasso);
		// Shift: added to what is selected.
		v.Drag(true, (52, 52), (58, 52), (58, 58), (52, 58));
		Assert.Equal(new HashSet<int> { 0, 1, 2 }, v.Selected);
		// A zone with nothing in it, with Shift, keeps the selection; a plain click on the ground clears it.
		v.Drag(true, (20, 20), (24, 20), (24, 24), (20, 24));
		Assert.Equal(3, v.Selected.Count);
		v.Click(25, 25, shift: true);
		Assert.Equal(3, v.Selected.Count);
	}

	[AvaloniaFact]
	public void EscapeCancelsAZoneBeingDrawn()
	{
		var v = Open(T(1, Wall, 40, 30, 40));
		v.Tool.Down(v.At(36, 30, 36), v.Size, false, false, 1);
		v.Tool.Moved(v.At(46, 30, 36), v.Size);
		v.Tool.Moved(v.At(46, 30, 44), v.Size);
		Assert.NotNull(v.Tool.Lasso);
		Assert.True(v.Tool.Key(Avalonia.Input.Key.Escape, false, false));
		Assert.Null(v.Tool.Lasso);
	}

	[AvaloniaFact]
	public void DraggingASelectedObjectMovesIt()
	{
		var v = Open(T(1, Beech, 40, 30, 40, false));
		v.Click(40, 40);
		v.Drag(false, (40, 40), (42, 40), (44, 41));
		Assert.True(v.S.Scene.Things[0].Gone);
		Assert.Equal(44, v.Last.Position.X, 1);
		Assert.Equal(41, v.Last.Position.Z, 1);
		// The copy is selected, and one undo puts the tree back.
		Assert.Equal(new HashSet<int> { v.S.Scene.Things.Count - 1 }, v.Selected);
		v.S.Undo();
		Assert.False(v.S.Scene.Things[0].Gone);
	}

	[AvaloniaFact]
	public void ADraggedWallSnapsToTheWallNextToIt()
	{
		var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 50, 30, 40));
		v.Click(50, 40);
		// Its west end brought 0.4 m from the other wall's east end: it closes the gap.
		v.Drag(false, (50, 40), (46, 40), (42.4f, 40.2f));
		var moved = v.Last;
		Assert.Equal(42, moved.Position.X, 2);
		Assert.Equal(40, moved.Position.Z, 2);
		Assert.Equal(30, moved.Position.Y, 2);
		// With snapping off, it stays where it was dropped.
		v.S.Undo();
		v.Tool.SnapToPieces = false;
		v.Click(50, 40);
		v.Drag(false, (50, 40), (46, 40), (42.4f, 40.2f));
		Assert.Equal(42.4f, v.Last.Position.X, 1);
	}

	[AvaloniaFact]
	public void ADoubleClickSelectsTheWholeBuilding()
	{
		// Two walls end to end and a floor against them: one building; a wall further off is another.
		var v = Open(T(1, Wall, 40, 30, 40), T(2, Wall, 42, 30, 40), T(3, Floor, 41, 29.5f, 41.2f), T(4, Wall, 55, 30, 55), T(5, Beech, 43.5f, 30, 40, false));
		v.Click(40, 40, clicks: 2);
		Assert.Equal(new HashSet<int> { 0, 1, 2 }, v.Selected);
		// From the button too, adding to what is selected.
		v.Click(55, 55);
		v.Tool.WholeBuilding();
		Assert.Equal(new HashSet<int> { 3 }, v.Selected);
		// A tree is not a building.
		string? said = null;
		v.Tool.Message += (t, _) => said = t;
		v.W.View.Select(new[] { 4 });
		v.Tool.WholeBuilding();
		Assert.StartsWith("Select a building piece first", said);
	}

	[AvaloniaFact]
	public void SameKindSelectsEveryShownObjectOfTheSelectedKinds()
	{
		var v = Open(T(1, Beech, 40, 30, 40, false), T(2, Beech, 50, 30, 50, false), T(3, Wall, 45, 30, 45));
		string? said = null;
		v.Tool.Message += (t, _) => said = t;
		v.Tool.SameKind();
		Assert.StartsWith("Select an object of each kind", said);
		v.W.View.Select(new[] { 0 });
		v.Tool.SameKind();
		Assert.Equal(new HashSet<int> { 0, 1 }, v.Selected);
		// Hidden kinds are left out.
		v.W.View.SetShown(ObjectKinds.Of("Beech1", false), false);
		v.W.View.Select(new[] { 0, 2 });
		v.Tool.SameKind();
		Assert.Equal(new HashSet<int> { 2 }, v.Selected);
	}

	[AvaloniaFact]
	public void DropPutsObjectsOnTheGroundOrOnWhatIsUnderThem()
	{
		// A tree floating 6 m up, and another above a floor.
		var v = Open(T(1, Beech, 40, 36, 40, false), T(2, Floor, 50, 31, 50), T(3, Beech, 50, 40, 50, false));
		string? said = null;
		v.Tool.Message += (t, _) => said = t;
		v.W.View.Select(new[] { 0, 2 });
		Assert.True(v.Tool.Key(Avalonia.Input.Key.End, false, false));
		Assert.Equal("Dropped 2 object(s): 1 onto other objects, 1 onto the ground.", said);
		v.Tool.Commit();
		var trees = v.S.Scene.Things.Where(t => !t.Gone && t.Prefab == Beech).OrderBy(t => t.Position.X).ToList();
		Assert.Equal(30, trees[0].Position.Y, 2);
		Assert.Equal(31 + PieceCatalog.Get(Floor)!.MaxY, trees[1].Position.Y, 2);
	}

	[AvaloniaFact]
	public void KeysLiftAndTurnTheSelection()
	{
		var v = Open(T(1, Beech, 40, 30, 40, false));
		v.W.View.Select(new[] { 0 });
		v.Tool.OnGround = false;
		Assert.True(v.Tool.Key(Avalonia.Input.Key.PageUp, false, false));
		Assert.True(v.Tool.Key(Avalonia.Input.Key.PageUp, true, false));
		Assert.True(v.Tool.Key(Avalonia.Input.Key.PageDown, false, false));
		Assert.True(v.Tool.Key(Avalonia.Input.Key.OemPeriod, false, false));
		Assert.True(v.Tool.Key(Avalonia.Input.Key.OemComma, true, false));
		Assert.False(v.Tool.Key(Avalonia.Input.Key.Q, false, false));
		v.Tool.Commit();
		// +0.25 + 1 - 0.25 = +1 m; +1° then -15°.
		Assert.Equal(31, v.Last.Position.Y, 2);
		Assert.Equal(346, (v.Last.Rotation.Y % 360 + 360) % 360, 1);
	}

	[AvaloniaFact]
	public void DeleteWithNothingSelectedSaysSo()
	{
		var v = Open(T(1, Beech, 40, 30, 40, false));
		string? said = null;
		v.Tool.Message += (t, _) => said = t;
		v.Tool.Delete();
		Assert.Equal("Select objects to delete first.", said);
		Assert.False(v.S.CanUndo);
	}
}
