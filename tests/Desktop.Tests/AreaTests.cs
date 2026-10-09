using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Area tool: a box or polygon selection, and the actions done inside it.
public class AreaTests
{
	private static AreaTool Box(float x0, float z0, float x1, float z1, float soft = 0)
	{
		var a = new AreaTool { Soft = soft };
		a.Points.Add(new Vector2(x0, z0));
		a.Points.Add(new Vector2(x1, z1));
		return a;
	}

	private static float H(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

	[Fact]
	public void ABoxWithoutASoftEdgeHasStraightEdges()
	{
		var a = Box(10, 10, 20, 20);
		var w = a.WeightsIn(129, 129)!;
		var at = w.Cells.ToDictionary(c => c.G, c => c.W);
		Assert.Equal(1, at[15 * 129 + 15], 3);
		// On the outline: half.
		Assert.Equal(0.5f, at[15 * 129 + 10], 3);
		Assert.False(at.ContainsKey(15 * 129 + 9));
		Assert.Equal(100, AreaTool.Area(a.Polygon()!), 3);
	}

	[Fact]
	public void TheSoftEdgeFadesInwards()
	{
		var a = Box(10, 10, 30, 30, soft: 4);
		var at = a.WeightsIn(129, 129)!.Cells.ToDictionary(c => c.G, c => c.W);
		Assert.Equal(1, at[20 * 129 + 20], 3);
		Assert.True(at[20 * 129 + 12] is > 0 and < 1);
		Assert.False(at.ContainsKey(20 * 129 + 10));
	}

	[Fact]
	public void APolygonClosesOnADoubleClick()
	{
		var a = new AreaTool { Box = false };
		a.Down(new Vector2(0, 0), 1);
		a.Down(new Vector2(10, 0), 1);
		Assert.Null(a.Polygon());
		a.Down(new Vector2(10, 10), 1);
		Assert.NotNull(a.Polygon());
		Assert.False(a.Closed);
		a.Down(new Vector2(10, 10), 2);
		Assert.True(a.Closed);
		Assert.Equal(3, a.Points.Count);
		// A click after closing starts a new one.
		a.Down(new Vector2(50, 50), 1);
		Assert.Single(a.Points);
	}

	[Fact]
	public void GroundActionsChangeOnlyTheInside()
	{
		var s = EditTests.Flat(2);
		var a = Box(40, 40, 80, 80, soft: 2);
		Assert.Contains("dig 0 m³, fill", a.Volume(s.Ground, AreaTool.GroundAction.Flatten, 33, 2));
		s.EditGround("Area: Flatten", g => a.Apply(g, s.Brush, AreaTool.GroundAction.Flatten, 33, 0, new float[4]));
		Assert.Equal(33, H(s, 60, 60), 3);
		Assert.Equal(30, H(s, 90, 60), 3);
		Assert.Equal(33, a.Average(s.Ground)!.Value, 0);
		Assert.Contains("raised", a.Volume(s.Ground, AreaTool.GroundAction.Raise, 0, 2));
		s.EditGround("Area: Restore", g => a.Apply(g, s.Brush, AreaTool.GroundAction.Restore, 0, 0, new float[4]));
		Assert.Equal(30, H(s, 60, 60), 3);
		Assert.Equal(new[] { (0, 0), (1, 0), (0, 1), (1, 1) }.OrderBy(z => z), a.ZonesUnder(s.Ground).OrderBy(z => z));
	}

	private static readonly int Beech = StableHash.Of("Beech1"), Rock = StableHash.Of("rock4_coast"), Wall = StableHash.Of("wood_wall_half");

	private static (MainWindow W, EditSession S) Open()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		// World x, z = grid + (-32): grid (60, 60) is world (28, 28).
		var s = EditTests.Flat(2,
			new WorldScene.Thing(1, Beech, new Vector3(28, 30, 28), Vector3.Zero, 1, false),
			new WorldScene.Thing(2, Beech, new Vector3(30, 30, 28), Vector3.Zero, 1, false),
			new WorldScene.Thing(3, Rock, new Vector3(28, 30, 30), Vector3.Zero, 1, false),
			new WorldScene.Thing(4, Wall, new Vector3(29, 30, 29), Vector3.Zero, 0, true),
			new WorldScene.Thing(5, Beech, new Vector3(80, 30, 80), Vector3.Zero, 1, false));
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.KeyPress(Key.B, RawInputModifiers.None, PhysicalKey.B, "b");
		var a = w.View.Area;
		a.Points.Add(new Vector2(50, 50));
		a.Points.Add(new Vector2(70, 70));
		a.Notify();
		return (w, s);
	}

