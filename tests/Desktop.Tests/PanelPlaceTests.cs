using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Place panel through its controls on the test world (zone 0, 0): what its memory brings back, the
// kind chooser (search, ticks, favourite stars, long groups cut), the favourites and recent chips, the
// weight sliders and ✕, presets (built in, saved, deleted), Pick from world, and its hints and notes.
[Collection("World files")]
public class PanelPlaceTests
{
	private sealed class Run : IDisposable
	{
		public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
		public string Dir { get; } = EditTests.CopyFixture();
		public WorldScene Scene { get; }
		public PlacePanel P => W.PlacePanel;
		public PlaceTool T => W.PlaceTool;
		public string? Said;

		public Run()
		{
			Scene = WorldScene.Load(Dir, 0, 0, 1);
			W.Show();
			W.View.Show(Scene, null);
			W.Edit(Scene.Session!);
			W.Tools.ChooseMode(ToolMode.Place);
			P.Message += t => Said = t;
			// A clean memory of its own (the tests' place.json is shared).
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

	private static string Texts(Control c) => string.Join(" | ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

	private static List<CheckBox> Ticks(Run r) => r.P.List.GetLogicalDescendants().OfType<CheckBox>().ToList();

	private static void Choose(Run r, params string[] names)
	{
		r.T.Chosen.Clear();
		r.T.Chosen.AddRange(names);
		// What ticking a kind does in the panel.
		typeof(PlacePanel).GetMethod("Chosen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(r.P, new object[] { true });
		Dispatcher.UIThread.RunJobs();
	}

	[AvaloniaFact]
	public void TheMemoryBringsBackTheKindsAndWeights()
	{
		using var r = new Run();
		var memory = new PlaceMemory { Chosen = { "Oak1", "Beech1" }, Weights = { ["Oak1"] = 7 } };
		_ = new PlacePanel(r.W.PlaceInput, () => r.Scene, p => PrefabCatalog.DisplayName(p), memory);
		Assert.Equal(new[] { "Oak1", "Beech1" }, r.T.Chosen);
		Assert.Equal(7, r.T.WeightOf("Oak1"));
	}

	[AvaloniaFact]
	public void TheChooserSearchesTicksAndStarsKinds()
	{
		using var r = new Run();
		Choose(r);
		Assert.False(r.P.Chooser.IsVisible);
		Click(r.P.KindsButton);
		Assert.True(r.P.Chooser.IsVisible);
		Assert.Equal("Done", r.P.KindsButton.Content);
		// Grouped by kind, long groups cut while not searching.
		Assert.Contains("TREES", Texts(r.P.List));
		int all = Ticks(r).Count;
		r.P.Search.Text = "beech";
		Assert.InRange(Ticks(r).Count, 1, all - 1);
		Assert.All(Ticks(r), b => Assert.Contains("beech", ((string)b.Content!).ToLowerInvariant()));
		r.P.Search.Text = "no kind is called this";
		Assert.Empty(Ticks(r));
		r.P.Search.Text = "Beech1";
		var tick = Ticks(r).First(b => (string)b.Content! == "Beech1");
		tick.IsChecked = true;
		Assert.Contains("Beech1", r.T.Chosen);
		Assert.Contains("Beech1", r.P.Memory.Chosen);
		tick.IsChecked = false;
		Assert.DoesNotContain("Beech1", r.T.Chosen);
		// The star makes it a favourite: a chip appears; clicking the chip ticks the kind.
		var star = r.P.List.GetLogicalDescendants().OfType<Button>().First(b => (string)b.Content! == "☆");
		Click(star);
		Assert.Contains("Beech1", r.P.Memory.Favourites);
		var chip = r.P.Favourites.Children.OfType<ToggleButton>().Single();
		Assert.Equal("Beech1", chip.Content);
		chip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Dispatcher.UIThread.RunJobs();
		Assert.Contains("Beech1", r.T.Chosen);
		// Starred again: no longer a favourite.
		Click(r.P.List.GetLogicalDescendants().OfType<Button>().First(b => (string)b.Content! == "★"));
		Assert.DoesNotContain("Beech1", r.P.Memory.Favourites);
		Click(r.P.KindsButton);
		Assert.False(r.P.Chooser.IsVisible);
		Assert.Equal("+ Add kinds", r.P.KindsButton.Content);
	}

	[AvaloniaFact]
	public void AGroupOfMoreThanSixtyKindsIsCutUntilSearched()
	{
		using var r = new Run();
		Click(r.P.KindsButton);
		r.P.Search.Text = "";
		string texts = Texts(r.P.List);
		if (r.P.Creatable().GroupBy(c => c.Kind).Any(g => g.Count() > 60))
		{
			Assert.Contains("more: search for them.", texts);
		}
	}

	[AvaloniaFact]
	public void RecentKindsBecomeChips()
	{
		using var r = new Run();
		Choose(r, "Beech1");
		// Placing notes the kinds as recent (the input's Placed event); the chip ticks the kind.
		var raise = typeof(PlaceInput).GetField("Placed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(r.W.PlaceInput) as Action<IReadOnlyList<string>>;
		raise!(new[] { "Oak1" });
		var chip = r.P.Recent.Children.OfType<ToggleButton>().Single(c => (string)c.Content! == "Oak1");
		chip.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Dispatcher.UIThread.RunJobs();
		Assert.Contains("Oak1", r.T.Chosen);
	}

	[AvaloniaFact]
	public void WeightsAreSetWithSlidersAndKindsTakenOutWithTheCross()
	{
		using var r = new Run();
		Choose(r, "Beech1", "Oak1");
		var sliders = r.P.Mix.GetLogicalDescendants().OfType<Slider>().ToList();
		Assert.Equal(2, sliders.Count);
		sliders[1].Value = 9;
		Assert.Equal(9, r.T.WeightOf("Oak1"));
		Assert.Equal(9, r.P.Memory.Weights["Oak1"]);
		Assert.Contains("%", Texts(r.P.Mix));
		Click(r.P.Mix.GetLogicalDescendants().OfType<Button>().First(b => (string)b.Content! == "✕"));
		Assert.Equal(new[] { "Oak1" }, r.T.Chosen);
		// One kind: no weight to set.
		Assert.Empty(r.P.Mix.GetLogicalDescendants().OfType<Slider>());
	}

	[AvaloniaFact]
	public async Task PresetsAreLoadedSavedAndDeleted()
	{
		using var r = new Run();
		// A built-in one: its kinds and settings.
		r.P.PresetBox.SelectedIndex = 1;
		Assert.Equal(PlaceTool.BuiltIn[0].Kinds.Keys.Where(k => r.P.Creatable().Any(c => c.Name == k)).Order(), r.T.Chosen.Order());
		Assert.False(r.P.DeletePresetButton.IsEnabled);
		// Saving needs kinds, and a name.
		Choose(r);
		Click(r.P.SavePresetButton);
		Assert.Equal("Tick one or more kinds first.", r.Said);
		Choose(r, "Beech1");
		r.P.AskName = () => Task.FromResult<string?>(null);
		Click(r.P.SavePresetButton);
		Assert.Empty(r.P.Memory.Presets);
		r.P.AskName = () => Task.FromResult<string?>("  My woods ");
		Click(r.P.SavePresetButton);
		await LiveTests.Until(() => r.Said == "Saved the preset My woods.");
		Assert.Equal("Yours: My woods", r.P.PresetBox.SelectedItem);
		Assert.True(r.P.DeletePresetButton.IsEnabled);
		// Saved again under the same name: replaced, not doubled.
		Click(r.P.SavePresetButton);
		await LiveTests.Until(() => r.P.Memory.Presets.Count == 1);
		// Delete asks first.
		r.P.Confirm = _ => Task.FromResult(false);
		Click(r.P.DeletePresetButton);
		Assert.Single(r.P.Memory.Presets);
		r.P.Confirm = _ => Task.FromResult(true);
		Click(r.P.DeletePresetButton);
		await LiveTests.Until(() => r.P.Memory.Presets.Count == 0);
		Assert.Equal(0, r.P.PresetBox.SelectedIndex);
	}

	[AvaloniaFact]
	public void PickFromWorldTakesAKindTheWorldCanMake()
	{
		using var r = new Run();
		Click(r.P.PickButton);
		Assert.Equal("Click an object in the view to place its kind.", r.Said);
		Assert.NotNull(r.W.PlaceInput.PickOnce);
		r.W.PlaceInput.PickOnce!("NotAThing_xyz");
		Assert.Equal("NotAThing_xyz cannot be placed: the game has no such kind to copy.", r.Said);
		Click(r.P.PickButton);
		string kind = r.P.Creatable().First().Name;
		r.W.PlaceInput.PickOnce!(kind);
		Assert.Equal($"Placing {kind}.", r.Said);
		Assert.Equal(new[] { kind }, r.T.Chosen);
	}

	[AvaloniaFact]
	public void TheHintsAndNotesFollowTheShapeAndKinds()
	{
		using var r = new Run();
		Click(r.P.ModeButtons[PlaceTool.Modes.Line]);
		Click(r.P.ShapeButtons[PlaceTool.LineShapes.Circle]);
		Assert.Contains("Press at the centre and drag out", Texts(r.P.Card));
		Click(r.P.ShapeButtons[PlaceTool.LineShapes.Rect]);
		Assert.Contains("Press at one corner and drag", Texts(r.P.Card));
		Choose(r);
		Assert.Equal("Tick at least one kind to place.", r.P.Info.Text);
		Assert.Equal("Nothing to place yet: + Add kinds, or a preset.", r.P.Note.Text);
		Choose(r, "sapling_turnip");
		Assert.Contains("only grows on cultivated ground", r.P.Note.Text);
	}
}
