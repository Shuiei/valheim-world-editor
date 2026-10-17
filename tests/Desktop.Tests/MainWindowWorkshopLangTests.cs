using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Workshop in the window in another language (the made-up one: PseudoLang): its title, cost line, save bar,
/// support switch, cut and back button show only catalogue texts, blank and with a blueprint opened; a message is shown
/// in the language while Claude reads it in English, as is each of its other messages (added, failures, a drop of other
/// files, a second open, a dungeon that could not be put down) and its History steps; and every question it asks (nothing to save, pieces that would
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
        var w = await r.OpenWorkshop();
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
        var w = await r.OpenWorkshop(BlueprintFiles.Hut(r.Homestead));
        Assert.StartsWith(PseudoLang.Wrap("Opened “Hut”: 3 piece(s)."), w.StatusMessage);
        Assert.Equal(
            "Opened “Hut”: 3 piece(s). 1 kind(s) the game does not know were left out (mods?): no_such_piece_xyz. Change it, then Save blueprint.",
            w.LastMessageEn);
    }

    /// <summary>
    /// The window's last message: shown in the language (every sentence, values inside), read by Claude in English.
    /// </summary>
    /// <param name="w">The window.</param>
    /// <param name="english">How the English starts.</param>
    private static void SaidInTheLanguage(MainWindow w, string english)
    {
        Assert.True(PseudoLang.Translated(w.StatusMessage, Data), w.StatusMessage);
        Assert.StartsWith(english, w.LastMessageEn);
        Assert.DoesNotContain(PseudoLang.Open, w.LastMessageEn);
    }

    [AvaloniaFact]
    public async Task TheWorkshopsOtherMessagesAndStepsAreInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        using var r = new BlueprintsPanelRun();
        string hut = BlueprintFiles.Hut(r.Homestead);
        var w = await r.OpenWorkshop(hut);
        var s = w.Session!;
        Assert.Equal(PseudoLang.Wrap("Opened Hut"), s.UndoList[^1].Label);
        // Added beside it: its own step.
        var anchor = Workshop.Anchor(s.Scene);
        w.AddToWorkshop(hut, new Vector2(anchor.X + 10, anchor.Z));
        SaidInTheLanguage(w, "Added “Hut”: ");
        Assert.Equal(PseudoLang.Wrap("Added Hut"), s.UndoList[^1].Label);
        // A blueprint that is not there, added or opened.
        string missing = Path.Combine(r.Dir, "missing.blueprint");
        w.AddToWorkshop(missing, null);
        SaidInTheLanguage(w, "Could not add that blueprint: ");
        await w.OpenWorkshop(missing);
        SaidInTheLanguage(w, "Could not open that blueprint: ");
        // Other files dropped on the view.
        w.Drop(null, [Path.Combine(r.Dir, "notes.txt")], new Avalonia.Point(0, 0), new Avalonia.Size(0, 0));
        SaidInTheLanguage(w, "Drop .blueprint (Homestead, PlanBuild) or .vbuild files.");
        // A second open while one waits on its question (the building is not saved).
        var answer = new TaskCompletionSource<bool>();
        w.Ask = (_, _, _, _) => answer.Task;
        var first = w.OpenWorkshop(null);
        await w.OpenWorkshop(null);
        SaidInTheLanguage(w, "The Workshop is already opening.");
        answer.SetResult(false);
        await first;
        // A dungeon that cannot be written (it has no objects list).
        await w.PlotDungeon(new DungeonGen.Settings(), new DungeonGen.Result(null!, Vector3.Zero, null, null, [], "Broken"), add: true);
        SaidInTheLanguage(w, "Could not put the dungeon on the plot: ");
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
            var w = await TestWindows.ChangedWorld(dir);
            var asked = new List<string>();
            w.Ask = (title, text, yes, no) => { asked.AddRange([title, text, yes, no]); return Task.FromResult(false); };
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
