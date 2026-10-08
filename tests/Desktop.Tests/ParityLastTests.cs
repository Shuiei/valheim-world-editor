using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The last web editor features in the native app: the Area tool's cut and fill following every change
// of the ground, the Place tool's fit (spacing for kinds wider than it), the chooser's Shift + click,
// Untick all, all / none rows and folding categories (remembered), the green markers on new objects
// not saved or applied yet, and placing a kind switched off in View switching it on.
[Collection("World files")]
public class ParityLastTests
{
	private sealed class Run : IDisposable
	{
		public MainWindow W { get; }
		public string Dir { get; } = EditTests.CopyFixture();
		public EditSession S { get; }
		public PlacePanel P => W.PlacePanel;
		public PlaceTool T => W.PlaceTool;
		public string? Said;

		public Run(ToolMode mode = ToolMode.Place, Prefs? prefs = null)
		{
			W = new MainWindow(load: false, prefs: prefs) { Width = 1600, Height = 1000 };
			var scene = WorldScene.Load(Dir, 0, 0, 1);
			W.Show();
			W.View.Show(scene, null);
			W.Edit(scene.Session!);
			S = scene.Session!;
			W.Tools.ChooseMode(mode);
			P.Message += t => Said = t;
			P.Memory.Presets.Clear();
			P.Memory.Favourites.Clear();
			P.Memory.Recent.Clear();
		}

		public void Dispose() => Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
	}

	private static void Click(Button b)
	{
		b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Dispatcher.UIThread.RunJobs();
	}

	private static List<CheckBox> Ticks(Run r) => r.P.List.GetLogicalDescendants().OfType<CheckBox>().ToList();

