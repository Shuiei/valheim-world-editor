using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

// The map page's edges on the test world: nothing happens without a world, the players' line when the
// game errors or sends garbage, the biomes' names, the cursor's line when the mouse is over the map, an
// empty search and one that fails, and the zone filter's messages and questions.
[Collection("World files")]
public class PanelMapTests
{
	private sealed class Run : IDisposable
	{
		public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
		public string Dir { get; } = EditTests.CopyFixture();
		public List<string> Told { get; } = new();
		public MapPage M => W.MapPage!;

		public async Task Open()
		{
			W.Show();
			await W.OpenWorld(() => Task.Run(() => WorldSession.Open(Dir)), "Opening…");
			M.Tell = t => { Told.Add(t); return Task.CompletedTask; };
		}

		public void Dispose() => Directory.Delete(Path.GetDirectoryName(Dir)!, recursive: true);
	}

	[AvaloniaFact]
	public async Task WithoutAWorldNothingHappens()
	{
		var m = new MapPage();
		m.UpdatePending();
		await m.ShowMatching();
		await m.MarkMatching();
		await m.UnmarkAll();
		await m.Search();
		m.Pick(0, 0);
		Assert.Equal("Zone 0, 0 · ", m.PickTitle.Text);
		Assert.Empty(m.Matches);
		Assert.Equal("", m.SearchInfo.Text ?? "");
	}

	[AvaloniaFact]
	public async Task ThePlayersLineSaysWhenTheGameDoesNotAnswer()
	{
		foreach (var broken in new Action<FakeGame>[] { g => g.Fail["/players"] = 500, g => g.Players = "this is not json" })
		{
			using var game = new FakeGame();
			broken(game);
			var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
			w.Show();
			await w.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label()), "Connecting…");
			await LiveTests.Until(() => w.MapPage!.LiveText.Text == "Live world · the game does not answer right now.");
			w.MapPage!.Stop();
		}
	}

	[AvaloniaFact]
	public async Task EachBiomeHasItsName()
	{
		using var r = new Run();
		await r.Open();
		var terrain = r.W.World!.Terrain;
		foreach (var (biome, name) in new[] { (ValheimGen.Heightmap.Biome.BlackForest, "Black Forest"), (ValheimGen.Heightmap.Biome.AshLands, "Ashlands"), (ValheimGen.Heightmap.Biome.DeepNorth, "Deep North"), (ValheimGen.Heightmap.Biome.Meadows, "Meadows") })
		{
			// The first zone of that biome on a walk across the world.
			(int X, int Z)? found = null;
			for (int z = -160; z <= 160 && found == null; z += 2)
			{
				for (int x = -160; x <= 160 && found == null; x += 2)
				{
					if (terrain.BiomeAt(x * 64f, z * 64f) == biome)
					{
						found = (x, z);
					}
				}
			}
			Assert.NotNull(found);
			r.M.Pick(found.Value.X, found.Value.Z);
			Assert.EndsWith(name, r.M.PickTitle.Text);
		}
	}

	// What the map raises when the mouse moves over it (headless windows put an overlay above the map,
	// so the event is raised as the map does).
	private static void Hovered(MapPage m, float x, float z) =>
		(typeof(MapView).GetField("Hovered", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(m.Map) as Action<float, float>)!(x, z);

	[AvaloniaFact]
	public async Task TheCursorLineFollowsTheMouseOverTheMap()
	{
		// No world yet: nothing to say.
		var empty = new MapPage();
		Hovered(empty, 10, 10);
		Assert.Equal("", empty.Cursor.Text ?? "");
		using var r = new Run();
		await r.Open();
		Hovered(r.M, 100.4f, -40.6f);
		Assert.Matches(@"^x 100, z -41 · zone 2, -1 · \w", r.M.Cursor.Text);
	}

	[AvaloniaFact]
	public async Task AnEmptySearchClearsThePinsAndAFailedOneSaysSo()
	{
		using var r = new Run();
		// A sign with a text, saved into the world first (the test world has none).
		var world = WorldSave.Load(r.Dir);
		var tree = world.Objects.First(o => o.Prefab == StableHash.Of("Beech1"));
		var sign = new ZdoData { Prefab = StableHash.Of("sign"), Position = tree.Position + new System.Numerics.Vector3(2, 0, 2) };
		sign.Set("strings", StableHash.Of("text"), "Welcome home");
		Assert.True(WorldWriter.Save(world, Array.Empty<ZoneEdit>(), added: new[] { new NewObject(-1, sign.Prefab, sign.Position, default, 0, Fresh: false, Raw: sign.Serialize()) }).Saved);
		await r.Open();
		r.M.SearchBox.Text = "beech";
		await r.M.Search();
		Assert.NotEmpty(r.M.Map.Pins);
		r.M.SearchBox.Text = "   ";
		await r.M.Search();
		Assert.Empty(r.M.Map.Pins);
		Assert.Equal("", r.M.SearchInfo.Text);
		Assert.Null(r.M.Hits.ItemsSource);
		// Texts are read from the chunk files: gone, the search fails with the reason.
		foreach (var f in Directory.GetFiles(r.Dir, "*.chunk"))
		{
			File.Delete(f);
		}
		r.M.SearchWhat.SelectedIndex = 2;
		r.M.SearchBox.Text = "a";
		await r.M.Search();
		Assert.StartsWith("Search failed: ", r.M.SearchInfo.Text);
	}

	[AvaloniaFact]
	public async Task TheZoneFilterSaysWhenNothingMatchesOrIsMarked()
	{
		using var r = new Run();
		await r.Open();
		await r.M.UnmarkAll();
		Assert.Equal("No zone is marked for reset.", r.Told.Single());
		// Nothing that far out: nothing to mark.
		r.M.RMinBox.Text = "99999";
		await r.M.MarkMatching();
		Assert.Equal("No zone matches the filter.", r.Told[1]);
		// A question answered no marks nothing.
		r.M.RMinBox.Text = "";
		r.M.NoBuildBox.IsChecked = false;
		r.M.OnlyGenBox.IsChecked = false;
		r.M.NoEditBox.IsChecked = false;
		r.M.GroundBox.IsChecked = true;
		string? asked = null;
		r.M.Confirm = q => { asked = q; return Task.FromResult(false); };
		await r.M.MarkMatching();
		Assert.Contains("and their ground edits undone", asked);
		Assert.Equal(0, r.W.World!.Pending.Resets);
		// The filter follows its boxes once shown.
		Assert.NotEmpty(r.M.Matches);
		r.M.RMaxBox.Text = "0";
		Assert.All(r.M.Matches, z => Assert.Equal((0, 0), z));
	}

	[AvaloniaFact]
	public async Task UnmarkingBeforeTheZonesAreReadStillWorks()
	{
		using var r = new Run();
		await r.Open();
		r.W.World!.Edits.SetReset(new ZoneReset(3, 4, true, false), true);
		await r.M.UnmarkAll();
		Assert.Equal(0, r.W.World.Pending.Resets);
		Assert.Equal("Everything is saved.", r.M.Pending.Text);
	}
}
