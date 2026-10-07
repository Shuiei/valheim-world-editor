using System.Numerics;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Place tool: brush, line (also end to end), grid and zone placements.
public class PlaceTests
{
	private const string Wall = "woodwall";

	private static (PlaceTool T, EditSession S) Tool(params WorldScene.Thing[] things)
	{
		var s = EditTests.Flat(2, things);
		var t = new PlaceTool { Scene = () => s.Scene, Brush = s.Brush };
		t.Chosen.Clear();
		t.Chosen.Add("Beech1");
		t.Tilt = 0;
		return (t, s);
	}

	private static float Yaw(PlaceTool.Placement p) => (p.Rotation.Y % 360 + 360) % 360;

	[Fact]
	public void TheWallHasSnapPoints()
	{
		Assert.True(PlaceTool.IsPiece(Wall));
		Assert.False(PlaceTool.IsPiece("Beech1"));
	}

	[Fact]
	public void TheBrushScattersInsideItKeepingTheSpacing()
	{
		var (t, _) = Tool();
		t.Brush.Radius = 15;
		t.Density = 5;
		t.Spacing = 3;
		var p = t.Preview(new Vector2(64, 64));
		Assert.InRange(p.Count, 10, 40);
		Assert.All(p, o => Assert.True(Vector2.Distance(o.G, new Vector2(64, 64)) <= 15.01f));
		for (int i = 0; i < p.Count; i++)
		{
			for (int j = i + 1; j < p.Count; j++)
			{
				Assert.True(Vector2.Distance(p[i].G, p[j].G) >= 3 - 1e-3f);
			}
		}
		Assert.All(p, o => Assert.InRange(o.Scale, 0.8f, 1.2f));
		// On the ground (a little sunk, like the game places them).
		Assert.All(p, o => Assert.Equal(29.95f, o.Position.Y, 3));
		// The layout stays the same while the cursor moves.
		var q = t.Preview(new Vector2(70, 64));
		Assert.Equal(p.Count, q.Count);
		Assert.True(Vector2.Distance(p[0].G + new Vector2(6, 0), q[0].G) < 1e-3f);
	}

