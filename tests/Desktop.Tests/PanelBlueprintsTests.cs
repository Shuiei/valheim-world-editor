using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The Blueprints panel through its list: Homestead's blueprints (its folder: a temporary one here) and
// the editor's older ones, empty, searched, each row's buttons (Paste, Edit, .vbuild, Delete; To Homestead
// for older ones), thumbnails that are not pictures, Import file…, what it says for files that are
// missing, empty or not blueprints, a name already taken, kinds a world cannot make, and whether
// Homestead is installed (a warning when not). Never the user's own folders.
public class PanelBlueprintsTests
{
	internal sealed class Run : IDisposable
	{
		public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
		public string Dir { get; } = Path.Combine(Path.GetTempPath(), "vwe-bp-ui-" + Guid.NewGuid().ToString("N")[..8]);
		public string Homestead => Path.Combine(Dir, "homestead");
		public BlueprintsPanel B => W.Blueprints;
		public string? Said;
		public bool Installed { get; set; } = true;

		public Run(EditSession? s = null)
		{
			W.Show();
			s ??= EditTests.Flat(2);
			W.View.Show(s.Scene, null);
			W.Edit(s);
			B.Store = new BlueprintStore(Path.Combine(Dir, "older"));
			B.FindHomestead = () => new TerrainEditor.App.Homestead.Status(Installed, Installed ? "1.3.2" : null, Installed ? new() { "test profile" } : new(), Homestead);
			B.Message += t => Said = t;
		}

		// A Homestead blueprint in the folder.
		public void Keep(string name, params string[] kinds)
		{
			Directory.CreateDirectory(Homestead);
			File.WriteAllText(Path.Combine(Homestead, TerrainEditor.App.Homestead.FileName(name)), TerrainEditor.App.Homestead.Write(CopyFormat.ToJson(Clip(name, kinds)), name, "test", null, DateTime.Now));
		}

		public List<TerrainEditor.App.Homestead.Entry> Listed => TerrainEditor.App.Homestead.List(Homestead);

		public void Dispose()
		{
			if (Directory.Exists(Dir))
			{
				Directory.Delete(Dir, recursive: true);
			}
		}
	}

	internal static CopyData Clip(string name, params string[] kinds) => new()
	{
		W = 3, H = 2,
		Rel = new[] { 0f, 0, 0, 0, 0, 0 },
		Wt = new[] { 1f, 1, 1, 1, 1, 1 },
		Pnt = Enumerable.Repeat(-1f, 24).ToArray(),
		Objects = kinds.Select((k, i) => new CopyData.Obj(StableHash.Of(k), k, i, 0, 0, Vector3.Zero, 0, null)).ToList(),
		Poly = new() { new(-2, -1), new(2, -1), new(2, 1), new(-2, 1) },
		Name = name,
	};

	private static void Click(Button b)
	{
		b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Dispatcher.UIThread.RunJobs();
	}

