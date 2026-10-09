using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Copy and paste: an Area selection's ground and objects, or the Select tool's objects, pasted
// elsewhere, turned, mirrored and repeated.
public class PasteTests
{
	private static readonly int Beech = StableHash.Of("Beech1");

	private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

	// A 2 × 2 zone area with a 3 m mound at grid (40, 40) and a tree on it (world = grid - 32).
	private static EditSession Area()
	{
		var s = EditTests.Flat(2, new WorldScene.Thing(1, Beech, new Vector3(8, 33, 8), new Vector3(0, 30, 0), 1, false));
		s.Shape(40, 40, Formula.Compile("h * smooth(1 - d / r)", new[] { "d", "r", "h" }), 6, 3, "mound");
		return s;
	}

	private static AreaTool Box(float x0, float z0, float x1, float z1)
	{
		var a = new AreaTool { Soft = 0 };
		a.Points.Add(new Vector2(x0, z0));
		a.Points.Add(new Vector2(x1, z1));
		return a;
	}

	[Fact]
	public void CopyKeepsTheGroundsShapeAndTheObjects()
	{
		var s = Area();
		var c = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, new[] { 0 }, _ => "Beech1")!;
		Assert.Equal(19, c.W);
		var o = Assert.Single(c.Objects);
		Assert.Equal(new Vector2(0, 0), new Vector2(o.Dx, o.Dz));
		Assert.Equal(1, o.SourceId);
		// The middle is 3 m higher than the edges.
		int mid = 9 * c.W + 9, edge = 9 * c.W + 1;
		Assert.Equal(3, c.Rel[mid] - c.Rel[edge], 2);
		Assert.True(float.IsNaN(c.Rel[0]));
	}

	[Fact]
	public void PastingPutsTheMoundAndTheTreeElsewhereInOneStep()
	{
		var s = Area();
		var paste = new PasteTool { Clip = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, new[] { 0 }, _ => "Beech1") };
		var add = new List<(TerrainEditor.Editing.NewObject, bool)>();
		var added = s.Commit("Paste", g => { var (t, r, a) = paste.Apply(g, new Vector2(90, 90)); add.AddRange(a); return (t, r); }, Array.Empty<int>(), add);
		// The shape is kept inside (the box's edge points get half: no soft edge).
		Assert.Equal(H(s, 40, 40) - H(s, 36, 40), H(s, 90, 90) - H(s, 86, 90), 2);
		var tree = s.Scene.Things[Assert.Single(added)];
		Assert.Equal(new Vector2(90 - 32, 90 - 32), new Vector2(tree.Position.X, tree.Position.Z));
		// It stood on the mound's top; it still does (the paste is relative to the ground clicked).
		Assert.Equal(H(s, 90, 90), tree.Position.Y, 2);
		Assert.Equal(1, s.Edits.Added[0].SourceId);
		Assert.Equal("Paste", s.UndoLabel);
		s.Undo();
		Assert.Equal(30, H(s, 90, 90), 3);
		Assert.True(s.Scene.Things[added[0]].Gone);
	}

	[Fact]
	public void TurningAndMirroringMoveTheObjectsAroundTheMiddle()
	{
		var p = new PasteTool();
		p.TurnBy(90);
		Assert.Equal(new Vector2(0, 1), Round(p.Xf(new Vector2(1, 0))));
		p.Mirror = true;
		Assert.Equal(new Vector2(0, -1), Round(p.Xf(new Vector2(1, 0))));
		Assert.Equal(new Vector2(1, 0), Round(p.Inv(p.Xf(new Vector2(1, 0)))));
		p.TurnBy(-100);
		Assert.Equal(350, p.Turn, 3);
	}

	private static Vector2 Round(Vector2 v) => new(MathF.Round(v.X, 3) + 0f, MathF.Round(v.Y, 3) + 0f);

	[Fact]
	public void RepeatsGoAlongTheWidthOrUp()
	{
		var s = Area();
		var paste = new PasteTool { Clip = CopyData.FromArea(Box(32, 32, 48, 48), s.Ground, s.Scene, new[] { 0 }, _ => "Beech1"), Copies = 3, Gap = 2 };
		var (objs, outlines) = paste.Preview(new Vector2(40, 90), _ => 30);
		Assert.Equal(3, objs.Count);
		Assert.Equal(new[] { 40f, 58f, 76f }, objs.Select(o => MathF.Round(o.X)));
		paste.Direction = PasteTool.Along.Up;
		(objs, _) = paste.Preview(new Vector2(40, 90), _ => 30);
		Assert.Equal(objs[0].X, objs[2].X);
		Assert.True(objs[1].Y > objs[0].Y);
		Assert.Equal(3, outlines.Count);
	}

	[AvaloniaFact]
	public void CtrlCAndCtrlVInTheWindow()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = Area();
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.KeyPress(Key.B, RawInputModifiers.None, PhysicalKey.B, "b");
		w.View.Area.Points.Add(new Vector2(32, 32));
		w.View.Area.Points.Add(new Vector2(48, 48));
		w.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
		Assert.NotNull(w.View.Paste.Clip);
		Assert.StartsWith("Copied 19 × 19 m and 1 object(s)", w.MessageText.Text);
		w.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, "v");
		Assert.Equal(ToolMode.Paste, w.View.Mode);
		Assert.True(w.PastePanel.Card.IsVisible);
		w.KeyPress(Key.R, RawInputModifiers.None, PhysicalKey.R, "r");
		Assert.Equal(90, w.View.Paste.Turn);
		Assert.Contains("turned 90°", w.PastePanel.Info.Text);
		w.PasteAt(new Vector2(90, 90));
		Assert.Equal((4, 0, 1, 0), (s.Pending.Zones > 0 ? 4 : 0, s.Pending.Deleted, s.Pending.Added, s.Pending.Resets));
		w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
		Assert.Equal(ToolMode.Area, w.View.Mode);
	}

	[AvaloniaFact]
	public void TheSelectToolCopiesObjectsThatFollowTheGround()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = Area();
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.Tools.ChooseSelect();
		w.View.Select(new[] { 0 });
		w.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.C, "c");
		var o = Assert.Single(w.View.Paste.Clip!.Objects);
		Assert.True(o.Follow);
		// On a flat place it lands at the ground plus what it stood above the mound.
		w.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, "v");
		w.PasteAt(new Vector2(100, 100));
		Assert.Equal(30 + (33 - H(s, 40, 40)), s.Scene.Things[^1].Position.Y, 2);
		// Objects only: the ground is not touched.
		Assert.Equal(30, H(s, 100, 100), 3);
	}

	// Copied with the Select tool on a slope: building pieces keep the building's shape (each piece's
	// height from the others), trees and the like keep their height above the ground where they land.
	[Fact]
	public void SelectCopiesKeepABuildingsShapeOnASlope()
	{
		int wall = StableHash.Of("woodwall");
		var s = Area();
		// On the mound's side: two walls one above the other, a tree beside them.
		float ox = s.Scene.X0 * 64f - 32f, oz = s.Scene.Z0 * 64f - 32f;
		float gy = H(s, 37, 40);
		s.Scene.Things.Add(new WorldScene.Thing(2, wall, new Vector3(37 + ox, gy + 1, 40 + oz), Vector3.Zero, 0, true));
		s.Scene.Things.Add(new WorldScene.Thing(3, wall, new Vector3(37 + ox, gy + 3, 40 + oz), Vector3.Zero, 0, true));
		float GroundAt(float x, float z) => H(s, (int)MathF.Round(x - ox), (int)MathF.Round(z - oz));
		var clip = CopyData.FromThings(s.Scene, new[] { 0, 1, 2 }, i => i == StableHash.Of("Beech1") ? "Beech1" : "woodwall", GroundAt)!;
		Assert.True(clip.Objects.Single(o => o.Name == "Beech1").Follow);
		Assert.All(clip.Objects.Where(o => o.Name == "woodwall"), o => Assert.False(o.Follow));
		var paste = new PasteTool { Clip = clip, Ground = false };
		var (_, _, add) = paste.Apply(s.Ground, new Vector2(90, 90));
		var walls = add.Select(a => a.Item1).Where(o => o.Prefab == wall).OrderBy(o => o.Position.Y).ToList();
		Assert.Equal(2, walls[1].Position.Y - walls[0].Position.Y, 3);
		var tree = add.Select(a => a.Item1).Single(o => o.Prefab == Beech);
		Assert.Equal(GroundAt(tree.Position.X, tree.Position.Z), tree.Position.Y, 2);
	}
}