	[Fact]
	public void SingleIsExactlyAtTheCursorAndStaysAwayFromOthers()
	{
		var (t, _) = Tool(new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(32, 30, 32), Vector3.Zero, 1, false));
		t.Single = true;
		var p = Assert.Single(t.Preview(new Vector2(50, 50)));
		Assert.Equal(new Vector2(50, 50), p.G);
		// Right on top of a standing tree (world 32, 32 is grid 64, 64): nothing.
		Assert.Empty(t.Preview(new Vector2(64.1f, 64)));
	}

	[Fact]
	public void ALineHasOneEveryFewMetresFacingAlongIt()
	{
		var (t, _) = Tool();
		t.Mode = PlaceTool.Modes.Line;
		t.Every = 4;
		t.Points.Add(new Vector2(20, 64));
		t.Points.Add(new Vector2(60, 64));
		var p = t.Preview(null);
		Assert.Equal(11, p.Count);
		Assert.All(p, o => Assert.Equal(90, Yaw(o), 2));
		t.Along = false;
		t.RandomYaw = false;
		Assert.All(t.Preview(null), o => Assert.Equal(0, Yaw(o), 2));
	}

	[Fact]
	public void EndToEndPiecesMeetAndStack()
	{
		var (t, _) = Tool();
		t.Chosen.Clear();
		t.Chosen.Add(Wall);
		t.AutoSnap();
		Assert.True(t.EndToEnd);
		Assert.True(t.Single);
		t.Mode = PlaceTool.Modes.Line;
		t.Points.Add(new Vector2(20, 64));
		t.Points.Add(new Vector2(41, 64));
		var e = t.EndsOf(Wall)!;
		var p = t.Preview(null);
		int n = (int)(21 / e.Len);
		Assert.Equal(n, p.Count);
		// Each piece starts where the last one ends.
		for (int i = 1; i < p.Count; i++)
		{
			Assert.Equal(e.Len, Vector2.Distance(p[i].G, p[i - 1].G), 2);
		}
		Assert.Equal(21 - n * e.Len, t.SnapGap, 2);
		Assert.All(p, o => Assert.Equal(0, o.Scale));
		t.Layers = 2;
		var two = t.Preview(null);
		Assert.Equal(2 * n, two.Count);
		Assert.Equal(e.H, two[1].Position.Y - two[0].Position.Y, 2);
	}

	[Fact]
	public void ARectangleEndToEndSnapsToWholePieces()
	{
		var (t, _) = Tool();
		t.Chosen.Clear();
		t.Chosen.Add(Wall);
		t.AutoSnap();
		t.Mode = PlaceTool.Modes.Line;
		t.LineShape = PlaceTool.LineShapes.Rect;
		t.FigA = new Vector2(40, 40);
		t.FigB = new Vector2(47, 45);
		var e = t.EndsOf(Wall)!;
		var b = t.SnapFigure(t.FigA.Value, t.FigB.Value);
		Assert.Equal(0, (b.X - 40) % e.Len, 3);
		var p = t.Preview(null);
		Assert.Equal(2 * ((b.X - 40) / e.Len + (b.Y - 40) / e.Len), p.Count, 1);
	}

	[Fact]
	public void AGridHasOneInEachCellAndTurns()
	{
		var (t, _) = Tool();
		t.Mode = PlaceTool.Modes.Grid;
		t.Cell = 4;
		t.GridA = new Vector2(40, 40);
		t.GridB = new Vector2(60, 52);
		var p = t.Preview(null);
		Assert.Equal(15, p.Count);
		Assert.Contains(p, o => Vector2.Distance(o.G, new Vector2(42, 42)) < 1e-3f);
		Assert.True(t.TurnShape(90));
		var q = t.Preview(null);
		Assert.Equal(15, q.Count);
		Assert.DoesNotContain(q, o => Vector2.Distance(o.G, new Vector2(42, 42)) < 1e-3f);
	}

	[Fact]
	public void AZoneIsFilledByDensity()
	{
		var (t, _) = Tool();
		t.Mode = PlaceTool.Modes.Zone;
		t.Density = 5;
		t.Spacing = 2;
		foreach (var p in new[] { new Vector2(30, 30), new Vector2(70, 30), new Vector2(70, 70), new Vector2(30, 70) })
		{
			t.Points.Add(p);
		}
		var all = t.Preview(null);
		Assert.InRange(all.Count, 60, 80);
		Assert.All(all, o => Assert.InRange(o.G.X, 30, 70));
		t.Clump = 100;
		Assert.True(t.Preview(null).Count < all.Count / 2);
	}

	[Fact]
	public void ElevationsAndUnderwater()
	{
		var (t, s) = Tool();
		t.Single = true;
		t.Elevation = PlaceTool.Elevations.At;
		t.Elev = 50;
		Assert.Equal(50, t.Preview(new Vector2(50, 50))[0].Position.Y, 3);
		t.Elevation = PlaceTool.Elevations.Above;
		t.Elev = 2;
		Assert.Equal(31.95f, t.Preview(new Vector2(50, 50))[0].Position.Y, 3);
		Assert.Equal("Elevation: above the ground by 2.5 m.", t.NudgeElevation(0.5f));
		// Under water nothing grows (sea level is 30 in this test area: lower it there).
		t.Elevation = PlaceTool.Elevations.Ground;
		s.Shape(50, 50, Formula.Compile("-3", new string[0]), 3, 0, "pond");
		Assert.Empty(t.Preview(new Vector2(50, 50)));
	}

	[Fact]
	public void ASinglePieceSnapsBesideAnother()
	{
		// A wall standing at grid (64, 64), facing north.
		var (t, _) = Tool(new WorldScene.Thing(1, StableHash.Of(Wall), new Vector3(32, 30, 32), Vector3.Zero, 0, true));
		t.Chosen.Clear();
		t.Chosen.Add(Wall);
		t.AutoSnap();
		var e = t.EndsOf(Wall)!;
		var p = Assert.Single(t.Preview(new Vector2(64 + e.Len + 0.3f, 64.2f)));
		Assert.StartsWith("beside", t.SnappedTo);
		// End to end with it: one wall length along.
		Assert.Equal(e.Len, Vector2.Distance(p.G, new Vector2(64, 64)), 2);
		Assert.Equal(30, p.Position.Y, 2);
		t.OnTop = true;
		var top = Assert.Single(t.Preview(new Vector2(64.2f, 64)));
		Assert.StartsWith("on top", t.SnappedTo);
		// Its bottom on the top of the one below: one wall higher.
		Assert.Equal(30 + e.H, top.Position.Y, 2);
	}

	[Fact]
	public void PresetsLoadTheKindsTheWorldCanMake()
	{
		var t = new PlaceTool();
		string msg = t.Load(PlaceTool.BuiltIn[0], n => n != "Oak1");
		Assert.DoesNotContain("Oak1", t.Chosen);
		Assert.Equal(4, t.WeightOf("Beech1"));
		Assert.Contains("1 kind(s) this world cannot place left out", msg);
		Assert.Equal(50, t.Clump);
		Assert.StartsWith("None of the kinds", t.Load(PlaceTool.BuiltIn[0], _ => false));
	}

	private static (MainWindow W, EditSession S, Func<float, float, Avalonia.Point> Screen) Open()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.KeyPress(Key.T, RawInputModifiers.None, PhysicalKey.T, "t");
		w.View.SetCamera(new Vector3(0, 150, 1), Vector3.Zero, 1.6f);
		w.PlaceTool.Chosen.Clear();
		w.PlaceTool.Chosen.Add("Beech1");
		Avalonia.Point Screen(float gx, float gz)
		{
			var q = Vector4.Transform(new Vector4(gx - 64, 30, -(gz - 64), 1), w.View.ViewProj);
			return new Avalonia.Point((q.X / q.W + 1) / 2 * 1600, (1 - q.Y / q.W) / 2 * 1000);
		}
		return (w, s, Screen);
	}

	[AvaloniaFact]
	public void AClickPlacesWhatThePreviewShowsInOneStep()
	{
		var (w, s, screen) = Open();
		Assert.Equal(ToolMode.Place, w.View.Mode);
		Assert.True(w.PlacePanel.Card.IsVisible);
		w.MouseMove(screen(64, 64));
		int shown = w.PlaceInput.Shown.Length;
		Assert.True(shown > 0);
		w.MouseDown(screen(64, 64), MouseButton.Left);
		w.MouseUp(screen(64, 64), MouseButton.Left);
		Assert.Equal(shown, s.Pending.Added);
		Assert.StartsWith("Placed", s.UndoLabel);
		s.Undo();
		Assert.Equal(0, s.Pending.Added);
	}

	[AvaloniaFact]
	public void ShiftDragRemovesTheChosenKinds()
	{
		var (w, s, screen) = Open();
		w.PlaceInput.Tool.Single = true;
		w.MouseMove(screen(64, 64));
		w.MouseDown(screen(64, 64), MouseButton.Left);
		w.MouseUp(screen(64, 64), MouseButton.Left);
		Assert.Equal(1, s.Pending.Added);
		w.PlaceInput.Tool.Brush.Radius = 5;
		w.MouseDown(screen(60, 64), MouseButton.Left, RawInputModifiers.Shift);
		w.MouseMove(screen(66, 64), RawInputModifiers.Shift);
		// The removal happens on the brush's timer: one frame by hand.
		typeof(PlaceInput).GetMethod("Paint", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(w.PlaceInput, null);
		w.MouseUp(screen(66, 64), MouseButton.Left, RawInputModifiers.Shift);
		Assert.Equal(0, s.Pending.Added);
		Assert.Equal("Place: removed 1", s.UndoLabel);
	}

	[AvaloniaFact]
	public void ALineIsClickedAndPlacedWithEnter()
	{
		var (w, s, screen) = Open();
		w.PlacePanel.ModeButtons[PlaceTool.Modes.Line].RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
		Assert.Equal(PlaceTool.Modes.Line, w.PlaceTool.Mode);
		foreach (var x in new[] { 44f, 84f })
		{
			w.MouseDown(screen(x, 64), MouseButton.Left);
			w.MouseUp(screen(x, 64), MouseButton.Left);
		}
		Assert.Equal(2, w.PlaceTool.Points.Count);
		Assert.Equal(11, w.PlaceInput.Shown.Length);
		Assert.StartsWith("11 object(s) along the line", w.PlacePanel.Info.Text);
		w.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
		Assert.Equal(11, s.Pending.Added);
		Assert.Equal("Placed 11 along a line", s.UndoLabel);
		w.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.Backspace, null);
		Assert.Single(w.PlaceTool.Points);
		w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
		Assert.Empty(w.PlaceTool.Points);
	}
}

