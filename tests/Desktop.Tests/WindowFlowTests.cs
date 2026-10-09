using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The window's workflows on a copy of the test world: saving from the map and from the editor (with
// the confirmation answered no, then yes), Discard, leaving and quitting with unsaved changes, a world
// that cannot be opened, a game that stops answering, and the shortcuts and their messages.
[Collection("World files")]
public class WindowFlowTests
{
	private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", new string[0]);

	private sealed class Run : IDisposable
	{
		public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
		public string Dir { get; } = EditTests.CopyFixture();
		public List<string> Told { get; } = new();
		public List<string> Asked { get; } = new();
		public bool Answer { get; set; }

		public Run()
		{
			W.Show();
			W.Tell = t => { Told.Add(t); return Task.CompletedTask; };
			W.ConfirmSave = t => { Asked.Add(t); return Task.FromResult(Answer); };
			W.Ask = (title, _, _, _) => { Asked.Add(title); return Task.FromResult(Answer); };
		}

		public async Task Open()
		{
			await W.OpenWorld(() => Task.Run(() => WorldSession.Open(Dir)), "Opening…");
			Assert.NotNull(W.World);
		}

		// An area with a raised spot: one zone of ground pending.
		public async Task Edited()
		{
			await Open();
			await W.EditArea(0, 0, 1);
			W.Session!.Shape(32, 32, Two, 3, 0, "raise");
			Assert.Equal(1, W.World!.Pending.Zones);
		}

		public int SaveNumber => WorldSave.Load(Dir).SaveNumber;

		public void Dispose()
		{
			try
			{
				Directory.Delete(Path.GetDirectoryName(Dir)!, true);
			}
			catch (Exception)
			{
			}
		}
	}

	[AvaloniaFact]
	public async Task SavingFromTheMapAsksFirstThenWritesTheWorld()
	{
		using var r = new Run();
		await r.Edited();
		r.W.ShowMap();
		int before = r.SaveNumber;
		r.Answer = false;
		await r.W.SaveWorld();
		Assert.Contains("Write 1 zone(s) of ground into the world files?", r.Asked.Single());
		Assert.Equal(before, r.SaveNumber);
		Assert.Equal(1, r.W.World!.Pending.Zones);
		r.Answer = true;
		await r.W.SaveWorld();
		Assert.Equal(before + 1, r.SaveNumber);
		Assert.Equal((0, 0, 0, 0), r.W.World.Pending);
		Assert.StartsWith("Saved 1 zone(s)", r.Told.Single());
		Assert.Equal("Everything is saved.", r.W.MapPage!.Pending.Text);
	}

	[AvaloniaFact]
	public async Task SavingFromTheEditorWithCtrlS()
	{
		using var r = new Run();
		await r.Open();
		await r.W.EditArea(0, 0, 1);
		// Nothing to save yet.
		await r.W.Save();
		Assert.Equal("There are no unsaved changes.", r.Told.Single());
		r.W.Session!.Shape(32, 32, Two, 3, 0, "raise");
		r.Answer = true;
		int before = r.SaveNumber;
		r.W.KeyPress(Key.S, RawInputModifiers.Control, PhysicalKey.S, "s");
		await LiveTests.Until(() => r.Told.Count == 2);
		Assert.StartsWith("Saved", r.Told[1]);
		Assert.Equal(before + 1, r.SaveNumber);
		Assert.Equal("All saved", r.W.PendingText.Text);
	}

	[AvaloniaFact]
	public async Task DiscardAsksThenReadsTheWorldAgain()
	{
		using var r = new Run();
		await r.Edited();
		r.W.ShowMap();
		r.Answer = false;
		await r.W.DiscardWorld();
		Assert.Equal("Discard", r.Asked.Single());
		Assert.Equal(1, r.W.World!.Pending.Zones);
		r.Answer = true;
		await r.W.DiscardWorld();
		Assert.Equal((0, 0, 0, 0), r.W.World.Pending);
	}

	// The world folder gone while the map is open: Discard and Save say so, the window stays (it
	// closed, losing every change) and is not left busy.
	[AvaloniaFact]
	public async Task AWorldFolderThatIsGoneIsSaidSo()
	{
		using var r = new Run();
		await r.Edited();
		r.W.ShowMap();
		r.Answer = true;
		Directory.Delete(r.Dir, recursive: true);
		await r.W.DiscardWorld();
		Assert.StartsWith("Could not read the world again: ", r.W.MessageText.Text);
		await r.W.SaveWorld();
		Assert.StartsWith("Could not save", r.Told.Last());
		Assert.Equal(1, r.W.World!.Pending.Zones);
		Assert.False(r.W.IsBusy);
		await r.W.LeaveWorld();
		Assert.Null(r.W.World);
	}

	// A blueprint that cannot be read for the Workshop: the world stays open, with its changes (it was
	// left first, and the editor kept showing the area of a world closed under it).
	[AvaloniaFact]
	public async Task AWorkshopBlueprintThatCannotBeReadKeepsTheWorld()
	{
		using var r = new Run();
		await r.Edited();
		r.Answer = true;
		var world = r.W.World;
		await r.W.OpenWorkshop(Path.Combine(r.Dir, "no-such.blueprint"));
		Assert.StartsWith("Could not open that blueprint: ", r.W.MessageText.Text);
		Assert.Same(world, r.W.World);
		Assert.Equal(1, r.W.World!.Pending.Zones);
		await r.W.Save();
		Assert.StartsWith("Saved", r.Told.Last());
	}