	[AvaloniaFact]
	public void TheInfoCountsWhatIsInside()
	{
		var (w, _) = Open();
		Assert.Equal(ToolMode.Area, w.View.Mode);
		Assert.True(w.AreaPanel.Card.IsVisible);
		Assert.StartsWith("400 m² selected · 4 shown object(s) inside.", w.AreaPanel.Info.Text);
		Assert.Equal("Trees & logs 2", w.AreaPanel.KindButtons[ObjectKind.Trees].Content);
		// Hidden kinds are not counted (ruins are off at first).
		Assert.Equal("Ruins & structures hidden", w.AreaPanel.KindButtons[ObjectKind.Ruins].Content);
	}

	[AvaloniaFact]
	public async Task RemoveTakesTheChosenKindsOnly()
	{
		var (w, s) = Open();
		w.AreaPanel.Choose(AreaPanel.Act.Remove);
		Assert.Equal(AreaPanel.Act.Remove, w.AreaPanel.Current);
		w.AreaPanel.KindButtons[ObjectKind.Rocks].IsChecked = false;
		await w.AreaPanel.Apply();
		Assert.True(s.Scene.Things[0].Gone && s.Scene.Things[1].Gone);
		Assert.False(s.Scene.Things[2].Gone);
		Assert.False(s.Scene.Things[3].Gone);
		Assert.False(s.Scene.Things[4].Gone);
		Assert.Equal(2, s.Pending.Deleted);
		s.Undo();
		Assert.Equal(0, s.Pending.Deleted);
	}

	[AvaloniaFact]
	public async Task ReplacePutsAnotherKindInTheSamePlaces()
	{
		var (w, s) = Open();
		var p = w.AreaPanel;
		p.Choose(AreaPanel.Act.Replace);
		Assert.StartsWith("Beech1 (2)", (string)p.FromBox.SelectedItem!);
		// Headless there is no world file, so nothing to choose from: replace directly.
		p.Replace(s, new[] { 0, 1 }, Rock);
		Assert.True(s.Scene.Things[0].Gone);
		var added = s.Scene.Things.Skip(5).ToList();
		Assert.Equal(2, added.Count);
		Assert.All(added, t => Assert.Equal(Rock, t.Prefab));
		Assert.Equal(new Vector3(28, 30, 28), added[0].Position);
		Assert.Equal((0, 2, 2, 0), s.Pending);
		s.Undo();
		Assert.Equal((0, 0, 0, 0), s.Pending);
	}

	[AvaloniaFact]
	public async Task ResetZonesMarksTheZonesAndUndoUnmarksThem()
	{
		var (w, s) = Open();
		var p = w.AreaPanel;
		string? asked = null;
		p.Confirm = q => { asked = q; return Task.FromResult(true); };
		s.Shape(60, 60, Formula.Compile("1", new string[0]), 3, 0, "x");
		p.Choose(AreaPanel.Act.Reset);
		await p.Apply();
		Assert.Contains("Reset 4 zone(s)", asked);
		Assert.Equal(4, s.Pending.Resets);
		// Its ground edits are gone too.
		Assert.Equal(30, H(s, 60, 60), 3);
		s.Undo();
		Assert.Equal(0, s.Pending.Resets);
		Assert.Equal(31, H(s, 60, 60), 3);
		s.Redo();
		p.UnresetButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
		Assert.Equal(0, s.Pending.Resets);
	}

