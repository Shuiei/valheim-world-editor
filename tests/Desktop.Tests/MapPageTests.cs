using Avalonia.Headless.XUnit;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map page's edges on the test world: nothing happens without a world, the players' line when the game errors or
/// sends garbage (or is slow, while another world is shown, or shown again), the biomes' names, the cursor's line when the mouse is over the map, an empty search and one that
/// fails, the zone filter's messages and questions (kept with their world when another is shown), and Show ruins.
/// </summary>
[Collection("World files")]
public class MapPageTests
{
    /// <summary>
    /// A main window on its own copy of the test world, with the map page's messages collected; deleted when disposed.
    /// </summary>
    private sealed class Run : IDisposable
    {
        public MainWindow W { get; } = new(load: false) { Width = 1600, Height = 1000 };
        public string Dir { get; } = Fixtures.CopyFixture();
        public List<string> Told { get; } = [];
        public MapPage M => W.MapPage!;

        /// <summary>
        /// Opens the world (its map page) and collects what the map tells.
        /// </summary>
        public async Task Open()
        {
            W.Show();
            await W.OpenWorld(() => Task.Run(() => WorldSession.Open(Dir)), "Opening…");
            M.Tell = t => { Told.Add(t); return Task.CompletedTask; };
        }

        /// <inheritdoc/>
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

    // Show ruins: off at first; it switches the map's ruins on and off.
    [AvaloniaFact]
    public void ShowRuinsSwitchesTheMapsRuins()
    {
        var m = new MapPage();
        Assert.False(m.RuinsBox.IsChecked);
        Assert.False(m.Map.ShowRuins);
        m.RuinsBox.IsChecked = true;
        Assert.True(m.Map.ShowRuins);
        m.RuinsBox.IsChecked = false;
        Assert.False(m.Map.ShowRuins);
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
            await TestWindows.Until(() => w.MapPage!.LiveText.Text == "Live world · the game does not answer right now.");
            w.MapPage!.Stop();
        }
    }

