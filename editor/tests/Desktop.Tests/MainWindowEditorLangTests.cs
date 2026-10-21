using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The 3D editor's area bar and live game in another language (the made-up one: PseudoLang): the area's controls,
/// Discard, Reload and Auto, the locations note, the area's line under the title, how it loaded, the live players and
/// the New world page's title show only catalogue texts; a message is shown in the language while Claude reads its
/// English; every question they ask (Discard, the world read again, Reload, Apply over the game's changes) is asked
/// in it; and an area or a world that cannot be opened is said in it.
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowEditorLangTests
{
    [AvaloniaFact]
    public async Task AreaBarShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var asked = new List<string>();
        using (var r = new WorldWindowRun())
        {
            Answer(r, asked);
            await r.W.OpenWorld(() => Task.Run(() => WorldSession.Open(r.Dir)), "Opening…");
            r.W.World!.World.Locations.Add((new Vector3(5, 30, 5), 1));
            await r.W.EditArea(0, 0, 1);
            Assert.True(r.W.LocationNote.IsVisible);
            // The area's line under its name in the top bar.
            string name = r.W.View.Scene!.Name;
            var subtitle = r.W.GetLogicalDescendants().OfType<StackPanel>()
                .Single(p => p.Children is [TextBlock title, TextBlock] && title.Text == name).Children[1];
            foreach (var c in new Control[] { r.W.AreaNav, r.W.DiscardButton, r.W.LocationNote, subtitle, r.W.LoadedInfo })
            {
                Assert.Empty(PseudoLang.Untranslated(c));
            }

            // A message with a reason put in it: shown in the language, read by Claude in English.
            r.W.View.Path.Points.Add(new Vector2(20, 20));
            await r.W.MoveArea(1, 0);
            Assert.Equal(PseudoLang.Wrap($"The area cannot move now: {PseudoLang.Wrap("a path is drawn (apply or clear it)")}."), r.W.MessageText.Text);
            Assert.Equal("The area cannot move now: a path is drawn (apply or clear it).", r.W.LastMessageEn);
            r.W.View.Path.Points.Clear();

            // Discard here, then the change of another area that needs the world read again.
            r.Raise();
            await r.W.DiscardHere();
            await r.W.EditArea(4, 0, 1);
            r.Answers.Enqueue(true);
            await r.W.DiscardHere();
        }
        Assert.Equal(3 * 4, asked.Count);

        using (var r = new WorldWindowRun())
        {
            Answer(r, asked);
            await r.OpenLive();
            await r.W.PollPlayers();
            foreach (var c in new Control[] { r.W.ReloadButton, r.W.AutoApplyBox, r.W.PlayersRow })
            {
                Assert.Empty(PseudoLang.Untranslated(c, "Ada", "Bjorn"));
            }
            r.Raise();
            await r.W.ReloadHere();
            await r.W.ConfirmOverGame([(1, 2), (3, 4)]);
        }
        Assert.Equal(5 * 4, asked.Count);
        Assert.All(asked, text => Assert.True(PseudoLang.Translated(text, []), text));

        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        w.ShowNewWorld();
        Assert.Equal(PseudoLang.Wrap($"Valheim World Editor {BuildInfo.Version} · A new world"), w.Title);
        w.NewWorld!.Dispose();
        w.Close();
    }

    [AvaloniaFact]
    public async Task OpenFailuresAreSaidInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        using (var r = new WorldWindowRun())
        {
            // An area that cannot be loaded: the status bar in the language, Claude's message in English.
            await r.Open();
            await r.W.EditBlock(0, 0, -1, -1);
            Assert.StartsWith("⟦Could not open that area: ", r.Said);
            Assert.StartsWith("Could not open that area: ", r.W.LastMessageEn);

            // From the map, with what was said before the failure: both in the language, in a dialog.
            r.W.ShowMap();
            await r.W.EditBlock(0, 0, -1, -1, context: Said.Of("top.msg.reopenFailed"));
            Assert.StartsWith(
                PseudoLang.Wrap("The world was read again, but the area could not be opened again.") + " ⟦Could not open that area: ",
                Assert.Single(r.Told));
        }

        // A world that cannot be opened: the start page says why in the language.
        using (var r = new WorldWindowRun())
        {
            await r.W.OpenWorld(() => Task.FromException<WorldSession>(new InvalidDataException("not a world")), "Opening…");
            var texts = r.W.StartPage!.View.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text);
            Assert.Contains(PseudoLang.Wrap("Could not open the world: not a world"), texts);
        }
    }

    /// <summary>The window's questions noted (title, text and both buttons) and answered from the run's queue.</summary>
    /// <param name="r">The run.</param>
    /// <param name="asked">Where the questions' texts go.</param>
    private static void Answer(WorldWindowRun r, List<string> asked) =>
        r.W.Ask = (title, text, yes, no) =>
        {
            asked.AddRange([title, text, yes, no]);
            return Task.FromResult(r.Answers.Count > 0 && r.Answers.Dequeue());
        };

    // A world given on the command line (--world) that cannot be opened: said in the language where the editor shows
    // how it loaded.
    [AvaloniaFact]
    public async Task ACommandLineWorldThatCannotOpenIsSaidInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        string folder = Directory.CreateTempSubdirectory("vwe-no-world-").FullName;
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        try
        {
            w.Show();
            await w.OpenDirect(folder, 0, 0, 1);
            Assert.Null(w.Session);
            // Its last line (what the 3D view says when it starts goes above).
            Assert.Matches("⟦Could not open the world: .+⟧$", w.LoadedInfo.Text);
        }
        finally
        {
            w.Close();
            Directory.Delete(folder, true);
        }
    }
}