	[AvaloniaFact]
	public async Task SelectObjectsSwitchesToTheSelectTool()
	{
		var (w, _) = Open();
		w.AreaPanel.Choose(AreaPanel.Act.Select);
		await w.AreaPanel.Apply();
		Assert.Equal(ToolMode.Select, w.View.Mode);
		Assert.Equal(new[] { 0, 1, 2 }, w.View.Selected.Order());
	}

	[AvaloniaFact]
	public void DraggingABoxThenEnterFlattensIt()
	{
		var (w, s) = Open();
		w.View.Area.Clear();
		w.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1.6f);
		Avalonia.Point Screen(float gx, float gz)
		{
			var q = Vector4.Transform(new Vector4(gx - 64, 30, -(gz - 64), 1), w.View.ViewProj);
			return new Avalonia.Point((q.X / q.W + 1) / 2 * 1600, (1 - q.Y / q.W) / 2 * 1000);
		}
		w.MouseDown(Screen(50, 50), MouseButton.Left);
		w.MouseMove(Screen(78, 70));
		w.MouseUp(Screen(78, 70), MouseButton.Left);
		var poly = w.View.Area.Polygon()!;
		Assert.Equal(28 * 20, AreaTool.Area(poly), 0);
		w.AreaPanel.HeightBox.Value = 32;
		w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
		Assert.Equal(32, H(s, 64, 60), 2);
		Assert.Equal("Area: Flatten", s.UndoLabel);
		w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
		Assert.Null(w.View.Area.Polygon());
	}
}

// Regrow and Restore from a backup, on a copy of the test world.
[Collection("World files")]
public class AreaWorldTests
{
	private static (MainWindow W, EditSession S, string Dir) Open()
	{
		string dir = EditTests.CopyFixture();
		var scene = WorldScene.Load(dir, 0, 0, 1);
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		w.View.Show(scene, null);
		w.Edit(scene.Session!);
		w.Tools.ChooseMode(ToolMode.Area);
		// The whole zone (its outer line stays: it is locked).
		w.View.Area.Points.Add(new Vector2(2, 2));
		w.View.Area.Points.Add(new Vector2(62, 62));
		w.View.Area.Notify();
		return (w, scene.Session!, dir);
	}