	private static string Texts(Control c) => string.Join(" | ", c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text));

	private static List<Button> RowButtons(Run r, string label) => r.B.List.GetLogicalDescendants().OfType<Button>().Where(b => b.Content as string == label).ToList();

	[AvaloniaFact]
	public void TheRailsBlueprintsIsAToolWithItsPanelOnTheLeft()
	{
		using var r = new Run();
		r.Keep("Hut", "woodwall");
		Click(r.W.Tools.BlueprintsButton);
		Assert.Equal(ToolMode.Blueprints, r.W.Tools.Mode);
		Assert.True(r.W.Tools.BlueprintsButton.Classes.Contains("on"));
		Assert.True(r.B.Card.IsVisible);
		// In the left column, next to the rail, where the tools' options are.
		Assert.Same(r.W.Tools.Rail.Parent, r.B.Card.Parent);
		Assert.Contains("Hut", Texts(r.B.List));
		// Another tool replaces it.
		r.W.Tools.ChooseMode(ToolMode.Path);
		Assert.False(r.B.Card.IsVisible);
		Assert.False(r.W.Tools.BlueprintsButton.Classes.Contains("on"));
		// The Area tool's Blueprints… opens it too; ✕ goes back to View.
		r.W.AreaPanel.LibraryButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
		Dispatcher.UIThread.RunJobs();
		Assert.Equal(ToolMode.Blueprints, r.W.Tools.Mode);
		Click(r.B.Card.GetLogicalDescendants().OfType<Button>().First(b => b.Content as string == "✕"));
		Assert.Equal(ToolMode.View, r.W.Tools.Mode);
		Assert.False(r.B.Card.IsVisible);
	}

	[AvaloniaFact]
	public void AnEmptyListAndASearchThatFindsNothingSaySo()
	{
		using var r = new Run();
		r.B.Toggle(true);
		Assert.Contains("No blueprints yet", Texts(r.B.List));
		r.Keep("Gate", "woodwall");
		r.Keep("Tower", "woodwall");
		r.B.Refresh();
		Assert.Equal(2, RowButtons(r, "Paste").Count);
		r.B.Search.Text = "tow";
		Assert.Single(RowButtons(r, "Paste"));
		r.B.Search.Text = "castle";
		Assert.Contains("Nothing matches.", Texts(r.B.List));
		// Closed, the list is not read.
		r.B.Toggle(false);
		Assert.False(r.B.Card.IsVisible);
	}

	[AvaloniaFact]
	public void TheBannerSaysWhetherHomesteadIsInstalled()
	{
		using var r = new Run();
		r.B.Toggle(true);
		Assert.Contains("Homestead 1.3.2 is installed (test profile)", r.B.Banner.Text);
		Assert.False(r.B.GetHomesteadButton.IsVisible);
		r.Installed = false;
		r.B.Refresh();
		Assert.StartsWith("Homestead is not installed", r.B.Banner.Text);
		Assert.True(r.B.GetHomesteadButton.IsVisible);
		Uri? opened = null;
		r.B.OpenUrl = u => { opened = u; return Task.FromResult(true); };
		Click(r.B.GetHomesteadButton);
		Assert.Equal(TerrainEditor.App.Homestead.PageUrl, opened?.ToString());
	}

	[AvaloniaFact]
	public void EachRowPastesEditsExportsAndDeletes()
	{
		using var r = new Run();
		r.Keep("Gate", "woodwall", "wood_floor");
		r.B.Toggle(true);
		Assert.Contains("2 piece(s) · by test", Texts(r.B.List));
		Click(RowButtons(r, ".vbuild").Single());
		Assert.StartsWith("Written to ", r.Said);
		Assert.EndsWith(".vbuild. Only the objects are written, not the ground.", r.Said);
		string? edit = "none";
		r.B.EditAsked += p => edit = p;
		Click(RowButtons(r, "Edit").Single());
		Assert.Equal(Path.Combine(r.Homestead, "Gate.blueprint"), edit);
		Click(RowButtons(r, "Paste").Single());
		Assert.Equal("Gate", r.W.View.Paste.Clip!.Name);
		Assert.False(r.B.Card.IsVisible);
		// Delete asks first, and takes the picture too.
		r.B.Toggle(true);
		r.B.Confirm = _ => Task.FromResult(false);
		Click(RowButtons(r, "Delete").Single());
		Assert.Single(r.Listed);
		r.B.Confirm = _ => Task.FromResult(true);
		Click(RowButtons(r, "Delete").Single());
		Assert.Empty(r.Listed);
		Assert.Empty(Directory.GetFiles(r.Homestead));
		Assert.Contains("No blueprints yet", Texts(r.B.List));
	}

	[AvaloniaFact]
	public void OlderEditorBlueprintsAreListedAndMovedToHomestead()
	{
		using var r = new Run();
		r.B.Store.Save("Gate", "OtherWorld", "data:image/png;base64,not-base64!!", CopyFormat.ToJson(Clip("Gate", "woodwall", "Beech1")));
		r.B.Toggle(true);
		Assert.Contains("Older editor blueprints", Texts(r.B.List));
		Assert.Contains("from OtherWorld", Texts(r.B.List));
		// The thumbnail that is not a picture is left empty.
		Assert.Null(r.B.List.GetLogicalDescendants().OfType<Image>().Single().Source);
		Click(RowButtons(r, "To Homestead").Single());
		var moved = Assert.Single(r.Listed);
		Assert.Equal("Gate", moved.Name);
		Assert.NotNull(moved.Picture);
		Assert.Contains("now a Homestead blueprint", r.Said);
		// Its ground (the copy had some) is not kept.
		Assert.Contains("ground shape is not kept", r.Said);
	}

	[AvaloniaFact]
	public void BlueprintsThatCannotBeReadAreSaidSo()
	{
		using var r = new Run();
		r.B.Paste("no-such-blueprint");
		Assert.Equal("That blueprint could not be read.", r.Said);
		r.B.Paste("hs:../outside.blueprint");
		Assert.Equal("That blueprint could not be read.", r.Said);
		Assert.Null(r.B.Export("no-such-blueprint", "vbuild"));
		Assert.Equal("Could not export that blueprint.", r.Said);
		Assert.Null(r.B.Import(Path.Combine(r.Dir, "missing.vbuild")));
		Assert.StartsWith("Could not import that file: ", r.Said);
		Directory.CreateDirectory(r.Dir);
		string empty = Path.Combine(r.Dir, "empty.vbuild");
		File.WriteAllText(empty, "nothing useful here");
		Assert.Null(r.B.Import(empty));
		Assert.Contains("no pieces found in it", r.Said);
	}

	// A blueprints folder that cannot be written (here a file where the folder should be) is said so;
	// it closed the editor, losing what was not saved (an import is also a drop on the 3D view).
	[AvaloniaFact]
	public void AFolderThatCannotBeWrittenIsSaidSo()
	{
		using var r = new Run();
		r.Keep("Gate", "woodwall");
		string file = r.B.Export("hs:Gate.blueprint", "vbuild")!;
		Directory.Delete(r.Homestead, recursive: true);
		File.WriteAllText(r.Homestead, "not a folder");
		Assert.Null(r.B.Import(file));
		Assert.StartsWith("Could not write in the blueprints folder: ", r.Said);
		string export = Path.GetDirectoryName(file)!;
		Directory.Delete(export, recursive: true);
		File.WriteAllText(export, "not a folder");
		File.Delete(r.Homestead);
		r.Keep("Gate", "woodwall");
		Assert.Null(r.B.Export("hs:Gate.blueprint", "vbuild"));
		Assert.StartsWith("Could not write ", r.Said);
	}

	[AvaloniaFact]
	public async Task SavingAsksForANameAndBeforeReplacingOne()
	{
		using var r = new Run();
		r.W.View.Paste.Clip = Clip("Gate", "woodwall");
		r.B.AskDetails = (_, _) => Task.FromResult<TerrainEditor.App.Homestead.Details?>(null);
		Assert.Null(await r.B.Save());
		r.B.AskDetails = (d, _) => Task.FromResult<TerrainEditor.App.Homestead.Details?>(d with { Name = "Gate" });
		Assert.Equal("Gate", await r.B.Save());
		Assert.True(File.Exists(Path.Combine(r.Homestead, "Gate.png")));
		r.B.Confirm = _ => Task.FromResult(false);
		Assert.Null(await r.B.Save());
		r.B.Confirm = _ => Task.FromResult(true);
		Assert.Equal("Gate", await r.B.Save());
		Assert.Single(r.Listed);
		// Without Homestead in the game, saving says it shows there once installed.
		r.Installed = false;
		Assert.Equal("Gate", await r.B.Save());
		Assert.Contains("not installed in your game yet", r.Said);
	}

	[AvaloniaFact]
	public async Task DetailsCostAndTagsAreShownSearchedAndChanged()
	{
		using var r = new Run();
		r.W.View.Paste.Clip = Clip("Gate", "woodwall", "woodwall", "stone_wall_2x1");
		r.B.AskDetails = (d, cost) => Task.FromResult<TerrainEditor.App.Homestead.Details?>(new("Gate", "A gate for the north wall", new() { "gate", "stone" }));
		Assert.Equal("Gate", await r.B.Save());
		r.Keep("Tower", "woodwall");
		r.B.Toggle(true);
		string texts = Texts(r.B.List);
		Assert.Contains("Cost: Stone 4 · Wood 4 (needs Stonecutter, Workbench)", texts);
		Assert.Contains("A gate for the north wall", texts);
		Assert.Contains("stone", texts);
		// Found by a tag, by its description, and by both.
		r.B.Search.Text = "stone";
		Assert.Single(RowButtons(r, "Paste"));
		r.B.Search.Text = "north gate";
		Assert.Single(RowButtons(r, "Paste"));
		r.B.Search.Text = "";
		// Details…: a new name (the file follows, with its picture) and tags.
		r.B.AskDetails = (d, _) => Task.FromResult<TerrainEditor.App.Homestead.Details?>(d with { Name = "North gate", Tags = new() { "gate" } });
		Assert.True(await r.B.EditDetails(r.Listed.Single(e => e.Name == "Gate")));
		var gate = r.Listed.Single(e => e.Name == "North gate");
		Assert.Equal("North gate.blueprint", Path.GetFileName(gate.Path));
		Assert.NotNull(gate.Picture);
		Assert.Equal(new[] { "gate" }, gate.Tags);
		Assert.Equal("A gate for the north wall", gate.Description);
		Assert.Equal(3, gate.Pieces);
		// Cancelled: nothing changes.
		r.B.AskDetails = (_, _) => Task.FromResult<TerrainEditor.App.Homestead.Details?>(null);
		Assert.False(await r.B.EditDetails(gate));
	}

	[AvaloniaFact]
	public void ImportFileUsesThePicker()
	{
		using var r = new Run();
		r.Keep("Gate", "woodwall");
		string file = r.B.Export("hs:Gate.blueprint", "vbuild")!;
		r.B.PickFile = () => Task.FromResult<string?>(null);
		Click(r.B.ImportButton);
		Assert.Single(r.Listed);
		r.B.PickFile = () => Task.FromResult<string?>(file);
		Click(r.B.ImportButton);
		Assert.Equal(2, r.Listed.Count);
		Assert.Contains(r.Listed, e => e.Name.StartsWith("Gate", StringComparison.Ordinal) && e.Name != "Gate");
	}
}

// A blueprint pasted into a world that cannot make some of its kinds (opens the test world).
[Collection("World files")]
public class PanelBlueprintsWorldTests
{
	[AvaloniaFact]
	public void KindsTheWorldCannotMakeAreLeftOutWhenPasting()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			var scene = WorldScene.Load(dir, 0, 0, 1);
			using var r = new PanelBlueprintsTests.Run(scene.Session);
			r.B.Store.Save("Odd", null, null, CopyFormat.ToJson(PanelBlueprintsTests.Clip("Odd", "Beech1", "NoSuchPiece_xyz")));
			r.B.Paste(r.B.Store.List()[0].Id);
			Assert.Contains("1 kind(s) are unknown here and are left out: NoSuchPiece_xyz", r.Said);
			Assert.DoesNotContain(r.W.View.Paste.Clip!.Objects, o => o.Name == "NoSuchPiece_xyz");
		}
		finally
		{
			Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
		}
	}
}