	private static void Choose(Run r, params string[] names)
	{
		r.T.Chosen.Clear();
		r.T.Chosen.AddRange(names);
		typeof(PlacePanel).GetMethod("Chosen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(r.P, new object[] { true });
		Dispatcher.UIThread.RunJobs();
	}

	// ---- Cut and fill

	[AvaloniaFact]
	public async Task CutAndFillFollowsTheGroundAndTheAction()
	{
		using var r = new Run(ToolMode.Area);
		var a = r.W.View.Area;
		a.Clear();
		a.Points.Add(new Vector2(12, 12));
		a.Points.Add(new Vector2(22, 22));
		a.Notify();
		var p = r.W.AreaPanel;
		p.Choose(AreaPanel.Act.Flatten);
		Assert.EndsWith("Ground inside: 0 m³ raised, 0 m³ dug since generated.", p.VolumeText.Text);
		Assert.StartsWith("Flatten to ", p.VolumeText.Text);
		// Another action: its own first line at once.
		p.Choose(AreaPanel.Act.Raise);
		p.AmountBox.Value = 2;
		Assert.StartsWith("Raise 2 m: fill ", p.VolumeText.Text);
		p.Choose(AreaPanel.Act.Lower);
		Assert.StartsWith("Lower 2 m: dig ", p.VolumeText.Text);
		// Raised: the ground inside counts it, after the change itself (not only when the panel changes).
		p.Choose(AreaPanel.Act.Raise);
		await p.Apply();
		Dispatcher.UIThread.RunJobs();
		Assert.DoesNotContain("Ground inside: 0 m³ raised", p.VolumeText.Text);
		Assert.Contains(" m³ raised, 0 m³ dug since generated.", p.VolumeText.Text);
		// Undone: back to nothing raised.
		r.S.Undo();
		Dispatcher.UIThread.RunJobs();
		Assert.EndsWith("Ground inside: 0 m³ raised, 0 m³ dug since generated.", p.VolumeText.Text);
	}

	// ---- Fit

	private static PlaceTool FitTool(params (string Name, float Width)[] kinds)
	{
		var t = new PlaceTool { ModelBox = n => kinds.FirstOrDefault(k => k.Name == n) is { Name: not null } k ? (new Vector3(-k.Width / 2, 0, -1), new Vector3(k.Width / 2, 3, 1)) : null };
		t.Chosen.Clear();
		t.Chosen.AddRange(kinds.Select(k => k.Name));
		t.SizeMin = 100;
		t.SizeMax = 100;
		return t;
	}

	[Fact]
	public void TheWidestChosenKindAtItsLargestSizeIsFound()
	{
		var t = FitTool(("Small", 2), ("Big", 6));
		Assert.Equal((6f, "Big"), t.Widest());
		// The largest size counts (Size max 150 %).
		t.SizeMax = 150;
		Assert.Equal(9, t.Widest().Width, 3);
		// The depth counts when it is the larger side.
		var deep = new PlaceTool { ModelBox = _ => (new Vector3(-1, 0, -4), new Vector3(1, 1, 4)) };
		deep.Chosen.Clear();
		deep.Chosen.Add("Deep");
		deep.SizeMax = 100;
		Assert.Equal(8, deep.Widest().Width, 3);
		// No model known: nothing.
		var none = new PlaceTool();
		Assert.Equal((0f, (string?)null), none.Widest());
		Assert.Null(none.FitHint());
		Assert.False(none.Fit());
	}

	[Fact]
	public void TheHintSaysWhenTheWidestKindOverlapsAndFitSpacesThem()
	{
		var t = FitTool(("Big", 6.3f));
		t.Mode = PlaceTool.Modes.Brush;
		t.Spacing = 4;
		Assert.Equal("Big is about 6.3 m wide: 4 m apart they overlap.", t.FitHint());
		Assert.True(t.Fit());
		Assert.Equal(6.5f, t.Spacing);
		Assert.Null(t.FitHint());
		// A quarter metre of slack: no hint.
		t.Spacing = 6.1f;
		Assert.Null(t.FitHint());
		// Grid: the cell; Line: the distance along it; End to end: none (the pieces' length).
		t.Mode = PlaceTool.Modes.Grid;
		t.Cell = 2;
		Assert.Equal(2, t.SpacingNow);
		Assert.Contains("2 m apart", t.FitHint());
		t.Fit();
		Assert.Equal(6.5f, t.Cell);
		t.Mode = PlaceTool.Modes.Line;
		t.Every = 1.5f;
		Assert.Contains("1.5 m apart", t.FitHint());
		t.Fit();
		Assert.Equal(6.5f, t.Every);
		t.EndToEnd = true;
		Assert.Null(t.SpacingNow);
		Assert.Null(t.FitHint());
		Assert.False(t.Fit());
		t.Mode = PlaceTool.Modes.Zone;
		Assert.Equal(t.Spacing, t.SpacingNow);
	}

	[Fact]
	public void FitStopsAtTheSettingsLargestValue()
	{
		var t = FitTool(("Huge", 50));
		t.Mode = PlaceTool.Modes.Brush;
		t.Fit();
		Assert.Equal(PlaceTool.MaxSpacing, t.Spacing);
		t.Mode = PlaceTool.Modes.Grid;
		t.Fit();
		Assert.Equal(PlaceTool.MaxCell, t.Cell);
		t.Mode = PlaceTool.Modes.Line;
		t.Fit();
		Assert.Equal(PlaceTool.MaxEvery, t.Every);
	}

	[AvaloniaFact]
	public void ThePanelShowsTheHintAndFitMovesTheSlider()
	{
		using var r = new Run();
		r.T.ModelBox = n => n == "Beech1" ? (new Vector3(-4, 0, -4), new Vector3(4, 10, 4)) : null;
		r.T.SizeMax = 100;
		r.T.Mode = PlaceTool.Modes.Brush;
		Choose(r, "Beech1");
		r.P.SpacingSlider.Value = 3;
		r.T.Notify();
		r.W.PlaceInput.Refresh();
		Assert.True(((Control)Avalonia.VisualTree.VisualExtensions.GetVisualParent(r.P.FitButton)!).IsVisible);
		Assert.Equal("Beech1 is about 8.0 m wide: 3 m apart they overlap.", r.P.FitText.Text);
		Click(r.P.FitButton);
		Assert.Equal(8, r.P.SpacingSlider.Value);
		Assert.Equal(8, r.T.Spacing);
		Assert.False(((Control)Avalonia.VisualTree.VisualExtensions.GetVisualParent(r.P.FitButton)!).IsVisible);
		// Grid: the cell slider.
		r.T.Mode = PlaceTool.Modes.Grid;
		r.P.CellSlider.Value = 2;
		r.W.PlaceInput.Refresh();
		Click(r.P.FitButton);
		Assert.Equal(8, r.P.CellSlider.Value);
		// A kind narrower than the spacing: no hint.
		r.T.ModelBox = _ => (new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 1, 0.5f));
		r.W.PlaceInput.Refresh();
		Assert.False(((Control)Avalonia.VisualTree.VisualExtensions.GetVisualParent(r.P.FitButton)!).IsVisible);
	}

