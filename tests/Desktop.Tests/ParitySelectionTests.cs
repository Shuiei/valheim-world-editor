using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// Saved selections (per world, found again by kind and place after the ids changed) and the
// eyedropper for the Replace lists (Area and Select): the store's edge cases, the Select panel's
// Keep / Select / Add / Forget, and picking a kind with a click in the 3D view.
[Collection("Saved selections")]
public sealed class ParitySelectionTests : IDisposable
{
	private readonly string? _old = SavedSelections.PathOverride;
	private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-selections-" + Guid.NewGuid().ToString("N")[..8]);

	public ParitySelectionTests()
	{
		Directory.CreateDirectory(_dir);
		SavedSelections.PathOverride = Path.Combine(_dir, "selections.json");
	}

	public void Dispose()
	{
		SavedSelections.PathOverride = _old;
		Directory.Delete(_dir, true);
	}

	private static readonly int Beech = StableHash.Of("Beech1"), Wall = StableHash.Of("wood_wall_half");

	private static WorldScene.Thing T(int id, int prefab, float x, float y, float z, bool gone = false) => new(id, prefab, new Vector3(x, y, z), Vector3.Zero, 1, false) { Gone = gone };

	private static SavedSelections.Saved S(params SavedSelections.Item[] items) => new("s", items.ToList());

	private static SavedSelections.Item I(int prefab, float x, float y, float z) => new(prefab, x, y, z);

	// ---- The store.

	[Fact]
	public void SelectionsAreKeptPerWorldAndReplacedByName()
	{
		Assert.Empty(SavedSelections.For("A"));
		Assert.True(SavedSelections.Keep("A", "trees", new[] { new SavedSelections.Item(Beech, 1.23456f, 2, 3) }));
		Assert.True(SavedSelections.Keep("A", "walls", new[] { new SavedSelections.Item(Wall, 4, 5, 6) }));
		Assert.True(SavedSelections.Keep("B", "trees", Array.Empty<SavedSelections.Item>()));
		Assert.Equal(new[] { "trees", "walls" }, SavedSelections.For("A").Select(s => s.Name));
		// Rounded to the millimetre.
		Assert.Equal(1.235f, SavedSelections.For("A")[0].Items[0].X, 4);
		// The same name again: replaced, and now the last.
		SavedSelections.Keep("A", "trees", new[] { new SavedSelections.Item(Beech, 9, 9, 9), new SavedSelections.Item(Beech, 8, 8, 8) });
		var a = SavedSelections.For("A");
		Assert.Equal(new[] { "walls", "trees" }, a.Select(s => s.Name));
		Assert.Equal(2, a[1].Items.Count);
		Assert.Single(SavedSelections.For("B"));
		// Forgetting: only that world's, and the world goes when it has none left.
		Assert.True(SavedSelections.Forget("B", "trees"));
		Assert.Empty(SavedSelections.For("B"));
		Assert.DoesNotContain("\"B\"", File.ReadAllText(SavedSelections.PathOverride!));
		Assert.True(SavedSelections.Forget("A", "no such selection"));
		Assert.True(SavedSelections.Forget("nowhere", "trees"));
		Assert.Equal(2, SavedSelections.For("A").Count);
	}

	[Fact]
	public void ABrokenOrUnwritableFileMeansNoSelections()
	{
		File.WriteAllText(SavedSelections.PathOverride!, "{ not json");
		Assert.Empty(SavedSelections.For("A"));
		File.WriteAllText(SavedSelections.PathOverride!, "{\"A\": null, \"B\": [null, {\"Name\": \"x\", \"Items\": null}, {\"Name\": \"ok\", \"Items\": []}]}");
		Assert.Empty(SavedSelections.For("A"));
		Assert.Equal("ok", Assert.Single(SavedSelections.For("B")).Name);
		File.WriteAllText(SavedSelections.PathOverride!, "[1, 2]");
		Assert.Empty(SavedSelections.For("A"));
		// A folder in the file's place: nothing written, said so.
		File.Delete(SavedSelections.PathOverride!);
		Directory.CreateDirectory(SavedSelections.PathOverride!);
		Assert.False(SavedSelections.Keep("A", "x", new[] { new SavedSelections.Item(Beech, 0, 0, 0) }));
		Assert.Empty(SavedSelections.For("A"));
	}