    // A game slow to answer gets one players question at a time, and its answer, come after another world is
    // shown, does not put its players on that world's map.
    [AvaloniaFact]
    public async Task PlayersAreAskedOnceAtATimeAndAnOldWorldsAnswerIsDropped()
    {
        using var slow = new FakeGame { HoldPlayers = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var other = new FakeGame { Players = "[{\"name\":\"Cyd\",\"x\":0,\"y\":30,\"z\":0,\"yaw\":0}]" };
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        await w.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(slow.Url, slow.Token), FakeGame.Label()), "Connecting…");
        var m = w.MapPage!;
        await TestWindows.Until(() => slow.PlayerReads == 1);
        // The timer's next ticks while the first question waits: no more questions.
        for (int i = 0; i < 3; i++)
        {
            await m.PollPlayers();
        }
        Assert.Equal(1, slow.PlayerReads);
        // Another world shown; then the slow game answers.
        using var session = await WorldSession.OpenLive(new LiveBridge(other.Url, other.Token), FakeGame.Label());
        m.Show(session);
        await TestWindows.Until(() => m.LiveText.Text == "Live world · 1 player(s) online: Cyd");
        slow.HoldPlayers.SetResult();
        await Task.Delay(300);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Live world · 1 player(s) online: Cyd", m.LiveText.Text);
        Assert.Equal(["Cyd"], m.Map.Players.Select(p => p.Name));
        m.Stop();
    }

    // A world shown again (A, B, then A) before its game answered is not asked a second time; its one answer then
    // puts its players on the map.
    [AvaloniaFact]
    public async Task AWorldShownAgainBeforeItsGameAnsweredIsNotAskedTwice()
    {
        using var slow = new FakeGame { HoldPlayers = new(TaskCreationOptions.RunContinuationsAsynchronously), Players = "[{\"name\":\"Ash\",\"x\":0,\"y\":30,\"z\":0,\"yaw\":0}]" };
        using var other = new FakeGame();
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        await w.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(slow.Url, slow.Token), FakeGame.Label()), "Connecting…");
        var m = w.MapPage!;
        var first = w.World!;
        await TestWindows.Until(() => slow.PlayerReads == 1);
        using var session = await WorldSession.OpenLive(new LiveBridge(other.Url, other.Token), FakeGame.Label());
        m.Show(session);
        await TestWindows.Until(() => other.PlayerReads == 1);
        m.Show(first);
        await m.PollPlayers();
        Assert.Equal(1, slow.PlayerReads);
        slow.HoldPlayers.SetResult();
        await TestWindows.Until(() => m.LiveText.Text == "Live world · 1 player(s) online: Ash");
        Assert.Equal(["Ash"], m.Map.Players.Select(p => p.Name));
        m.Stop();
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

    /// <summary>
    /// What the map raises when the mouse moves over it (headless windows put an overlay above the map, so the event is
    /// raised as the map does).
    /// </summary>
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
        var (id, prefab, position, rotation, scale) = world.Objects.First(o => o.Prefab == StableHash.Of("Beech1"));
        var sign = new ZdoData { Prefab = StableHash.Of("sign"), Position = position + new System.Numerics.Vector3(2, 0, 2) };
        sign.Set("strings", StableHash.Of("text"), "Welcome home");
        Assert.True(WorldWriter.Save(world, [], added: [new NewObject(-1, sign.Prefab, sign.Position, default, 0, Fresh: false, Raw: sign.Serialize())]).Saved);
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
        foreach (string f in Directory.GetFiles(r.Dir, "*.chunk"))
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
        // A distance that is no number (or not a finite one) is said, not taken as no limit; spaces between thousands
        // are fine.
        foreach (string bad in new[] { "abc", "NaN", "Infinity" })
        {
            r.M.RMaxBox.Text = bad;
            Assert.Empty(r.M.Matches);
            Assert.StartsWith("The distances from the centre must be numbers of metres", r.M.ZoneInfo.Text);
        }
        r.M.RMaxBox.Text = "";
        int all = r.M.Matches.Count;
        Assert.True(all > 0);
        r.M.RMaxBox.Text = "20 000";
        Assert.Equal(all, r.M.Matches.Count);
    }

    // Zone counts still being read when another world is shown are the first world's: dropped, not matched against the
    // second; a question answered once another world is shown marks nothing in either.
    [AvaloniaFact]
    public async Task ZoneCountsAndMarksStayWithTheirWorld()
    {
        using var r = new Run();
        await r.Open();
        var first = r.W.World!;
        string otherDir = Fixtures.CopyFixture();
        try
        {
            var other = WorldSession.Open(otherDir);
            r.M.NoBuildBox.IsChecked = false;
            r.M.OnlyGenBox.IsChecked = false;
            r.M.NoEditBox.IsChecked = false;
            // Shown before the counts come back (they come back on this thread, after it).
            var showing = r.M.ShowMatching();
            r.M.Show(other);
            await showing;
            Assert.Empty(r.M.Matches);
            // The second world's own counts are read when asked.
            await r.M.ShowMatching();
            Assert.NotEmpty(r.M.Matches);
            // Back to the first, which switches to the second while asking.
            r.M.Show(first);
            r.M.Confirm = _ =>
            {
                r.M.Show(other);
                return Task.FromResult(true);
            };
            await r.M.MarkMatching();
            Assert.Equal(0, first.Pending.Resets);
            Assert.Equal(0, other.Pending.Resets);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(otherDir)!, recursive: true);
        }
    }

    // Zone counts still being read when the same world is shown again (after a save or a discard) are from before: dropped
    // (with their "Reading" status), and read again when next asked; a question answered once it is shown again marks
    // nothing.
    [AvaloniaFact]
    public async Task ZoneCountsAndMarksFromBeforeTheWorldIsShownAgainAreDropped()
    {
        using var r = new Run();
        await r.Open();
        r.M.NoBuildBox.IsChecked = false;
        r.M.OnlyGenBox.IsChecked = false;
        r.M.NoEditBox.IsChecked = false;
        // Shown again before the counts come back (they come back on this thread, after it).
        var showing = r.M.ShowMatching();
        r.M.Show(r.W.World!);
        await showing;
        Assert.Empty(r.M.Matches);
        Assert.Equal("", r.M.ZoneInfo.Text);
        var again = r.M.ShowMatching();
        Assert.Equal("Reading every zone…", r.M.ZoneInfo.Text);
        await again;
        Assert.NotEmpty(r.M.Matches);
        r.M.Confirm = _ =>
        {
            r.M.Show(r.W.World!);
            return Task.FromResult(true);
        };
        await r.M.MarkMatching();
        Assert.Equal(0, r.W.World!.Pending.Resets);
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
