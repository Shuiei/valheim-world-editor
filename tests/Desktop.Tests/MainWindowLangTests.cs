using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The status bar in another language (the made-up one: PseudoLang): a message is shown in it while its English is
/// kept for Claude, who reads it back in editor_state; a message added to keeps each side's language; a text put
/// there directly is the same for both. The questions about saving, discarding, leaving and quitting are catalogue
/// texts, titles and buttons too.
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowLangTests
{
    [AvaloniaFact]
    public async Task TheStatusBarShowsTheLanguageAndClaudeReadsEnglish()
    {
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        // What the window posts when it opens says nothing over the messages below.
        Dispatcher.UIThread.RunJobs();
        var tools = new ClaudeTools(w);
        using var pseudo = new PseudoLang();

        w.Msg("settings.worldNotFound", "/x");
        Assert.Equal("⟦World folder not found: /x⟧", w.MessageText.Text);
        Assert.Equal("World folder not found: /x", w.LastMessageEn);
        Assert.Equal("World folder not found: /x", ClaudeRun.J(await tools.EditorState()).GetProperty("message").GetString());

        w.Msg(w.LastSaid + Said.Of("common.save"));
        Assert.Equal("⟦World folder not found: /x⟧ ⟦Save⟧", w.MessageText.Text);
        Assert.Equal("World folder not found: /x Save", w.LastMessageEn);

        // An exception's message, put there as it is.
        w.MessageText.Text = "disk full";
        Assert.Equal("disk full", w.LastMessageEn);
        Assert.Equal(new Said("disk full", "disk full"), w.LastSaid);
    }

    // Save with nothing to save, then Save, Discard, Leave and Quit with a change waiting: what each asks or says, its
    // title and its buttons, are catalogue texts (the world's folder in the save's question is data).
    [AvaloniaFact]
    public async Task TheSaveAndLeaveQuestionsAreCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        using var r = new WorldWindowRun();
        var asked = new List<string>();
        r.W.Ask = (title, text, yes, no) =>
        {
            asked.AddRange([title, text, yes, no]);
            return Task.FromResult(false);
        };
        r.W.ConfirmSave = text =>
        {
            asked.Add(text);
            return Task.FromResult(false);
        };
        r.W.Tell = text =>
        {
            asked.Add(text);
            return Task.CompletedTask;
        };
        await r.Open();
        await r.W.Save();
        r.Raise();
        await r.W.Save();
        r.W.ShowMap();
        await r.W.DiscardWorld();
        await r.W.LeaveWorld();
        r.W.Close();
        Dispatcher.UIThread.RunJobs();
        // Nothing to save, the save's question, then three questions of four parts each.
        Assert.Equal(14, asked.Count);
        string folder = Ui.Tilde(r.W.World!.World.Directory);
        Assert.All(asked, t => Assert.True(PseudoLang.Translated(t, [folder]), t));
        Assert.True(r.W.IsVisible);
    }

    // What a save and an Apply live did (the writer's and the live sync's summaries in them too) is shown in the
    // language in use, and Claude reads it in English.
    [AvaloniaFact]
    public async Task ASaveAndAnApplySayWhatTheyDidInTheLanguageAndInEnglish()
    {
        using var pseudo = new PseudoLang();
        using (var r = new WorldWindowRun())
        {
            await r.Open();
            r.Raise();
            await r.W.Save();
            Assert.StartsWith(PseudoLang.Open.ToString(), r.W.MessageText.Text);
            Assert.Matches(@"^Saved 1 zone\(s\) to save #\d+\.$", r.W.LastMessageEn);
            Assert.True(PseudoLang.Translated(Assert.Single(r.Told), []), r.Told[0]);
        }
        using (var r = new WorldWindowRun())
        {
            await r.OpenLive();
            var s = r.W.Session!;
            int thing = s.Scene.Things.FindIndex(t => !t.Gone);
            Assert.True(thing >= 0);
            s.Delete([thing]);
            await r.W.ApplyNow();
            Assert.StartsWith(PseudoLang.Open.ToString(), r.W.MessageText.Text);
            Assert.Matches(@"^Applied to the running game: \d+ object\(s\) removed, 0 created", r.W.LastMessageEn);
            Assert.DoesNotContain(PseudoLang.Open, r.W.LastMessageEn);
        }
    }
}
