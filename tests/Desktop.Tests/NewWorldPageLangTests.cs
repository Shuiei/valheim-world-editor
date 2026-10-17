using Avalonia.Controls;
using Avalonia.Headless.XUnit;
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
            Assert.Empty(PseudoLang.Untranslated(w, data));
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
}
