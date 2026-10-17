using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.Terrain;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Workshop in the window in another language (the made-up one: PseudoLang): its title, cost line, save bar,
/// support switch, cut and back button show only catalogue texts, blank and with a blueprint opened; a message is shown
/// in the language while Claude reads it in English; and every question it asks (nothing to save, pieces that would
/// fall, another blueprint or the start page over a building not saved, a world with changes left) is in the language.
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowWorkshopLangTests
{
    /// <summary>
    /// The test blueprint's name and its unknown kind: data, the same in every language.
    /// </summary>
    private static readonly string[] Data = ["Hut", "no_such_piece_xyz"];

    /// <summary>
    /// What the Workshop shows in the window that did not come from the catalogue.
    /// </summary>
    /// <param name="w">The window, in the Workshop.</param>
    /// <param name="data">Texts that are data.</param>
    private static List<string> Untranslated(MainWindow w, string[] data) =>
    [
        .. new Control[] { w.TitleText, w.SubtitleText, w.PendingText, w.SaveButton, w.SupportBox, w.CutBox, w.MapButton }
            .SelectMany(c => PseudoLang.Untranslated(c, data)),
    ];

    [AvaloniaFact]
    public async Task TheWorkshopShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        w.Tell = _ => Task.CompletedTask;
        await w.OpenWorkshop(null);
        Assert.Empty(Untranslated(w, Data));

        await w.OpenWorkshop(BlueprintFiles.Hut(r.Homestead));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, w.LastSupport!.Breaking);
        w.CutSlider.Value = 3;
        // The cost's tip lists the materials by their names (game data).
        string[] data = [.. Data, .. PieceCost.Of(Workshop.Kinds(w.Session!.Scene)).Materials.Select(m => m.Name)];
        Assert.Empty(Untranslated(w, data));

        // Changed since opened: not saved.
        w.Session.Commit("Moved", null, [w.Session.Scene.Things.FindIndex(t => !t.Gone)], []);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(Untranslated(w, data));
    }

    [AvaloniaFact]
    public async Task AMessageIsShownInTheLanguageAndClaudeReadsItInEnglish()
    {
        using var pseudo = new PseudoLang();
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        await w.OpenWorkshop(BlueprintFiles.Hut(r.Homestead));
        Assert.StartsWith(PseudoLang.Wrap("Opened “Hut”: 3 piece(s)."), w.StatusMessage);
        Assert.Equal(
            "Opened “Hut”: 3 piece(s). 1 kind(s) the game does not know were left out (mods?): no_such_piece_xyz. Change it, then Save blueprint.",
            w.LastMessageEn);
    }

    [AvaloniaFact]
    public async Task TheWorkshopsQuestionsAreInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        using var r = new BlueprintsPanelRun();
        var w = r.W;
        var asked = new List<string>();
        w.Ask = (title, text, yes, no) => { asked.AddRange([title, text, yes, no]); return Task.FromResult(false); };
        w.Tell = text => { asked.Add(text); return Task.CompletedTask; };
        // Nothing to save.
        await w.OpenWorkshop(null);
        await w.SaveWorkshop();
        // A piece would fall.
        await w.OpenWorkshop(BlueprintFiles.Hut(r.Homestead));
        await w.SaveWorkshop();
        // Not saved: another blueprint, then the start page.
        w.Session!.Commit("Moved", null, [w.Session.Scene.Things.FindIndex(t => !t.Gone)], []);
        await w.OpenWorkshop(null);
        await w.LeaveWorkshop();
        Assert.True(w.InWorkshop);
        Assert.Equal(1 + 3 * 4, asked.Count);
        Assert.All(asked, text => Assert.True(PseudoLang.Translated(text, Data), text));
    }

    [AvaloniaFact]
    public async Task LeavingAWorldWithChangesForTheWorkshopIsAskedInTheLanguage()
    {
        string dir = Fixtures.CopyFixture();
        try
        {
            using var pseudo = new PseudoLang();
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            var asked = new List<string>();
            w.Ask = (title, text, yes, no) => { asked.AddRange([title, text, yes, no]); return Task.FromResult(false); };
            w.Tell = _ => Task.CompletedTask;
            await w.OpenWorld(() => Task.Run(() => WorldSession.Open(dir)), "Opening…");
            await w.EditArea(0, 0, 1);
            w.Session!.Shape(32, 32, Formula.Compile("2", []), 3, 0, "raise");
            await w.OpenWorkshop(null);
            Assert.False(w.InWorkshop);
            Assert.Equal(4, asked.Count);
            Assert.All(asked, text => Assert.True(PseudoLang.Translated(text, []), text));
        }
        finally
        {
            string folder = Path.GetDirectoryName(dir)!;
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