	[Fact]
	public void FindingMatchesKindAndPlaceWithinTolerances()
	{
		var things = new List<WorldScene.Thing>
		{
			T(1, Beech, 10, 30, 10),
			T(2, Beech, 10.04f, 30.19f, 9.96f),  // within 5 cm across and 20 cm up of the second item
			T(3, Wall, 20, 30, 20),
			T(4, Beech, 50, 30, 50, gone: true),
			T(5, Beech, 60.06f, 30, 60),         // 6 cm off: not it
		};
		var (ids, missing) = SavedSelections.Find(S(
			I(Beech, 10, 30, 10), I(Beech, 10, 30, 10), I(Wall, 20, 30.25f, 20), I(Beech, 50, 30, 50), I(Beech, 60, 30, 60), I(Wall, 10, 30, 10)), things);
		// Two items at the same place take two different objects; the wall is 25 cm too high, the
		// deleted one and the one 6 cm away are not found, nor a wall where a tree stands.
		Assert.Equal(new[] { 0, 1 }, ids);
		Assert.Equal(4, missing);
		Assert.Empty(SavedSelections.Find(S(), things).Ids);
		Assert.Equal(0, SavedSelections.Find(S(), things).Missing);
		Assert.Equal(1, SavedSelections.Find(S(I(Beech, 0, 0, 0)), Array.Empty<WorldScene.Thing>()).Missing);
	}

	// ---- The Select panel.

	private static (MainWindow W, EditSession S, List<string> Messages) Window(params WorldScene.Thing[] things)
	{
		var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
		w.Show();
		var s = EditTests.Flat(2, things);
		w.View.Show(s.Scene, null);
		w.Edit(s);
		w.Tools.ChooseSelect();
		var messages = new List<string>();
		w.SelectPanel.Message += messages.Add;
		w.SelectPanel.WorldName = () => "World one";
		w.SelectPanel.AskName = n => Task.FromResult<string?>(n);
		w.SelectPanel.Confirm = _ => Task.FromResult(true);
		return (w, s, messages);
	}

	private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

	[AvaloniaFact]
	public async Task KeepSelectAddAndForgetASelection()
	{
		var (w, s, messages) = Window(T(1, Beech, 40, 30, 40), T(2, Beech, 44, 30, 40), T(3, Wall, 48, 30, 40));
		var p = w.SelectPanel;
		p.FillSaved();
		Assert.Equal("No saved selections", p.SavedBox.PlaceholderText);
		Assert.Equal(0, p.SavedBox.ItemCount);
		// Nothing selected: nothing to keep.
		await p.Keep();
		Assert.Equal("Select something first.", messages[^1]);
		// Kept under the name offered (Selection 1), then under a name typed.
		w.View.Select(new[] { 0, 1 });
		await p.Keep();
		Assert.Equal("Kept the selection “Selection 1” (2 object(s)). It can be picked again here, also after saving the world.", messages[^1]);
		Assert.Equal("Selection 1 (2)", p.SavedBox.SelectedItem);
		Assert.Equal("Saved selections…", p.SavedBox.PlaceholderText);
		w.View.Select(new[] { 2 });
		p.AskName = n => Task.FromResult<string?>("  The wall  ");
		await p.Keep();
		Assert.Equal(new[] { "Selection 1 (2)", "The wall (1)" }, p.SavedBox.Items.Cast<string>());
		Assert.Equal(1, p.SavedBox.SelectedIndex);
		// Select it again: only the wall; Add: the trees too.
		w.View.Select(Array.Empty<int>());
		p.UseSaved(false);
		Assert.Equal(new[] { 2 }, w.View.Selected);
		Assert.Equal("Selected 1 object(s) of “The wall”.", messages[^1]);
		p.SavedBox.SelectedIndex = 0;
		p.UseSaved(true);
		Assert.Equal(new[] { 0, 1, 2 }, w.View.Selected.OrderBy(i => i));
		// One tree deleted since: said, the rest selected.
		s.Delete(new[] { 1 });
		p.UseSaved(false);
		Assert.Equal(new[] { 0 }, w.View.Selected);
		Assert.Equal("Selected 1 object(s) of “Selection 1”; 1 are not in this area or no longer where they were.", messages[^1]);
		// Forget, after asking (no: kept).
		p.Confirm = _ => Task.FromResult(false);
		await p.Forget();
		Assert.Equal(2, SavedSelections.For("World one").Count);
		string? asked = null;
		p.Confirm = t => { asked = t; return Task.FromResult(true); };
		await p.Forget();
		Assert.Equal("Forget the selection “Selection 1”?", asked);
		Assert.Equal(new[] { "The wall (1)" }, p.SavedBox.Items.Cast<string>());
		Assert.Equal(-1, p.SavedBox.SelectedIndex);
		// Nothing chosen: the buttons do nothing.
		int count = messages.Count;
		p.UseSaved(false);
		await p.Forget();
		Assert.Equal(count, messages.Count);
	}