	[AvaloniaFact]
	public async Task LeavingWithUnsavedChangesAsksFirst()
	{
		using var r = new Run();
		await r.Edited();
		r.W.ShowMap();
		r.Answer = false;
		await r.W.LeaveWorld();
		Assert.NotNull(r.W.World);
		r.Answer = true;
		await r.W.LeaveWorld();
		Assert.Null(r.W.World);
		Assert.NotNull(r.W.StartPage);
		Assert.Equal(new[] { "Leave the world", "Leave the world" }, r.Asked);
	}

	[AvaloniaFact]
	public async Task QuittingWithUnsavedChangesAsksFirst()
	{
		using var r = new Run();
		await r.Edited();
		bool closed = false;
		r.W.Closed += (_, _) => closed = true;
		r.Answer = false;
		r.W.Close();
		await LiveTests.Until(() => r.Asked.Count == 1);
		Assert.False(closed);
		r.Answer = true;
		r.W.Close();
		await LiveTests.Until(() => closed);
		Assert.Equal("Unsaved changes", r.Asked[1]);
	}

	[AvaloniaFact]
	public async Task AWorldThatCannotBeOpenedGoesBackToTheStartPageWithTheReason()
	{
		using var r = new Run();
		await r.W.OpenWorld(() => Task.FromException<WorldSession>(new InvalidDataException("not a world")), "Opening…");
		Assert.Null(r.W.World);
		Assert.NotNull(r.W.StartPage);
		Assert.Contains("Could not open the world: not a world", string.Join(" ", Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(r.W.StartPage!.View).OfType<Avalonia.Controls.TextBlock>().Select(t => t.Text)));
	}

	[AvaloniaFact]
	public async Task AGameThatStopsAnsweringOnReloadIsReported()
	{
		var game = new FakeGame();
		using var r = new Run();
		await r.W.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label()), "Connecting…");
		r.W.MapPage!.Stop();
		game.Dispose();
		await r.W.ReloadWorld();
		Assert.StartsWith("Could not reach the game", r.W.MessageText.Text);
		Assert.NotNull(r.W.World);
	}

	[AvaloniaFact]
	public async Task ShortcutsAndWhatTheySayWithNothingToWorkOn()
	{
		using var r = new Run();
		await r.Open();
		await r.W.EditArea(0, 0, 1);
		var w = r.W;
		// The right-hand panels.
		w.KeyPress(Key.L, RawInputModifiers.None, PhysicalKey.L, "l");
		Assert.True(w.History.Card.IsVisible);
		w.KeyPress(Key.V, RawInputModifiers.None, PhysicalKey.V, "v");
		Assert.False(w.History.Card.IsVisible);
		w.KeyPress(Key.F3, RawInputModifiers.None, PhysicalKey.F3, null);
		Assert.True(w.HelpCard.IsVisible);
		w.KeyPress(Key.F3, RawInputModifiers.None, PhysicalKey.F3, null);
		Assert.False(w.HelpCard.IsVisible);
		// Undo and redo.
		w.Session!.Shape(32, 32, Two, 3, 0, "raise");
		w.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.Z, "z");
		Assert.False(w.Session.CanUndo);
		w.KeyPress(Key.Y, RawInputModifiers.Control, PhysicalKey.Y, "y");
		Assert.True(w.Session.CanUndo);
		// Copy and paste with nothing to work on.
		w.Tools.ChooseMode(ToolMode.Area);
		w.Copy();
		Assert.Equal("Select an area first.", w.MessageText.Text);
		w.Tools.ChooseSelect();
		w.Copy();
		Assert.Equal("Select objects to copy first.", w.MessageText.Text);
		w.StartPaste();
		Assert.Equal("Copy an area first (Area tool, Ctrl+C).", w.MessageText.Text);
		// A path needs two points; a shape a formula that works.
		w.ApplyPath();
		Assert.Equal("Draw a line with at least two points first.", w.MessageText.Text);
		w.ShapePanel.FormulaBox.Text = "x +* 2";
		w.PutShape(32, 32);
		Assert.Equal("Fix the formula first.", w.MessageText.Text);
		// The number pad picks tools like the number row.
		w.KeyPress(Key.NumPad2, RawInputModifiers.None, PhysicalKey.NumPad2, "2");
		Assert.Equal(BrushTool.Lower, w.Tools.Tool);
	}

	[AvaloniaFact]
	public async Task ABuilderIdMustBeAWholeNumber()
	{
		using var r = new Run();
		await r.Open();
		await r.W.EditArea(0, 0, 1);
		var w = r.W;
		w.AskPlayerId = () => Task.FromResult<string?>("not a number");
		int other = w.BuilderIds.IndexOf(-1);
		Assert.True(other >= 0);
		long before = WorldSave.Builder;
		w.BuilderBox.SelectedIndex = other;
		await LiveTests.Until(() => w.MessageText.Text == "A player id is a whole number other than 0.");
		Assert.Equal(before, WorldSave.Builder);
		// A good one is used and remembered for the world.
		w.AskPlayerId = () => Task.FromResult<string?>("987654321");
		w.BuilderBox.SelectedIndex = -1;
		w.BuilderBox.SelectedIndex = w.BuilderIds.IndexOf(-1);
		await LiveTests.Until(() => WorldSave.Builder == 987654321);
		Assert.Equal(987654321, w.PlacePanel.Memory.Builders[w.World!.World.Name]);
	}
}
