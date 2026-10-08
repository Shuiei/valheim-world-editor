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

// The Blueprints panel through its list: empty, searched, each row's Paste, .blueprint, .vbuild and
// Delete buttons, thumbnails that are not pictures, Import file…, and what it says for files that are
// missing, empty or not blueprints, a name already taken, and kinds a world cannot make. Its store is a
// temporary folder, never the user's.
public class PanelBlueprintsTests
{
	internal sealed class Run : IDisposable
	{
		public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
		public string Dir { get; } = Path.Combine(Path.GetTempPath(), "vwe-bp-ui-" + Guid.NewGuid().ToString("N")[..8]);
		public BlueprintsPanel B => W.Blueprints;
		public string? Said;

		public Run(EditSession? s = null)
		{
			W.Show();
			s ??= EditTests.Flat(2);
			W.View.Show(s.Scene, null);
			W.Edit(s);
			B.Store = new BlueprintStore(Dir);
			B.Message += t => Said = t;
		}

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
	public void AnEmptyListAndASearchThatFindsNothingSaySo()
	{
		using var r = new Run();
		r.B.Toggle(true);
		Assert.Contains("No blueprints yet", Texts(r.B.List));
		r.B.Store.Save("Gate", null, null, CopyFormat.ToJson(Clip("Gate", "woodwall")));
		r.B.Store.Save("Tower", null, null, CopyFormat.ToJson(Clip("Tower", "woodwall")));
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
	public void EachRowPastesExportsAndDeletes()
	{
		using var r = new Run();
		r.B.Store.Save("Gate", "OtherWorld", "data:image/png;base64,not-base64!!", CopyFormat.ToJson(Clip("Gate", "woodwall", "Beech1")));
		r.B.Toggle(true);
		// The thumbnail that is not a picture is left empty.
		Assert.Null(r.B.List.GetLogicalDescendants().OfType<Image>().Single().Source);
		Assert.Contains("from OtherWorld", Texts(r.B.List));
		Click(RowButtons(r, ".vbuild").Single());
		Assert.StartsWith("Written to ", r.Said);
		Assert.EndsWith(".vbuild. Only the objects are written, not the ground.", r.Said);
		Click(RowButtons(r, ".blueprint").Single());
		Assert.Contains("PlanBuild/blueprints", r.Said);
		Click(RowButtons(r, "Paste").Single());
		Assert.Equal("Gate", r.W.View.Paste.Clip!.Name);
		Assert.False(r.B.Card.IsVisible);
		// Delete asks first.
		r.B.Toggle(true);
		r.B.Confirm = _ => Task.FromResult(false);
		Click(RowButtons(r, "Delete").Single());
		Assert.Single(r.B.Store.List());
		r.B.Confirm = _ => Task.FromResult(true);
		Click(RowButtons(r, "Delete").Single());
		Assert.Empty(r.B.Store.List());
		Assert.Contains("No blueprints yet", Texts(r.B.List));
	}

	[AvaloniaFact]
	public void AThumbnailThatIsNotAPngIsLeftEmpty()
	{
		using var r = new Run();
		r.B.Store.Save("Gate", null, "data:image/jpeg;base64,AAAA", CopyFormat.ToJson(Clip("Gate", "woodwall")));
		r.B.Toggle(true);
		Assert.All(r.B.List.GetLogicalDescendants().OfType<Image>(), i => Assert.Null(i.Source));
	}

	[AvaloniaFact]
	public void BlueprintsThatCannotBeReadAreSaidSo()
	{
		using var r = new Run();
		r.B.Paste("no-such-blueprint");
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

	[AvaloniaFact]
	public async Task SavingAsksForANameAndBeforeReplacingOne()
	{
		using var r = new Run();
		r.W.View.Paste.Clip = Clip("Gate", "woodwall");
		r.B.AskName = _ => Task.FromResult<string?>(null);
		Assert.Null(await r.B.Save());
		r.B.AskName = _ => Task.FromResult<string?>("Gate");
		Assert.Equal("Gate", await r.B.Save());
		r.B.Confirm = _ => Task.FromResult(false);
		Assert.Null(await r.B.Save());
		r.B.Confirm = _ => Task.FromResult(true);
		Assert.Equal("Gate", await r.B.Save());
		Assert.Single(r.B.Store.List());
	}

	[AvaloniaFact]
	public void ImportFileUsesThePicker()
	{
		using var r = new Run();
		r.B.Store.Save("Gate", null, null, CopyFormat.ToJson(Clip("Gate", "woodwall")));
		string file = r.B.Export(r.B.Store.List()[0].Id, "vbuild")!;
		r.B.PickFile = () => Task.FromResult<string?>(null);
		Click(r.B.ImportButton);
		Assert.Single(r.B.Store.List());
		r.B.PickFile = () => Task.FromResult<string?>(file);
		Click(r.B.ImportButton);
		Assert.Equal(2, r.B.Store.List().Count);
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