	[AvaloniaFact]
	public async Task NamingCancelledOrBlankKeepsNothing()
	{
		var (w, _, messages) = Window(T(1, Beech, 40, 30, 40));
		var p = w.SelectPanel;
		w.View.Select(new[] { 0 });
		p.AskName = _ => Task.FromResult<string?>(null);
		await p.Keep();
		p.AskName = _ => Task.FromResult<string?>("   ");
		await p.Keep();
		Assert.Empty(SavedSelections.For("World one"));
		Assert.Empty(messages);
	}

	[AvaloniaFact]
	public async Task SelectionsBelongToTheirWorld()
	{
		var (w, _, messages) = Window(T(1, Beech, 40, 30, 40));
		var p = w.SelectPanel;
		w.View.Select(new[] { 0 });
		await p.Keep();
		p.WorldName = () => "Another world";
		p.FillSaved();
		Assert.Equal(0, p.SavedBox.ItemCount);
		Assert.False(p.LoadButton.IsEffectivelyVisible);
		// No world (nothing open): nothing kept.
		p.WorldName = () => null;
		await p.Keep();
		Assert.Equal("Select something first.", messages[^1]);
		p.FillSaved();
		Assert.Equal(0, p.SavedBox.ItemCount);
	}

	[AvaloniaFact]
	public async Task TheButtonsDoWhatTheySay()
	{
		var (w, _, _) = Window(T(1, Beech, 40, 30, 40), T(2, Wall, 44, 30, 40));
		var p = w.SelectPanel;
		w.View.Select(new[] { 1 });
		Click(p.KeepButton);
		await Task.Delay(20);
		Assert.Single(SavedSelections.For("World one"));
		// The row of buttons shows once one is chosen.
		Assert.True(p.LoadButton.IsVisible || p.LoadButton.Parent is Control { IsVisible: true });
		w.View.Select(new[] { 0 });
		Click(p.LoadButton);
		Assert.Equal(new[] { 1 }, w.View.Selected);
		w.View.Select(new[] { 0 });
		Click(p.AddSavedButton);
		Assert.Equal(new[] { 0, 1 }, w.View.Selected.OrderBy(i => i));
		Click(p.ForgetButton);
		await Task.Delay(20);
		Assert.Empty(SavedSelections.For("World one"));
		Assert.False(((Control)p.LoadButton.Parent!).IsVisible);
	}

