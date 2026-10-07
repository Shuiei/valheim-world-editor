using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Sculpting and painting the ground: the brushes' math (the web editor's applyBrush), undo and redo,
// what goes to the edit store, and saving into a world.
public class EditTests
{
	// A flat block of size × size zones at 30 m, with nothing edited, and its edit session.
	internal static EditSession Flat(int size = 2, params WorldScene.Thing[] things)
	{
		int w = size * 64 + 1;
		var ground = new Ground(w, w, 0, 0, size);
		Array.Fill(ground.Base, 30f);
		var scene = new WorldScene
		{
			World = new WorldSave { Directory = "none", SaveNumber = 1 }, Name = "test", X0 = 0, Z0 = 0, Size = size, W = w, H = w,
			Heights = Enumerable.Repeat(30f, w * w).ToArray(), Biomes = new int[w * w], Things = things.ToList(), BiomeColor = new byte[w * w * 4],
			Cx = -32 + (w - 1) / 2f, Cz = -32 + (w - 1) / 2f,
			Mask = new byte[w * w * 4], OceanDepth = new float[w * w], Limit = new float[w * w],
		};
		var session = new EditSession(scene, ground, new EditStore(scene.World));
		scene.Session = session;
		return session;
	}

	// A stroke held for a number of frames of 1/60 s at grid point (x, z).
	private static void Stroke(EditSession s, BrushTool tool, float x, float z, int frames = 30)
	{
		s.BeginStroke(tool, x, z);
		for (int i = 0; i < frames; i++)
		{
			s.StrokeStep(x, z, 1 / 60f);
		}
		s.EndStroke();
	}

	private static float At(EditSession s, int x, int z) => s.Scene.Heights[z * s.Scene.W + x];

	[Fact]
	public void RaiseLiftsTheMiddleMostAndLeavesTheOutside()
	{
		var s = Flat();
		Stroke(s, BrushTool.Raise, 64, 64);
		// 6 m a second × strength 0.4 × 0.5 s at the middle (full weight).
		Assert.Equal(30 + 6 * 0.4f * 0.5f, At(s, 64, 64), 2);
		Assert.True(At(s, 67, 64) > 30 && At(s, 67, 64) < At(s, 64, 64));
		Assert.Equal(30, At(s, 64 + 7, 64), 3);
		Assert.Equal(30, At(s, 10, 10), 3);
	}

	[Fact]
	public void LowerDigsAndTheGameLimitStopsItAt8Metres()
	{
		var s = Flat();
		s.Brush.Strength = 1;
		s.BeginStroke(BrushTool.Lower, 64, 64);
		for (int i = 0; i < 300; i++)
		{
			s.StrokeStep(64, 64, 0.1f);
		}
		string msg = s.EndStroke();
		Assert.Equal(22, At(s, 64, 64), 3);
		Assert.Contains("8 m", msg);
		Assert.Equal(1, s.Scene.Limit[64 * s.Scene.W + 64]);
	}

	[Fact]
	public void TheOuterLineOfPointsNeverMoves()
	{
		var s = Flat(1);
		s.Brush.Radius = 10;
		Stroke(s, BrushTool.Raise, 0, 30);
		Assert.Equal(30, At(s, 0, 30), 4);
		Assert.True(At(s, 1, 30) > 30);
	}

	[Fact]
	public void FlattenLevelsToTheGroundWhereTheStrokeStarts()
	{
		var s = Flat();
		s.Brush.Radius = 4;
		Stroke(s, BrushTool.Raise, 70, 64, 60);
		float high = At(s, 70, 64);
		s.Brush.Radius = 8;
		s.Brush.Strength = 1;
		s.BeginStroke(BrushTool.Flatten, 64, 64);
		Assert.Equal(30, s.Brush.Target, 3);
		for (int i = 0; i < 60; i++)
		{
			s.StrokeStep(64, 64, 0.1f);
		}
		s.EndStroke();
		Assert.True(At(s, 70, 64) < high - 0.5f, $"{At(s, 70, 64)} vs {high}");
		Assert.Equal(30, At(s, 64, 64), 2);
	}

	[Fact]
	public void SmoothEvensOutABump()
	{
		var s = Flat();
		s.Brush.Radius = 2;
		Stroke(s, BrushTool.Raise, 64, 64, 60);
		float bump = At(s, 64, 64) - At(s, 66, 64);
		s.Brush.Radius = 6;
		Stroke(s, BrushTool.Smooth, 64, 64, 60);
		Assert.True(At(s, 64, 64) - At(s, 66, 64) < bump / 2, $"{bump} → {At(s, 64, 64) - At(s, 66, 64)}");
	}

	[Fact]
	public void NaturalizeAddsBumps()
	{
		var s = Flat();
		s.Brush.Noise = new Noise(12345);
		s.Brush.Strength = 1;
		s.Brush.Radius = 12;
		Stroke(s, BrushTool.Natural, 64, 64, 120);
		var values = Enumerable.Range(58, 12).Select(x => At(s, x, 64)).ToList();
		Assert.True(values.Max() - values.Min() > 0.1f, string.Join(", ", values));
	}

