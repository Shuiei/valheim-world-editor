using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using ValheimGen;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The New world page in another language (the made-up one: PseudoLang): a seed's world with its lists, landmarks and
/// dungeons, a search's results and a name refused show only catalogue texts, but for the data (seeds, folders); what
/// Claude is told when it makes a world (a refusal, the world made) stays English while the window shows the language.
/// </summary>
[Collection("DataDir")]
public sealed class NewWorldPageLangTests
{
    /// <summary>A seed whose world the core's tests know (landmarks and dungeons from the test fixtures' rules).</summary>
    private const string Seed = "5DCcdIcuYJ";

    /// <summary>How long a seed's world may take to show, in milliseconds (as NewWorldPageTests waits).</summary>
    private const int SeedWait = 180000;

    [AvaloniaFact]
    public async Task NewWorldPageShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        Fixtures.UseLocations();
        string root = Path.Combine(Path.GetTempPath(), "vwe-newworld-lang-" + Guid.NewGuid().ToString("N")[..8]);
        string[] folders = [Path.Combine(root, "local"), Path.Combine(root, "cloud")];
        foreach (string f in folders)
        {
            Directory.CreateDirectory(f);
        }
        NewWorldPage? made = null;
        NewWorldPageTests.OnUi(() => made = new NewWorldPage(folders, previewSize: 64));
        var page = made!;
        var w = new Window { Width = 1600, Height = 1000, Content = page.View };
        w.Show();
        NewWorldPageTests.Settle();
        try
        {
            // A seed's world with its lists and landmarks, its dungeons found and a kind of them on the map.
            NewWorldPageTests.OnUi(() => page.ShowSeed(Seed));
            await TestWindows.Until(() => page.Shown?.Seed == Seed && !page.Loading.IsVisible, SeedWait);
            await TestWindows.Until(() => page.DungeonMarks != null, 120000);
            page.ShowDungeons["Sunken crypts"].IsChecked = true;
            // A search stopped part way: its results, with their scores and tooltips.
            page.Count.SelectedIndex = 0;
            NewWorldPageTests.Click(page.Find);
            await TestWindows.Until(() => page.Progress.Value > 0, 120000);
            NewWorldPageTests.Click(page.Stop);
            await TestWindows.Until(() => page.Find.IsEnabled, 120000);
            Assert.NotEmpty(page.Results);
            // A name refused: why, in the language in use.
            page.NameBox.Text = "a/b";
            page.AskCreate();
            Assert.StartsWith(PseudoLang.Open + "Give the world a name", page.Problem.Text);
            NewWorldPageTests.Settle();

            string[] data = [Seed, .. page.Results.Select(r => r.Preview.Seed), .. folders.Select(Ui.Tilde)];
            Assert.Empty(PseudoLang.Untranslated(page.View, data));
        }
        finally
        {
            page.Dispose();
            w.Close();
            Directory.Delete(root, true);
        }
    }

    // Claude's create_world: a name refused (NewWorldPage.Check), the world made and a name taken are told in English;
    // the window says the world was made in the language in use, in the status bar and in a dialog over the map.
    [AvaloniaFact]
    public async Task ClaudeIsToldInEnglishWhatTheWindowSaysInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        using var r = new ClaudeRun();
        var told = new List<string>();
        r.W.Tell = text => { told.Add(text); return Task.CompletedTask; };
        string root = Path.Combine(Path.GetTempPath(), "vwe-newworld-lang-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        r.T.WorldFolders = () => [root];
        try
        {
            string refused = await r.T.CreateWorld("a/b", "abc", root);
            Assert.StartsWith("Give the world a name that can be a file name", refused);
            Assert.DoesNotContain(PseudoLang.Open, refused);

            string made = await r.T.CreateWorld("Fresh", "abc", root);
            Assert.StartsWith("Made Fresh (seed abc).", made);
            Assert.DoesNotContain(PseudoLang.Open, made);
            Assert.StartsWith(PseudoLang.Open + "Made Fresh (seed abc).", r.W.MessageText.Text);
            Assert.True(PseudoLang.Translated(Assert.Single(told), ["Fresh", "abc"]));

            string taken = await r.T.CreateWorld("Fresh", "xyz", root);
            Assert.StartsWith("There is a world called Fresh already in ", taken);
            Assert.DoesNotContain(PseudoLang.Open, taken);
        }
        finally
        {
            await r.W.LeaveWorld();
            Directory.Delete(root, true);
        }
    }

    // The page's own names and distances: each of the game's dungeon kinds, landmarks and biomes is a catalogue text
    // (an id not named stays as it is), and a distance is written in the language in use.
    [AvaloniaFact]
    public void NamesAndDistancesComeFromTheCatalogue()
    {
        using var pseudo = new PseudoLang();
        foreach (var (name, _, _) in SeedPreview.Dungeons)
        {
            Assert.Equal(PseudoLang.Wrap(name), NewWorldPage.DungeonName(name));
        }
        string[] landmarks = ["The start", "Haldor", "The Bog Witch", "Hildir", "Eikthyr", "The Elder", "Bonemass", "Moder", "Yagluth", "The Queen", "Fader"];
        foreach (string name in landmarks)
        {
            Assert.Equal(PseudoLang.Wrap(name), NewWorldPage.LandmarkName(name));
        }
        Assert.Equal("Mystery", NewWorldPage.DungeonName("Mystery"));
        Assert.Equal("Mystery", NewWorldPage.LandmarkName("Mystery"));
        Assert.Equal(PseudoLang.Wrap("Black Forest"), NewWorldPage.Name(Heightmap.Biome.BlackForest));
        Assert.Equal(PseudoLang.Wrap("< 100 m"), NewWorldPage.Km(40));
        Assert.Equal(PseudoLang.Wrap(Lang.English["newWorld.none"]), NewWorldPage.Km(float.PositiveInfinity));
        foreach (float m in new[] { 420f, 1530f })
        {
            Assert.True(PseudoLang.Translated(NewWorldPage.Km(m), []));
        }
    }

    // A seed refused and a world that could not be made: the page says why in the language in use, Claude (Check's
    // and CreateWorld's answers) is told in English.
    [AvaloniaFact]
    public async Task RefusalsAndFailuresAreShownInTheLanguageAndToldInEnglish()
    {
        using var pseudo = new PseudoLang();
        string dir = Fixtures.CopyFixture();
        var w = MainWindowNewWorldTests.OnNewWorldPage();
        try
        {
            var page = w.NewWorld!;
            page.ShowSeed("ABCDEFGHIJK");
            Assert.Empty(PseudoLang.Untranslated(page.Info));
            Assert.Equal(PseudoLang.Wrap(Lang.English["newWorld.error.seedLength"]), NewWorldPage.Check(Path.GetTempPath(), "Fine", ""));
            Assert.Equal(Lang.English["newWorld.error.seedLength"], NewWorldPage.Check(Path.GetTempPath(), "Fine", "", english: true));

            // The folder already holds a world: the game's files are never written over.
            string failed = Assert.IsType<string>(await w.CreateWorld(dir, "CITest", "abc"));
            Assert.StartsWith("Could not make the world:", failed);
            Assert.DoesNotContain(PseudoLang.Open, failed);
            Assert.StartsWith(PseudoLang.Open + "Could not make the world:", page.Problem.Text);
        }
        finally
        {
            w.CloseWithoutAsking();
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
        }
    }
}