	[AvaloniaFact]
	public async Task ASelectionIsFoundAgainAfterTheIdsChanged()
	{
		// Kept in one area; the "saved world" has the same objects under new ids, and moved ones.
		var (w, _, messages) = Window(T(1, Beech, 40, 30, 40), T(2, Wall, 44, 31, 40));
		w.View.Select(new[] { 0, 1 });
		await w.SelectPanel.Keep();
		var (w2, _, messages2) = Window(T(900, Wall, 44, 31.1f, 40.02f), T(901, Beech, 41, 30, 40));
		w2.SelectPanel.FillSaved();
		w2.SelectPanel.SavedBox.SelectedIndex = 0;
		w2.SelectPanel.UseSaved(false);
		Assert.Equal(new[] { 0 }, w2.View.Selected);
		Assert.Equal("Selected 1 object(s) of “Selection 1”; 1 are not in this area or no longer where they were.", messages2[^1]);
		// Selecting switches to the Select tool.
		w2.Tools.ChooseMode(ToolMode.Measure);
		w2.SelectPanel.UseSaved(false);
		Assert.Equal(ToolMode.Select, w2.Tools.Mode);
	}

	// ---- The eyedropper.

	private static Avalonia.Point ScreenOf(MainWindow w, Vector3 viewPos)
	{
		var q = Vector4.Transform(new Vector4(viewPos, 1), w.View.ViewProj);
		var size = w.View.Bounds.Size;
		return new Avalonia.Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
	}

	// A click in the window, once its layout is done (a tool chosen changes it).
	private static void Press(MainWindow w, Avalonia.Point at, MouseButton button)
	{
		Avalonia.Threading.Dispatcher.UIThread.RunJobs();
		w.MouseMove(at);
		w.MouseDown(at, button);
		w.MouseUp(at, button);
	}

	private static (MainWindow W, EditSession S, Avalonia.Point Wall, Avalonia.Point Empty) PickWindow()
	{
		var w = new MainWindow(load: false) { Width = 1000, Height = 1000 };
		w.Show();
		var s = EditTests.Flat(2, new WorldScene.Thing(11, Wall, new Vector3(40, 30, 40), Vector3.Zero, 0, true));
		w.View.Show(s.Scene, null);
		w.Edit(s);
		var c = new Vector3(40 - s.Scene.Cx, 30, -(40 - s.Scene.Cz));
		// The wall's box, as drawing would know it (no models here): 2 m wide, 2 m high.
		w.View.SetBoxes(new[] { (0, c + new Vector3(-1, 0, -0.2f), c + new Vector3(1, 2, 0.2f)) });
		w.View.SetCamera(c + new Vector3(0, 6, 8), c, 1);
		// Laid out and drawn once: hit tests use the last frame (with one app for the whole run, the
		// window is not always drawn yet here).
		w.UpdateLayout();
		Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
		Avalonia.Threading.Dispatcher.UIThread.RunJobs();
		return (w, s, ScreenOf(w, c + new Vector3(0, 1, 0.2f)), ScreenOf(w, c + new Vector3(0, 0, 3)));
	}

	[AvaloniaFact]
	public void TheEyedropperGivesTheClickedKindAndSwallowsTheClick()
	{
		var (w, _, wall, _) = PickWindow();
		w.Tools.ChooseSelect();
		int? picked = null;
		w.PickKind("Replace", p => picked = p);
		Assert.Equal("Click an object to pick its kind for Replace. Esc cancels.", w.MessageText.Text);
		Press(w, wall, MouseButton.Left);
		Assert.Equal(Wall, picked);
		// The click was the eyedropper's: the Select tool did not select the wall.
		Assert.Empty(w.View.Selected);
		Assert.Null(w.View.PickObjectOnce);
		// The next click is the tool's again.
		Press(w, wall, MouseButton.Left);
		Assert.Equal(new[] { 0 }, w.View.Selected);
	}

