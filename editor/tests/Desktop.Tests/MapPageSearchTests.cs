using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map page's search and zone filter on the test world: a box emptied to nothing, items in a chest counted in all,
/// a search with more hits than are pinned, an older search ending last, a search of the world before it was shown
/// again, and a distance box emptied; then a live world's pending line and question.
/// </summary>
[Collection("World files")]
public class MapPageSearchTests
{
    /// <summary>
    /// A shown main window with its own copy of the test world open on the map; the copy's folder.
    /// </summary>
    private static async Task<(MainWindow W, string Dir)> Open()
    {
        string dir = Fixtures.CopyFixture();
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
        return (w, dir);
    }

    /// <summary>
    /// Deletes the copy of the test world (its temporary parent folder).
    /// </summary>
    private static void Done(string dir) => Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);

    [AvaloniaFact]
    public async Task ItemsAreCountedInAllAndTooManyHitsAreSaid()
    {
        var (w, dir) = await Open();
        try
        {
            var m = w.MapPage!;
            // No text at all: nothing searched, nothing pinned.
            m.SearchBox.Text = null;
            await m.Search();
            Assert.Equal("", m.SearchInfo.Text);
            Assert.Empty(m.Map.Pins);
            // A chest holding 42 wood, added in this session.
            var world = w.World!.World;
            int chest = world.Objects.First(o => o.Prefab == StableHash.Of("piece_chest_wood")).Id;
            var z = ZdoData.Parse(world.ObjectBytes(chest));
            var inv = new InventoryData();
            inv.Items.Add(new InventoryData.Item { Prefab = StableHash.Of("Wood"), Stack = 30 });
            inv.Items.Add(new InventoryData.Item { Prefab = StableHash.Of("Wood"), Stack = 12, X = 1 });
            z.SetBytes(StableHash.Of("items"), inv.Write());
            w.World.Edits.AddObjects([new NewObject(-1, z.Prefab, z.Position, z.Rotation, 0f, Fresh: false, Raw: z.Serialize())]);
            m.SearchWhat.SelectedIndex = 1;
            m.SearchBox.Text = "wood";
            await m.Search();
            var expected = WorldSearch.Search(world, w.World.Edits, "wood", "items");
            Assert.Equal(expected.Hits.Count, m.Map.Pins.Count);
            Assert.Contains($"{expected.Total:N0} found in all.", m.SearchInfo.Text);
            Assert.StartsWith($"{expected.Counts["Wood"]:N0} Wood", m.SearchInfo.Text);
            // Every kind with an e: more hits than are pinned, it says so when there are.
            m.SearchWhat.SelectedIndex = 0;
            m.SearchBox.Text = "e";
            await m.Search();
            var many = WorldSearch.Search(world, w.World.Edits, "e", "kinds");
            Assert.Equal(many.Truncated, m.SearchInfo.Text!.Contains($", the first {many.Hits.Count:N0} pinned", StringComparison.Ordinal));
            Assert.Equal(many.Hits.Count, m.Map.Pins.Count);
        }
        finally
        {
            w.CloseWithoutAsking();
            Done(dir);
        }
    }

    // A search started while another runs: only the newer one's results are pinned, the older ending last.
    [AvaloniaFact]
    public async Task AnOlderSearchEndingLastIsDropped()
    {
        var (w, dir) = await Open();
        try
        {
            var m = w.MapPage!;
            // The items search is held until the newer one is shown.
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var search = m.RunSearch;
            m.RunSearch = async (s, q, what) =>
            {
                if (what == "items")
                {
                    await held.Task;
                }
                return await search(s, q, what);
            };
            // The box emptied while the kind changes, so the change searches nothing by itself.
            m.SearchBox.Text = null;
            m.SearchWhat.SelectedIndex = 1;
            m.SearchBox.Text = "wood";
            var older = m.Search();
            m.SearchBox.Text = null;
            m.SearchWhat.SelectedIndex = 0;
            m.SearchBox.Text = "e";
            await m.Search();
            Assert.False(older.IsCompleted);
            string info = m.SearchInfo.Text!;
            var many = WorldSearch.Search(w.World!.World, w.World.Edits, "e", "kinds");
            held.SetResult();
            await older;
            Assert.Equal(many.Hits.Count, m.Map.Pins.Count);
            Assert.Equal(info, m.SearchInfo.Text);
            // Its hits are kinds: picking one goes there, to select it (not to the inspector).
            m.ShowHit(0);
            Assert.False(m.Target!.Value.Inspect);
        }
        finally
        {
            w.CloseWithoutAsking();
            Done(dir);
        }
    }

    // The world shown again (read again, saved) while a search runs: its results, of the world as it was, are dropped
    // and it says to search again.
    [AvaloniaFact]
    public async Task ASearchOfTheWorldBeforeItWasShownAgainIsDropped()
    {
        var (w, dir) = await Open();
        try
        {
            var m = w.MapPage!;
            var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var search = m.RunSearch;
            m.RunSearch = async (s, q, what) =>
            {
                await held.Task;
                return await search(s, q, what);
            };
            m.SearchBox.Text = null;
            m.SearchWhat.SelectedIndex = 0;
            m.SearchBox.Text = "e";
            var running = m.Search();
            Assert.Equal("Searching…", m.SearchInfo.Text);
            m.Show(w.World!);
            held.SetResult();
            await running;
            Assert.Empty(m.Map.Pins);
            Assert.Equal("The world changed during the search: search again.", m.SearchInfo.Text);
        }
        finally
        {
            w.CloseWithoutAsking();
            Done(dir);
        }
    }

    [AvaloniaFact]
    public async Task ADistanceBoxEmptiedIsNoLimit()
    {
        var (w, dir) = await Open();
        try
        {
            var m = w.MapPage!;
            m.NoBuildBox.IsChecked = false;
            m.NoEditBox.IsChecked = false;
            m.OnlyGenBox.IsChecked = false;
            m.RMaxBox.Text = "0";
            await m.ShowMatching();
            Assert.All(m.Matches, z => Assert.Equal((0, 0), z));
            int near = m.Matches.Count;
            // Emptied (no text at all): no limit, so no fewer zones than within 0 m.
            m.RMaxBox.Text = null;
            await m.ShowMatching();
            Assert.True(m.Matches.Count >= near);
        }
        finally
        {
            w.CloseWithoutAsking();
            Done(dir);
        }
    }

    [AvaloniaFact]
    public async Task ALiveWorldSaysWhatIsNotAppliedAndAsksBeforeMarking()
    {
        using var game = new FakeGame();
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        try
        {
            await w.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label()), "Connecting…");
            var m = w.MapPage!;
            m.Stop();
            Assert.Equal("Everything is applied.", m.Pending.Text);
            w.World!.Edits.SetReset(new ZoneReset(3, 4, true, false), true);
            m.UpdatePending();
            Assert.Equal("Not applied yet: 1 zone reset.", m.Pending.Text);
            // Marking: the question says the game makes them again at once where players are, once applied.
            m.NoBuildBox.IsChecked = false;
            m.NoEditBox.IsChecked = false;
            m.OnlyGenBox.IsChecked = false;
            m.GroundBox.IsChecked = false;
            string? asked = null;
            m.Confirm = q => { asked = q; return Task.FromResult(false); };
            await m.MarkMatching();
            Assert.NotNull(asked);
            Assert.Contains("(at once where players are, once applied)", asked);
            Assert.Contains("Apply live does it", asked);
            Assert.DoesNotContain("ground edits undone", asked);
            // Answered no: still the one reset only, said in the zone line.
            Assert.Equal(1, w.World.Pending.Resets);
            Assert.Contains("· 1 marked for reset", m.ZoneInfo.Text);
        }
        finally
        {
            w.MapPage?.Stop();
            w.CloseWithoutAsking();
        }
    }
}