	private static void Done(string dir) => Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);

	// Saving offline is like Apply live: the area and its history stay, the steps saved; an undo is
	// then pending, and saved by the next save. The save the world was opened from stays until the world
	// is left, then only the latest. No backup folder.
	[AvaloniaFact]
	public void SavingKeepsTheHistoryAndAnUndoIsSavedToo()
	{
		var (w, s, dir) = Open();
		try
		{
			float H(int x, int z) => s.Scene.Heights[z * s.Scene.W + x];
			float was = H(32, 32);
			int tree = s.Scene.Things.FindIndex(t => !t.Piece);
			s.Shape(32, 32, Formula.Compile("3", new string[0]), 4, 0, "x");
			s.Delete(new[] { tree });
			Assert.True(s.Save().Saved);
			Assert.Equal((0, 0, 0, 0), s.Pending);
			Assert.True(s.CanUndo);
			Assert.Equal(0, s.UnappliedSteps);
			Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(dir)!, "*_backup_*"));
			// The tree back (undo): pending, then saved.
			s.Undo();
			Assert.Equal((0, 1, 0, 0), s.Pending);
			Assert.True(s.Save().Saved);
			Assert.Equal((0, 0, 0, 0), s.Pending);
			var saved = WorldSave.Load(dir);
			Assert.Contains(saved.Objects, o => o.Prefab == s.Scene.Things[tree].Prefab && Vector3.Distance(o.Position, s.Scene.Things[tree].Position) < 0.01f);
			// The raise back too.
			s.Undo();
			Assert.Equal(1, s.Pending.Zones);
			Assert.True(s.Save().Saved);
			Assert.Equal(was, H(32, 32), 2);
			// Two saves in the folder (the one it was opened from, the latest); one once left.
			Assert.Equal(2, Directory.GetFiles(dir, "_main.*.ok").Length);
			s.Scene.Owner!.Dispose();
			Assert.Single(Directory.GetFiles(dir, "_main.*.ok"));
		}
		finally
		{
			Done(dir);
		}
	}

	[AvaloniaFact]
	public async Task RegrowPutsBackWhatTheGameGrows()
	{
		var (w, s, dir) = Open();
		try
		{
			var p = w.AreaPanel;
			foreach (var k in p.KindButtons.Keys)
			{
				p.KindButtons[k].IsChecked = k == ObjectKind.Trees;
			}
			p.Choose(AreaPanel.Act.Remove);
			await p.Apply();
			int removed = s.Pending.Deleted;
			Assert.True(removed > 0, "the test world has trees in zone 0, 0");
			p.Choose(AreaPanel.Act.Regrow);
			await p.Apply();
			Assert.True(s.Pending.Added > 0, w.MessageText.Text);
			Assert.StartsWith("Regrew", w.MessageText.Text);
			// Only trees, and only inside.
			var added = s.Scene.Things.Where(t => t.Id < 0 && !t.Gone).ToList();
			Assert.All(added, t => Assert.Equal(ObjectKind.Trees, ObjectKinds.Of(TerrainEditor.Terrain.PrefabCatalog.DisplayName(t.Prefab), false)));
			Assert.All(added, t => Assert.InRange(t.Position.X, -30, 30));
			// Again: everything is standing now, so nothing is doubled.
			int before = s.Pending.Added;
			await p.Apply();
			Assert.Equal(before, s.Pending.Added);
		}
		finally
		{
			Done(dir);
		}
	}

	[AvaloniaFact]
	public async Task RestoreFromABackup()
	{
		var (w, s, dir) = Open();
		try
		{
			var p = w.AreaPanel;
			float H(int x, int z) => s.Scene.Heights[z * s.Scene.W + x];
			float was = H(32, 32);
			int tree = s.Scene.Things.FindIndex(t => !t.Piece);
			var gone = s.Scene.Things[tree];
			// A backup of how it was (the game's), then the world changed and saved.
			WorldEditor.Tests.TempWorld.CopyDir(dir, dir.TrimEnd(Path.DirectorySeparatorChar) + "_backup_auto-20261001100000");
			s.Shape(32, 32, Formula.Compile("3", new string[0]), 4, 0, "x");
			s.Delete(new[] { tree });
			Assert.True(s.Save().Saved);
			var backup = Assert.Single(TerrainEditor.App.Backups.Find(dir));
			Assert.Equal("game", backup.Kind);
			Assert.Equal(was + 3, H(32, 32), 2);

			foreach (var k in p.KindButtons.Keys)
			{
				p.KindButtons[k].IsChecked = true;
			}
			p.Choose(AreaPanel.Act.Backup);
			await p.RestoreBackup(s, w.View.Area.Polygon()!, backup.Path);
			Assert.Equal(was, H(32, 32), 2);
			Assert.Contains(s.Scene.Things, t => !t.Gone && t.Prefab == gone.Prefab && Vector3.Distance(t.Position, gone.Position) < 0.01f);
			Assert.Equal(1, s.Pending.Added);
			Assert.Equal(0, s.Pending.Deleted);
			Assert.StartsWith("Restored from the backup: the ground, 1 object(s) brought back, 0 removed.", w.MessageText.Text);
			// A second restore finds nothing to do.
			await p.RestoreBackup(s, w.View.Area.Polygon()!, backup.Path);
			Assert.StartsWith("Nothing to restore", w.MessageText.Text);
		}
		finally
		{
			Done(dir);
		}
	}
}