	// ---- The chooser

	[AvaloniaFact]
	public void PickFromWorldPlacesOnlyThatKindOrWithShiftAddsIt()
	{
		using var r = new Run();
		var kinds = r.P.Creatable().Select(c => c.Name).Take(3).ToList();
		Choose(r, kinds[0]);
		Click(r.P.PickButton);
		Assert.Equal("Click an object to pick its kind for placing. Esc cancels.", r.Said);
		r.W.PlaceInput.PickOnce!(kinds[1], true);
		Assert.Equal(new[] { kinds[0], kinds[1] }, r.T.Chosen);
		Assert.Equal($"Placing {kinds[0]}, {kinds[1]}.", r.Said);
		// Shift + click on a ticked kind takes it away.
		Click(r.P.PickButton);
		r.W.PlaceInput.PickOnce!(kinds[1], true);
		Assert.Equal(new[] { kinds[0] }, r.T.Chosen);
		// A plain click: only that kind.
		Click(r.P.PickButton);
		r.W.PlaceInput.PickOnce!(kinds[2], false);
		Assert.Equal(new[] { kinds[2] }, r.T.Chosen);
		Assert.Equal($"Placing {kinds[2]}.", r.Said);
	}

	[AvaloniaFact]
	public void EscCancelsPickingFromTheWorld()
	{
		using var r = new Run();
		string? said = null;
		r.W.PlaceInput.Message += t => said = t;
		Click(r.P.PickButton);
		Assert.True(r.W.PlaceInput.Key(Key.Escape, false, false));
		Assert.Null(r.W.PlaceInput.PickOnce);
		Assert.Equal("Picking cancelled.", said);
		// Nothing to cancel: Esc is not taken.
		Assert.False(r.W.PlaceInput.Key(Key.Escape, false, false));
	}

	[AvaloniaFact]
	public void AChipTicksItsKindOrWithShiftPlacesOnlyIt()
	{
		using var r = new Run();
		var kinds = r.P.Creatable().Select(c => c.Name).Take(3).ToList();
		r.P.Memory.Favourites.AddRange(kinds);
		Choose(r, kinds[0]);
		r.P.ChipClicked(kinds[1], shift: false);
		Assert.Equal(new[] { kinds[0], kinds[1] }, r.T.Chosen);
		r.P.ChipClicked(kinds[1], shift: false);
		Assert.Equal(new[] { kinds[0] }, r.T.Chosen);
		r.P.ChipClicked(kinds[2], shift: true);
		Assert.Equal(new[] { kinds[2] }, r.T.Chosen);
		// The chips show what is ticked.
		var chips = r.P.Favourites.Children.OfType<ToggleButton>().ToList();
		Assert.Equal(kinds, chips.Select(c => (string)c.Content!));
		Assert.Equal(new[] { false, false, true }, chips.Select(c => c.IsChecked == true));
	}

