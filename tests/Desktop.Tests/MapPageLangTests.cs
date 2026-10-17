using Avalonia.Headless.XUnit;
using TerrainEditor.App;
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
}