	[AvaloniaFact]
	public void AClickOnNothingOrEscCancelsTheEyedropper()
	{
		var (w, _, _, empty) = PickWindow();
		w.Tools.ChooseMode(ToolMode.Area);
		w.UpdateLayout();
		Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
		Avalonia.Threading.Dispatcher.UIThread.RunJobs();
		var hit = Avalonia.Input.InputExtensions.InputHitTest(w, empty);
		Assert.True(hit == w.Surface, $"{empty} hit {hit?.GetType().Name} {(hit as Control)?.Parent?.GetType().Name}");
		bool called = false;
		w.PickKind("Replace", _ => called = true);
		Press(w, empty, MouseButton.Left);
		Assert.False(called);
		Assert.Equal("Nothing picked: click right on an object (only things that are shown can be picked).", w.MessageText.Text);
		// The Area tool did not get that click either.
		Assert.Empty(w.View.Area.Points);
		w.PickKind("Replace", _ => called = true);
		w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
		Assert.Equal("Picking cancelled.", w.MessageText.Text);
		Assert.Null(w.View.PickObjectOnce);
		Assert.False(called);
		// A right click does not pick: it is the camera's.
		w.PickKind("Replace", _ => called = true);
		Press(w, empty, MouseButton.Right);
		Assert.NotNull(w.View.PickObjectOnce);
	}

	[AvaloniaFact]
	public void TheAreaReplacePickButtonFillsTheWithList()
	{
		var (w, s, wall, _) = PickWindow();
		var a = w.AreaPanel;
		var messages = new List<string>();
		a.Message += messages.Add;
		w.Tools.ChooseMode(ToolMode.Area);
		a.Choose(AreaPanel.Act.Replace);
		a.Refresh();
		Assert.True(a.ToPickButton.IsEffectivelyVisible);
		Click(a.ToPickButton);
		Assert.NotNull(w.View.PickObjectOnce);
		Press(w, wall, MouseButton.Left);
		string name = w.NameOfPrefab(Wall)!;
		// The wall is a kind the game makes: it is chosen.
		Assert.Contains(Wall, s.Scene.World.Creatable);
		Assert.Equal(name, a.ToBox.SelectedItem);
		Assert.Equal($"Replace with {name}.", messages[^1]);
	}

	[AvaloniaFact]
	public void UsePickedTakesOnlyKindsTheGameCanMake()
	{
		var (w, s, _, _) = PickWindow();
		var a = w.AreaPanel;
		var messages = new List<string>();
		a.Message += messages.Add;
		a.Refresh();
		var creatable = s.Scene.World.Creatable.Where(p => w.NameOfPrefab(p) != null).OrderBy(p => w.NameOfPrefab(p), StringComparer.OrdinalIgnoreCase).ToList();
		Assert.NotEmpty(creatable);
		a.UsePicked(creatable[^1]);
		Assert.Equal(creatable.Count - 1, a.ToBox.SelectedIndex);
		Assert.Equal($"Replace with {w.NameOfPrefab(creatable[^1])}.", messages[^1]);
		a.UsePicked(12345);
		Assert.Equal(creatable.Count - 1, a.ToBox.SelectedIndex);
		Assert.Equal("12345 cannot be placed: the game has no such kind to copy.", messages[^1]);
	}

	[AvaloniaFact]
	public void TheSelectReplacePickButtonFillsReplaceWith()
	{
		var (w, _, wall, _) = PickWindow();
		var p = w.SelectPanel;
		var messages = new List<string>();
		p.Message += messages.Add;
		w.Tools.ChooseSelect();
		Click(p.ReplacePickButton);
		Press(w, wall, MouseButton.Left);
		int i = p.ReplaceKinds.IndexOf(Wall);
		string name = w.NameOfPrefab(Wall)!;
		Assert.True(i >= 0);
		Assert.Equal(i, p.ReplaceBox.SelectedIndex);
		Assert.Equal($"Replace with {name}.", messages[^1]);
		// A kind not in the list.
		p.UsePicked(777);
		Assert.Equal("777 cannot be placed: the game has no such kind to copy.", messages[^1]);
		p.NameOf = null;
		p.UsePicked(778);
		Assert.Equal("778 cannot be placed: the game has no such kind to copy.", messages[^1]);
	}
}

[CollectionDefinition("Saved selections", DisableParallelization = true)]
public sealed class SavedSelectionsGroup
{
}