	[AvaloniaFact]
	public void ShiftClickingAChipWithTheMousePlacesOnlyIt()
	{
		using var r = new Run();
		var kinds = r.P.Creatable().Select(c => c.Name).Take(2).ToList();
		r.P.Memory.Favourites.AddRange(kinds);
		Choose(r, kinds[0]);
		Click(r.P.KindsButton);
		r.W.UpdateLayout();
		Point At(ToggleButton chip) => chip.TranslatePoint(new Point(chip.Bounds.Width / 2, chip.Bounds.Height / 2), r.W)!.Value;
		var chip = r.P.Favourites.Children.OfType<ToggleButton>().Single(c => (string)c.Content! == kinds[1]);
		r.W.MouseDown(At(chip), MouseButton.Left, RawInputModifiers.Shift);
		r.W.MouseUp(At(chip), MouseButton.Left, RawInputModifiers.Shift);
		Dispatcher.UIThread.RunJobs();
		Assert.Equal(new[] { kinds[1] }, r.T.Chosen);
		// Without Shift: added.
		r.W.UpdateLayout();
		chip = r.P.Favourites.Children.OfType<ToggleButton>().Single(c => (string)c.Content! == kinds[0]);
		r.W.MouseDown(At(chip), MouseButton.Left);
		r.W.MouseUp(At(chip), MouseButton.Left);
		Dispatcher.UIThread.RunJobs();
		Assert.Equal(new[] { kinds[1], kinds[0] }, r.T.Chosen);
	}

	[AvaloniaFact]
	public void UntickAllAndTheRowsAllAndNone()
	{
		using var r = new Run();
		var kinds = r.P.Creatable().Select(c => c.Name).Take(4).ToList();
		r.P.Memory.Favourites.AddRange(new[] { kinds[0], kinds[1], "NotAThing_xyz" });
		r.P.Memory.Recent.AddRange(new[] { kinds[2], kinds[3] });
		Choose(r, kinds[3]);
		Click(r.P.FavAllButton);
		// Only kinds this world can place.
		Assert.Equal(new[] { kinds[3], kinds[0], kinds[1] }, r.T.Chosen);
		Click(r.P.FavNoneButton);
		Assert.Equal(new[] { kinds[3] }, r.T.Chosen);
		Click(r.P.RecentAllButton);
		Assert.Equal(new[] { kinds[3], kinds[2] }, r.T.Chosen);
		Click(r.P.RecentNoneButton);
		Assert.Empty(r.T.Chosen);
		Choose(r, kinds[0], kinds[1]);
		Click(r.P.UntickAllButton);
		Assert.Empty(r.T.Chosen);
		Assert.Empty(r.P.Memory.Chosen);
		Assert.Equal("Nothing to place yet: + Add kinds, or a preset.", r.P.Note.Text);
	}

	[AvaloniaFact]
	public void TheCategoriesFoldAndCountWhatIsTicked()
	{
		using var r = new Run();
		var trees = r.P.Creatable().Where(c => c.Kind == ObjectKind.Trees).Select(c => c.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
		var rocks = r.P.Creatable().Where(c => c.Kind == ObjectKind.Rocks).Select(c => c.Name).ToList();
		Assert.NotEmpty(trees);
		Assert.NotEmpty(rocks);
		Choose(r, trees[0]);
		Click(r.P.KindsButton);
		// Trees open (the default), the others folded: only the trees' boxes are made.
		Assert.True(r.P.Groups[ObjectKind.Trees].IsExpanded);
		Assert.False(r.P.Groups[ObjectKind.Rocks].IsExpanded);
		Assert.Equal(trees, Ticks(r).Select(b => (string)b.Content!));
		string Count(ObjectKind k) => ((Grid)r.P.Groups[k].Header!).Children.OfType<TextBlock>().Last().Text!;
		Assert.Equal($"1 / {trees.Count}", Count(ObjectKind.Trees));
		Assert.Equal($"{rocks.Count}", Count(ObjectKind.Rocks));
		// Ticking in the list counts at once.
		Ticks(r).First(b => (string)b.Content! == trees[^1]).IsChecked = true;
		Assert.Equal($"{(trees.Count > 1 ? 2 : 1)} / {trees.Count}", Count(ObjectKind.Trees));
		// Opening a category makes its boxes and is remembered.
		int changed = 0;
		r.P.OpenKindsChanged += () => changed++;
		r.P.Groups[ObjectKind.Rocks].IsExpanded = true;
		Assert.Contains(ObjectKind.Rocks, r.P.OpenKinds);
		Assert.Equal(1, changed);
		Assert.Equal(trees.Count + rocks.Count, Ticks(r).Count);
		r.P.Groups[ObjectKind.Trees].IsExpanded = false;
		Assert.DoesNotContain(ObjectKind.Trees, r.P.OpenKinds);
		Assert.Equal(2, changed);
		// Searching opens every category with a match, and is not remembered.
		r.P.Search.Text = rocks[0];
		Assert.All(r.P.Groups.Values, g => Assert.True(g.IsExpanded));
		r.P.Groups[ObjectKind.Rocks].IsExpanded = false;
		Assert.Contains(ObjectKind.Rocks, r.P.OpenKinds);
		Assert.Equal(2, changed);
		r.P.Search.Text = "no kind is called this";
		Assert.Empty(r.P.Groups);
		Assert.Contains("Nothing matches.", r.P.List.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));
		// Back without a search: as remembered.
		r.P.Search.Text = "";
		Assert.False(r.P.Groups[ObjectKind.Trees].IsExpanded);
		Assert.True(r.P.Groups[ObjectKind.Rocks].IsExpanded);
	}