	[Fact]
	public void RestoreBringsTheGeneratedGroundBack()
	{
		var s = Flat();
		Stroke(s, BrushTool.Raise, 64, 64);
		Stroke(s, BrushTool.PaintDirt, 64, 64);
		s.Brush.Strength = 1;
		s.Brush.Radius = 10;
		Stroke(s, BrushTool.Restore, 64, 64, 120);
		int g = 64 * s.Ground.W + 64;
		Assert.Equal(0, s.Ground.Mod[g]);
		Assert.Equal(0, s.Ground.PMod[g]);
		Assert.Equal(30, At(s, 64, 64), 3);
	}

	[Fact]
	public void PaintMovesTowardsItsColourAndShowsInTheMask()
	{
		var s = Flat();
		s.Brush.Strength = 1;
		Stroke(s, BrushTool.PaintPaved, 64, 64, 60);
		int g = 64 * s.Ground.W + 64;
		Assert.Equal(1, s.Ground.PMod[g]);
		Assert.Equal(new[] { 0f, 0, 1, 1 }, s.Ground.Paint[(g * 4)..(g * 4 + 4)].Select(v => MathF.Round(v, 2)).ToArray());
		Assert.Equal(255, s.Scene.Mask[g * 4 + 2]);
		Assert.Equal(0, s.Scene.Mask[g * 4]);
	}

	[Fact]
	public void UndoAndRedoPutTheGroundBackAndForth()
	{
		var s = Flat();
		Stroke(s, BrushTool.Raise, 64, 64);
		float raised = At(s, 64, 64);
		Assert.True(s.CanUndo);
		Assert.Equal("Raise", s.UndoLabel);
		Assert.Equal((1, 0, 0, 0), (s.Pending.Zones > 0 ? 1 : 0, s.Pending.Deleted, s.Pending.Added, s.Pending.Resets));
		Assert.True(s.Undo());
		Assert.Equal(30, At(s, 64, 64), 4);
		// Back as loaded: nothing waits to be saved.
		Assert.Equal(0, s.Pending.Zones);
		Assert.True(s.CanRedo);
		Assert.True(s.Redo());
		Assert.Equal(raised, At(s, 64, 64), 4);
		Assert.True(s.Pending.Zones > 0);
		// A new stroke after an undo drops the redo.
		s.Undo();
		Stroke(s, BrushTool.Lower, 20, 20);
		Assert.False(s.CanRedo);
	}

	[Fact]
	public void AStrokeOverAZoneEdgeChangesBothZones()
	{
		var s = Flat(2);
		Stroke(s, BrushTool.Raise, 64, 30);
		Assert.Equal(2, s.Pending.Zones);
		var left = s.Edits.Get(0, 0)!;
		var right = s.Edits.Get(1, 0)!;
		// The shared edge point: the last column of the left zone, the first of the right one.
		Assert.True(left.Modified[30 * 65 + 64]);
		Assert.True(right.Modified[30 * 65]);
		Assert.Equal(left.Level[30 * 65 + 64], right.Level[30 * 65], 5);
	}

	[Fact]
	public void ZonesOfEdgePointsAreBothSides()
	{
		var g = new Ground(129, 129, 0, 0, 2);
		Assert.Equal(new[] { (0, 0) }, g.ZonesOf(new[] { 10 * 129 + 10 }));
		Assert.Equal(new[] { (0, 0), (1, 0) }, g.ZonesOf(new[] { 10 * 129 + 64 }));
		Assert.Equal(new[] { (0, 0), (1, 0), (0, 1), (1, 1) }, g.ZonesOf(new[] { 64 * 129 + 64 }));
	}

	[Fact]
	public void BrushShapesAndFalloffs()
	{
		var b = new Brush { Radius = 10 };
		Assert.Equal(1, b.Weight(0, 0), 4);
		Assert.Equal(0, b.Weight(10, 0), 4);
		Assert.Equal(0.5f, b.Weight(5, 0), 4);
		b.Falloff = Falloff.Sharp;
		Assert.Equal(1, b.Weight(9.9f, 0), 4);
		b.Shape = BrushShape.Square;
		Assert.Equal(1, b.Weight(9, 9), 4);
		b.Turn = 45;
		Assert.Equal(0, b.Weight(9, 9), 4);
		Assert.Equal(1, b.Weight(13, 0), 4);
		Assert.True(b.Reach > 14);
		b.Shape = BrushShape.Ring;
		Assert.Equal(0, b.Weight(0, 0), 4);
		Assert.Equal(1, b.Weight(7, 0), 4);
		Assert.NotNull(b.InnerOutline(16));
	}