// What the Place tool remembers: your presets, favourites and recent kinds.
public class PlaceMemoryTests
{
	[AvaloniaFact]
	public async Task PresetsFavouritesAndRecentAreKept()
	{
		var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		var p = w.PlacePanel;
		p.Memory.Presets.Clear();
		w.PlaceTool.Chosen.Clear();
		w.PlaceTool.Chosen.Add("Birch1");
		w.PlaceTool.Weights["Birch1"] = 3;
		w.PlaceTool.Density = 7;
		p.AskName = () => Task.FromResult<string?>("My birches");
		p.Confirm = _ => Task.FromResult(true);
		p.SavePresetButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
		await Task.Delay(10);
		var mine = Assert.Single(p.Memory.Presets);
		Assert.Equal(7, mine.Density);
		Assert.Equal(3, mine.Kinds["Birch1"]);
		Assert.True(p.DeletePresetButton.IsEnabled);
		// Saved to the file: read back.
		var again = PlaceMemory.Load();
		Assert.Equal("My birches", again.Presets.Single().Name);
		p.Memory.ToggleFavourite("Oak1");
		Assert.Contains("Oak1", PlaceMemory.Load().Favourites);
		p.Memory.NoteRecent(new[] { "Beech1", "Oak1" });
		p.Memory.NoteRecent(new[] { "Rock_3" });
		Assert.Equal(new[] { "Rock_3", "Beech1", "Oak1" }, PlaceMemory.Load().Recent);
		p.DeletePresetButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
		await Task.Delay(10);
		Assert.Empty(p.Memory.Presets);
	}
}
