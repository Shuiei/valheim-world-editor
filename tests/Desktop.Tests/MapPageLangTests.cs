using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map page in another language (the made-up one: PseudoLang): with the test world shown, a zone picked, a search
/// done and its first result picked and the zone filter's matches shown, it shows only catalogue texts and data (the
/// world's name and seed, biomes, what was found); the zone filter's questions and messages are catalogue texts too.
/// </summary>
[Collection("DataDir")]
public sealed class MapPageLangTests
{
    /// <summary>
    /// The names that are data on the page: the brand title's words, the world's name and seed, and what a search found
    /// (kinds, items, the texts that matched). Biome names come from the catalogue.
    /// </summary>
    private static List<string> Data(MainWindow w, WorldSearch.Result found) =>
    [
        "Valheim", "World Editor", w.World!.World.Name, w.World.World.SeedName,
        .. found.Counts.Keys,
        .. found.Hits.Select(h => h.Name),
        .. found.Hits.Select(h => h.Match ?? ""),
    ];

    [AvaloniaFact]
    public async Task MapPageShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        string dir = Fixtures.CopyFixture();
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        try
        {
            w.Show();
            await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
            var m = w.MapPage!;
            var told = new List<string>();
            m.Tell = t => { told.Add(t); return Task.CompletedTask; };
            m.Confirm = q => { told.Add(q); return Task.FromResult(false); };
            m.Pick(0, 0);
            // The picked zone's biome is the catalogue's name, inside the zone's line.
            Assert.Contains(PseudoLang.Wrap(ZoneFilter.BiomeName(w.World!.Terrain.BiomeAt(0, 0)).English), m.PickTitle.Text, StringComparison.Ordinal);
            m.SearchBox.Text = "e";
            await m.Search();
            var found = WorldSearch.Search(w.World!.World, w.World.Edits, "e", "kinds");
            Assert.NotEmpty(found.Hits);
            m.ShowHit(0);
            await m.ShowMatching();
            Assert.StartsWith(PseudoLang.Open.ToString(), m.ZoneInfo.Text);
            var data = Data(w, found);
            Assert.Empty(PseudoLang.Untranslated(m.View, [.. data]));

            // The zone filter's messages and its question (with the ground undone too).
            await m.UnmarkAll();
            m.RMinBox.Text = "99999";
            await m.MarkMatching();
            m.RMinBox.Text = "abc";
            await m.MarkMatching();
            m.RMinBox.Text = "";
            m.NoBuildBox.IsChecked = m.OnlyGenBox.IsChecked = m.NoEditBox.IsChecked = false;
            m.GroundBox.IsChecked = true;
            await m.MarkMatching();
            Assert.Equal(4, told.Count);
            Assert.All(told, t => Assert.True(PseudoLang.Translated(t, data), t));
        }
        finally
        {
            w.CloseWithoutAsking();
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task TheMapsMessagesAreCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        string dir = Fixtures.CopyFixture();
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        try
        {
            w.Show();
            await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
            var m = w.MapPage!;
            Assert.Equal(PseudoLang.Wrap("Everything is saved."), m.Pending.Text);
            Assert.Equal(PseudoLang.Wrap("Not saved yet: " + PseudoLang.Wrap("2 zones of ground") + ", " + PseudoLang.Wrap("1 deleted") + "."),
                MapPage.PendingText((2, 1, 0, 0), live: false));
            Assert.Equal(PseudoLang.Wrap("Not applied yet: " + PseudoLang.Wrap("1 zone of ground") + "."), MapPage.PendingText((1, 0, 0, 0), live: true));

            // The search: nothing found, a search that fails, one the world changed under, and what was found.
            m.SearchBox.Text = "no such thing at all";
            await m.Search();
            Assert.Equal(PseudoLang.Wrap("Nothing found."), m.SearchInfo.Text);
            m.RunSearch = (_, _, _) => Task.FromException<WorldSearch.Result>(new IOException("the disk is gone"));
            await m.Search();
            Assert.Equal(PseudoLang.Wrap("Search failed: the disk is gone"), m.SearchInfo.Text);
            var held = new TaskCompletionSource<WorldSearch.Result>();
            m.RunSearch = (_, _, _) => held.Task;
            var running = m.Search();
            Assert.Equal(PseudoLang.Wrap("Searching…"), m.SearchInfo.Text);
            m.Show(w.World!);
            Assert.Equal(PseudoLang.Wrap("The world changed during the search: search again."), m.SearchInfo.Text);
            held.SetResult(new WorldSearch.Result([], [], 0, false));
            await running;
            var found = new WorldSearch.Result([new(1, "Beech", 0, 0, 0, null)], new() { ["Beech"] = 1 }, 1, true);
            Assert.Equal(PseudoLang.Wrap("1 Beech") + "\n"
                + PseudoLang.Wrap("1 found in all, the first 1 pinned. Click one to go there."), MapPage.Found(found, "items"));
        }
        finally
        {
            w.CloseWithoutAsking();
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    // The map's build status while it builds, and when it cannot be built.
    [AvaloniaFact]
    public async Task TheMapsBuildStatusIsCatalogueText()
    {
        using var pseudo = new PseudoLang();
        string dir = Fixtures.CopyFixture();
        try
        {
            using var session = WorldSession.Open(dir);
            using var map = new MapView { BuildGlobal = _ => throw new InvalidDataException("a broken zone") };
            var said = new List<string>();
            map.Status += said.Add;
            map.Show(session);
            await TestWindows.Until(() => said.Count == 2);
            Assert.Equal([PseudoLang.Wrap("Building the world map (same as the game does)…"), PseudoLang.Wrap("Could not build the world map: a broken zone")], said);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }

    // A live world: the players line with players, without any, and when the game does not answer; all applied.
    [AvaloniaFact]
    public async Task ALiveWorldsLinesAreCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        foreach (var (players, line) in new[]
        {
            ("[{\"name\":\"Cyd\",\"x\":0,\"y\":30,\"z\":0,\"yaw\":0}]", "Live world · 1 player(s) online: Cyd"),
            ("[]", "Live world · 0 player(s) online"),
            ("this is not json", "Live world · the game does not answer right now."),
        })
        {
            using var game = new FakeGame { Players = players };
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            await w.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label()), "Connecting…");
            await TestWindows.Until(() => w.MapPage!.LiveText.Text == PseudoLang.Wrap(line));
            Assert.Equal(PseudoLang.Wrap("Everything is applied."), w.MapPage!.Pending.Text);
            w.MapPage.Stop();
            w.CloseWithoutAsking();
        }
    }
}