	[Fact]
	public void TheNoiseIsTheWebEditors()
	{
		// Values from the web editor's reseed(12345) and fbm (editor.html).
		var n = new Noise(12345);
		Assert.Equal(0.04665f, n.Fbm(3.3f, 7.7f), 4);
		Assert.Equal(0.29457f, n.Fbm(-12.25f, 40.5f), 4);
		Assert.Equal(-0.07274f, n.Fbm(100.1f, -3.9f), 4);
	}

	[Fact]
	public void UntouchedZonesKeepTheirSavedValues()
	{
		var s = Flat(1);
		var e = s.Ground.ZoneEdit(0, 0);
		Assert.True(e.IsEmpty);
	}

	[Fact]
	public void SavingWritesTheGroundIntoTheWorld()
	{
		string dir = CopyFixture();
		try
		{
			var scene = WorldScene.Load(dir, 0, 0, 1);
			var s = scene.Session!;
			int g = 32 * scene.W + 32;
			float before = scene.Heights[g];
			Stroke(s, BrushTool.Raise, 32, 32, 60);
			float after = scene.Heights[g];
			Assert.True(after > before + 1);
			var res = s.Save();
			Assert.True(res.Saved, res.Message);
			Assert.Equal(0, s.Pending.Zones);
			Assert.False(s.CanUndo);
			var again = WorldScene.Load(dir, 0, 0, 1);
			Assert.Equal(after, again.Heights[g], 3);
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
		}
	}

	// The test world (tests/fixtures/CITest) copied where saving cannot harm it.
	internal static string CopyFixture()
	{
		string? root = AppContext.BaseDirectory;
		while (root != null && !Directory.Exists(Path.Combine(root, "tests", "fixtures", "CITest")))
		{
			root = Path.GetDirectoryName(root);
		}
		Assert.NotNull(root);
		string dir = Path.Combine(Path.GetTempPath(), "vwe-native-" + Guid.NewGuid().ToString("N")[..10], "CITest");
		Directory.CreateDirectory(dir);
		foreach (string f in Directory.GetFiles(Path.Combine(root, "tests", "fixtures", "CITest")))
		{
			File.Copy(f, Path.Combine(dir, Path.GetFileName(f)));
		}
		return dir;
	}

	// ---- In the window.

	[AvaloniaFact]
	public void NumberKeysPickTheBrushesAndEscapeGoesBackToView()
	{
		var w = new MainWindow(load: false) { Width = 1000, Height = 700 };
		w.Show();
		w.KeyPress(Key.D3, RawInputModifiers.None, PhysicalKey.Digit3, "3");
		Assert.Equal(BrushTool.Flatten, w.Tools.Tool);
		Assert.Equal(BrushTool.Flatten, w.View.Tool);
		w.KeyPress(Key.D0, RawInputModifiers.None, PhysicalKey.Digit0, "0");
		Assert.Equal(BrushTool.Natural, w.Tools.Tool);
		w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
		Assert.Null(w.Tools.Tool);
		Assert.Null(w.View.Tool);
	}

	[AvaloniaFact]
	public void TheSlidersSetTheBrush()
	{
		var w = new MainWindow(load: false) { Width = 1000, Height = 700 };
		w.Show();
		w.Tools.SizeSlider.Value = 12.5;
		w.Tools.StrengthSlider.Value = 0.8;
		w.Tools.ShapeBox.SelectedIndex = 1;
		w.Tools.FalloffBox.SelectedIndex = 5;
		Assert.Equal(12.5f, w.Tools.Brush.Radius);
		Assert.Equal(0.8f, w.Tools.Brush.Strength, 4);
		Assert.Equal(BrushShape.Square, w.Tools.Brush.Shape);
		Assert.Equal(Falloff.Sharp, w.Tools.Brush.Falloff);
	}

	[AvaloniaFact]
	public async Task UndoRedoAndSaveFromTheWindow()
	{
		var w = new MainWindow(load: false) { Width = 1000, Height = 700 };
		w.Show();
		var s = Flat();
		w.Edit(s);
		Assert.Equal(s.Brush, w.Tools.Brush);
		Assert.False(w.SaveButton.IsEnabled);
		Stroke(s, BrushTool.Raise, 32, 32);
		Avalonia.Threading.Dispatcher.UIThread.RunJobs();
		Assert.True(w.SaveButton.IsEnabled);
		Assert.True(w.UndoButton.IsEnabled);
		Assert.StartsWith("Unsaved: 1 zone", w.PendingText.Text);
		w.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
		Assert.Equal(30, At(s, 32, 32), 4);
		Assert.Equal("All saved", w.PendingText.Text);
		Assert.True(w.RedoButton.IsEnabled);
		w.KeyPress(Key.Z, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.Z, "Z");
		Assert.True(At(s, 32, 32) > 30);
		// Saving asks first; saying no writes nothing.
		string? asked = null;
		w.ConfirmSave = q => { asked = q; return Task.FromResult(false); };
		w.Tell = _ => Task.CompletedTask;
		await w.Save();
		Assert.Contains("1 zone", asked);
		Assert.True(s.Pending.Zones > 0);
	}
}
