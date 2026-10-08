using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Editing;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The map page's tools: search, zones picked by filter to reset, and the edited zones.
[Collection("World files")]
public class MapToolsTests
{
	private static void Done(string dir) => Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);

	[Fact]
	public void ZoneFilterLeavesOutBuildingsEditsAndFarZones()
	{
		// x, z, biome, pieces, edited, objects, distance to buildings, generated.
		int[] stats =
		{
			0, 0, 1, 5, 0, 10, 0, 1,     // buildings
			1, 0, 1, 0, 0, 3, 1, 1,      // next to them
			5, 0, 1, 0, 0, 4, 5, 1,      // far enough
			6, 0, 8, 0, 1, 2, 6, 1,      // ground edited, Black Forest
			7, 0, 1, 0, 0, 1, 7, 0,      // not generated
			100, 0, 1, 0, 0, 1, 100, 1,  // 6400 m out
		};
		var all = new ZoneFilter(new HashSet<int>(), true, 2, true, true, null, null);
		var (zones, objects) = all.Match(stats);
		Assert.Equal(new[] { (5, 0), (100, 0) }, zones);
		Assert.Equal(5, objects);
		Assert.Equal(new[] { (5, 0) }, (all with { MaxRadius = 1000 }).Match(stats).Zones);
		Assert.Equal(new[] { (6, 0) }, (all with { Biomes = new HashSet<int> { 8 }, NoEdits = false }).Match(stats).Zones);
		// Zones with pieces never match, even with every option off.
		var loose = new ZoneFilter(new HashSet<int>(), false, 0, false, false, null, null);
		Assert.DoesNotContain((0, 0), loose.Match(stats).Zones);
		Assert.Contains((1, 0), loose.Match(stats).Zones);
	}

	[AvaloniaFact]
	public async Task SearchFindsAndOpensTheObject()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
			w.Show();
			await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
			var map = w.MapPage!;
			var expected = WorldSearch.Search(w.World!.World, w.World.Edits, "beech", "kinds");
			Assert.NotEmpty(expected.Hits);
			map.SearchBox.Text = "beech";
			await map.Search();
			Assert.Equal(expected.Hits.Count, map.Map.Pins.Count);
			Assert.Contains($"{expected.Total:N0} found", map.SearchInfo.Text);
			map.Hits.SelectedIndex = 0;
			var hit = expected.Hits[0];
			Assert.Equal(0, map.Map.PinChosen);
			Assert.Equal((hit.Id, false), map.Target);
			Assert.StartsWith(hit.Name, map.PickTitle.Text);
			var (zx, zz) = map.Spot!.Value;
			await w.EditArea(zx, zz, 3);
			var sel = Assert.Single(w.View.Selected);
			Assert.Equal(hit.Id, w.Session!.Scene.Things[sel].Id);
			// A plain click on the map forgets the object.
			map.Pick(zx, zz);
			Assert.Null(map.Target);
			// Nothing to find.
			map.SearchBox.Text = "no such thing at all";
			await map.Search();
			Assert.Equal("Nothing found.", map.SearchInfo.Text);
			Assert.Empty(map.Map.Pins);
		}
		finally
		{
			Done(dir);
		}
	}

	[AvaloniaFact]
	public async Task ZonesMarkedForResetAndEditedZones()
	{
		string dir = EditTests.CopyFixture();
		try
		{
			var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
			w.Show();
			await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
			var map = w.MapPage!;
			map.Confirm = _ => Task.FromResult(true);
			map.Tell = _ => Task.CompletedTask;
			// Every zone without buildings.
			map.NoBuildBox.IsChecked = false;
			map.NoEditBox.IsChecked = false;
			map.OnlyGenBox.IsChecked = false;
			await map.ShowMatching();
			Assert.NotEmpty(map.Matches);
			Assert.Contains("zones match", map.ZoneInfo.Text);
			int all = map.Matches.Count;
			// A biome narrows it, live.
			map.BiomeButtons[1].IsChecked = true;
			Assert.True(map.Matches.Count <= all);
			map.BiomeButtons[1].IsChecked = false;
			Assert.Equal(all, map.Matches.Count);
			await map.MarkMatching();
			Assert.Equal(all, w.World!.Pending.Resets);
			Assert.Contains("zone reset", map.Pending.Text);
			await map.UnmarkAll();
			Assert.Equal(0, w.World.Pending.Resets);
			// Edited ground: listed; picking one goes there.
			await w.EditArea(0, 0, 1);
			w.Session!.Shape(32, 32, Formula.Compile("2", new string[0]), 4, 0, "x");
			w.ShowMap();
			var edited = w.World.Edits.All().Where(e => e.HeightCount > 0).ToList();
			Assert.Equal(edited.Count, ((IEnumerable<string>)map.EditedList.ItemsSource!).Count());
			var e0 = edited.OrderByDescending(e => e.HeightCount + e.PaintCount).First();
			map.EditedList.SelectedIndex = 0;
			Assert.Equal((e0.ZoneX, e0.ZoneZ), map.Spot);
		}
		finally
		{
			Done(dir);
		}
	}
}