	[AvaloniaFact]
	public void TheOpenCategoriesAreRememberedBetweenRuns()
	{
		string file = Path.Combine(Path.GetTempPath(), $"vwe-prefs-open-{Guid.NewGuid():N}.json");
		try
		{
			File.WriteAllText(file, "{\"place.openKinds\": [\"Rocks\", \"NotAKind\"]}");
			using (var r = new Run(prefs: Prefs.Load(file)))
			{
				Assert.Equal(new[] { ObjectKind.Rocks }, r.P.OpenKinds);
				Click(r.P.KindsButton);
				Assert.True(r.P.Groups[ObjectKind.Rocks].IsExpanded);
				Assert.False(r.P.Groups[ObjectKind.Trees].IsExpanded);
				r.P.Groups[ObjectKind.Trees].IsExpanded = true;
				r.W.Prefs.Flush();
			}
			Assert.Equal(new[] { "Rocks", "Trees" }, Prefs.Load(file).Get<string[]?>("place.openKinds", null));
		}
		finally
		{
			File.Delete(file);
		}
	}

	// ---- Markers on new objects

	private static NewObject Tree(EditSession s, string name, float x, float z) => new(0, StableHash.Of(name), new Vector3(x + s.Scene.X0 * 64 - 32, 30, z + s.Scene.Z0 * 64 - 32), Vector3.Zero, 1f);

	[AvaloniaFact]
	public void NewObjectsCarryAMarkerUntilSavedWhenTheirKindIsShown()
	{
		using var r = new Run();
		var s = r.S;
		Assert.Empty(r.W.View.NewMarkers(s.Scene));
		var added = s.Commit("Placed", null, Array.Empty<int>(), new[] { (Tree(s, "Beech1", 20, 20), false), (Tree(s, "Pickable_Stone", 30, 30), false) });
		Dispatcher.UIThread.RunJobs();
		var marks = r.W.View.NewMarkers(s.Scene);
		Assert.Equal(2, marks.Count);
		// Just above the object, in the view's frame.
		var t = s.Scene.Things[added[0]];
		Assert.Contains(new Vector3(t.Position.X - s.Scene.Cx, t.Position.Y + 0.4f, -(t.Position.Z - s.Scene.Cz)), marks);
		// A kind switched off in View: its markers go too.
		r.W.KindBoxes[ObjectKind.Pickables].IsChecked = false;
		Assert.Single(r.W.View.NewMarkers(s.Scene));
		r.W.KindBoxes[ObjectKind.Pickables].IsChecked = true;
		// Deleted: no marker.
		s.Delete(new[] { added[1] });
		Assert.Single(r.W.View.NewMarkers(s.Scene));
		// Objects that were in the world already never have one.
		Assert.DoesNotContain(s.Scene.Things.Where(x => x.Id >= 0), x => marks.Contains(new Vector3(x.Position.X - s.Scene.Cx, x.Position.Y + 0.4f, -(x.Position.Z - s.Scene.Cz))));
	}

	[AvaloniaFact]
	public async Task AppliedObjectsLoseTheirMarkerInLiveMode()
	{
		using var game = new FakeGame();
		var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
		w.Show();
		var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), "test");
		var scene = WorldScene.Load(world, 0, 0, 1);
		var s = scene.Session!;
		w.View.Show(scene, null);
		w.Edit(s);
		s.Commit("Placed", null, Array.Empty<int>(), new[] { (Tree(s, "Beech1", 20, 20), false) });
		Assert.Single(w.View.NewMarkers(scene));
		var o = await s.ApplyLive();
		Assert.True(o.Done, o.Message);
		Assert.Empty(w.View.NewMarkers(scene));
		// Another one placed afterwards: marked until it is applied too.
		s.Commit("Placed", null, Array.Empty<int>(), new[] { (Tree(s, "Beech1", 25, 25), false) });
		Assert.Single(w.View.NewMarkers(scene));
	}

	[Fact]
	public async Task TheLiveSyncKnowsWhichNewObjectsTheGameHas()
	{
		using var game = new FakeGame();
		var world = await WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), "test");
		var s = WorldScene.Load(world, 0, 0, 1).Session!;
		var i = s.Commit("Placed", null, Array.Empty<int>(), new[] { (Tree(s, "Beech1", 20, 20), false) })[0];
		int id = s.Scene.Things[i].Id;
		Assert.False(world.LiveSync.IsLive(id));
		await s.ApplyLive();
		Assert.True(world.LiveSync.IsLive(id));
		Assert.False(world.LiveSync.IsLive(12345));
	}

	// ---- Hidden kinds switched on

	[AvaloniaFact]
	public void PlacingAKindSwitchedOffInViewSwitchesItOnAndSaysSo()
	{
		using var r = new Run();
		var s = r.S;
		var box = r.W.KindBoxes[ObjectKind.Pickables];
		box.IsChecked = false;
		Assert.False(r.W.View.IsShown(ObjectKind.Pickables));
		r.W.MessageText.Text = "Placed 1 object.";
		s.Commit("Placed", null, Array.Empty<int>(), new[] { (Tree(s, "Pickable_Stone", 20, 20), false) });
		Dispatcher.UIThread.RunJobs();
		Assert.True(box.IsChecked);
		Assert.True(r.W.View.IsShown(ObjectKind.Pickables));
		Assert.Equal("Placed 1 object. Switched on Pickables in View, so what you placed stays visible.", r.W.MessageText.Text);
	}

	[AvaloniaFact]
	public void KindsAlreadyShownChangeNothingAndSeveralAreNamedTogether()
	{
		using var r = new Run();
		var s = r.S;
		r.W.MessageText.Text = "Placed.";
		s.Commit("Placed", null, Array.Empty<int>(), new[] { (Tree(s, "Beech1", 20, 20), false) });
		Dispatcher.UIThread.RunJobs();
		Assert.Equal("Placed.", r.W.MessageText.Text);
		// Two kinds off: both named, in one sentence; no message of the tool before it: just this.
		r.W.KindBoxes[ObjectKind.Pickables].IsChecked = false;
		r.W.KindBoxes[ObjectKind.Rocks].IsChecked = false;
		r.W.MessageText.Text = "";
		s.Commit("Pasted", null, Array.Empty<int>(), new[] { (Tree(s, "Pickable_Stone", 20, 20), false), (Tree(s, "Rock_3", 22, 22), false), (Tree(s, "Rock_3", 24, 24), false) });
		Dispatcher.UIThread.RunJobs();
		Assert.True(r.W.KindBoxes[ObjectKind.Rocks].IsChecked);
		Assert.True(r.W.KindBoxes[ObjectKind.Pickables].IsChecked);
		Assert.StartsWith("Switched on ", r.W.MessageText.Text);
		Assert.Contains("Pickables", r.W.MessageText.Text);
		Assert.Contains("Rocks", r.W.MessageText.Text);
		// Undo and redo bring objects back without switching anything (only new placing does).
		r.W.KindBoxes[ObjectKind.Rocks].IsChecked = false;
		s.Undo();
		s.Redo();
		Dispatcher.UIThread.RunJobs();
		Assert.False(r.W.KindBoxes[ObjectKind.Rocks].IsChecked);
	}
}
