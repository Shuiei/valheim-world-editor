using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The status bar in another language (the made-up one: PseudoLang): a message is shown in it while its English is
/// kept for Claude, who reads it back in editor_state; a message added to keeps each side's language; a text put
/// there directly is the same for both. The questions about saving, discarding, leaving and quitting are catalogue
/// texts, titles and buttons too. The Script tool's question and its run's messages are in the language, what
/// Claude's run_script is told in English. The brush's messages from the window are in the language and read in
/// English.
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

    // The questions and messages the start page, the map and the window itself show in a dialog of the window's: the
    // title is the editor's name (the same in every language), the rest catalogue texts, Yes, Cancel and OK included.
    [AvaloniaFact]
    public async Task ThePagesDialogsAreCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        static async Task Answered(Avalonia.Controls.Window owner, Task asking, string button)
        {
            var d = TestWindows.Shown(owner);
            Assert.Empty(PseudoLang.Untranslated(d, "Valheim World Editor"));
            DialogsTests.Press(d, button);
            await asking;
        }
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        w.ShowStart();
        await Answered(w, w.StartPage!.Confirm(Lang.T("common.keepThem")), Lang.T("common.cancel"));
        await Answered(w, w.Tell(Lang.T("common.keepThem")), Lang.T("common.ok"));

        using var r = new WorldWindowRun();
        await r.Open();
        r.W.ShowMap();
        await Answered(r.W, r.W.MapPage!.Confirm(Lang.T("common.keepThem")), Lang.T("common.cancel"));
        await Answered(r.W, r.W.MapPage.Tell(Lang.T("common.keepThem")), Lang.T("common.ok"));
    }

    // A save after the game saved the world, with an object the editor moved and the game removed: the offer to bring
    // the changes over (not now, then yes), whose version stays for that object, what bringing them over did, and then
    // undoing a step from before it. Each question with its title and buttons, and what is said or told, is a catalogue
    // text (the object's name in the question is game data).
    [AvaloniaFact]
    public async Task TheReplayAndEarlierStepQuestionsAreCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        using var r = new WorldWindowRun();
        var asked = new List<string>();
        // Not now; bring them over; keep mine; do not undo the earlier step.
        var answers = new Queue<bool>([false, true, true, false]);
        r.W.Ask = (title, text, yes, no) =>
        {
            asked.AddRange([title, text, yes, no]);
            return Task.FromResult(answers.Dequeue());
        };
        await r.Open();
        var s = r.W.Session!;
        var thing = TestScenes.MovedThenRemovedByTheGame(s, r.Dir);
        string[] data = [r.W.NameOfPrefab(thing.Prefab) ?? thing.Prefab.ToString(System.Globalization.CultureInfo.InvariantCulture)];

        await r.W.Save();
        Assert.True(PseudoLang.Translated(r.Said, data), r.Said);
        Assert.StartsWith("Nothing was saved: the game saved this world", r.W.LastMessageEn);
        await r.W.Save();
        await r.W.Undo();
        Assert.Equal("Nothing changed.", r.W.LastMessageEn);

        // The offer twice, the object's question, the earlier step's question.
        Assert.Equal(4 * 4, asked.Count);
        Assert.Empty(answers);
        Assert.All(asked, t => Assert.True(PseudoLang.Translated(t, data), t));
        string told = Assert.Single(r.Told);
        Assert.True(PseudoLang.Translated(told, data), told);
        Assert.True(PseudoLang.Translated(r.Said, data), r.Said);
    }

    // A file chosen as the brush's stamp that is not a picture: said in the language in use, read in English, and no
    // stamp added.
    [AvaloniaFact]
    public void AStampPictureThatCannotBeReadIsSaidInTheLanguage()
    {
        string file = Path.Combine(Path.GetTempPath(), "vwe-stamp-" + Guid.NewGuid().ToString("N")[..8] + ".png");
        File.WriteAllText(file, "not a picture");
        try
        {
            using var pseudo = new PseudoLang();
            var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
            w.Show();
            Dispatcher.UIThread.RunJobs();
            int stamps = w.Tools.StampList.Count;

            w.LoadStamp(file);

            Assert.Equal(PseudoLang.Wrap("That picture could not be read."), w.MessageText.Text);
            Assert.Equal("That picture could not be read.", w.LastMessageEn);
            Assert.Equal(stamps, w.Tools.StampList.Count);
        }
        finally
        {
            File.Delete(file);
        }
    }

    // A stamp picture that cannot be opened (gone since it was chosen, a folder) or is too large to read: said like one
    // that is not a picture, no stamp added, and logged with what failed but not where the file is.
    [AvaloniaFact]
    public void AStampPictureThatCannotBeOpenedIsSaidAndLoggedWithoutItsPath()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-stamp-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        // Sparse: no disk space taken.
        string large = Path.Combine(dir, "large.png");
        using (var f = File.Create(large))
        {
            f.SetLength(Stamps.MostPictureBytes + 1);
        }
        var stderr = Console.Error;
        using var log = new StringWriter();
        try
        {
            using var pseudo = new PseudoLang();
            var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
            w.Show();
            Dispatcher.UIThread.RunJobs();
            int stamps = w.Tools.StampList.Count;
            Console.SetError(TextWriter.Synchronized(log));

            foreach (string path in new[] { Path.Combine(dir, "gone.png"), dir, large })
            {
                w.MessageText.Text = "";
                w.LoadStamp(path);

                Assert.Equal(PseudoLang.Wrap("That picture could not be read."), w.MessageText.Text);
                Assert.Equal("That picture could not be read.", w.LastMessageEn);
                Assert.Equal(stamps, w.Tools.StampList.Count);
            }
        }
        finally
        {
            Console.SetError(stderr);
            Directory.Delete(dir, recursive: true);
        }
        string logged = log.ToString();
        // (The exceptions' own messages may be in the system's language.)
        Assert.Contains("Load stamp: the stamp picture could not be read: FileNotFoundException: ", logged);
        Assert.Contains("Load stamp: the stamp picture could not be read: UnauthorizedAccessException: ", logged);
        Assert.Contains($"Load stamp: the stamp picture is not read: {Stamps.MostPictureBytes + 1} bytes, more than {Stamps.MostPictureBytes}.", logged);
        Assert.DoesNotContain(dir, logged);
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

    [AvaloniaFact]
    public async Task TheScriptToolSpeaksTheLanguageAndClaudeReadsEnglish()
    {
        string data = Path.Combine(Path.GetTempPath(), "vwe-script-lang-" + Guid.NewGuid().ToString("N")[..8]);
        string? old = AppSettings.DataDirOverride;
        AppSettings.DataDirOverride = data;
        try
        {
            using var pseudo = new PseudoLang();
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            Dispatcher.UIThread.RunJobs();

            await w.RunScript();
            Assert.Equal(PseudoLang.Wrap("Open an area first: a script works on the open area."), w.MessageText.Text);
            Assert.Equal("Open an area first: a script works on the open area.", w.LastMessageEn);

            // Saving over another saved script asks first: its title, question and buttons.
            Directory.CreateDirectory(ScriptPanel.Folder);
            File.WriteAllText(Path.Combine(ScriptPanel.Folder, "mine.csx"), "Print(1);");
            w.ScriptPanel.Fill();
            w.ScriptPanel.NameBox.Text = "mine";
            var saving = w.ScriptPanel.Save();
            var d = TestWindows.Shown(w);
            Assert.Empty(PseudoLang.Untranslated(d));
            DialogsTests.Press(d, Lang.T("common.cancel"));
            await saving;
            Assert.Equal("Print(1);", File.ReadAllText(Path.Combine(ScriptPanel.Folder, "mine.csx")));

            // A run: what became of it is shown in the language after what the script printed, and is English for
            // Claude (run_script answers with the English output).
            var s = TestScenes.Flat(2);
            w.View.Show(s.Scene, null);
            w.Edit(s);
            var r = await w.RunScriptCode("Print(\"hi\"); Ground.Raise(32, 32, 1);", "Script: test");
            Assert.True(r.Changed);
            Assert.StartsWith("hi\n" + PseudoLang.Open + "The script changed ", r.Output.Shown);
            Assert.StartsWith("hi\nThe script changed ", r.Output.English);
            Assert.DoesNotContain(PseudoLang.Open, r.Output.English);
            Assert.DoesNotContain(PseudoLang.Open, r.Message.English);
        }
        finally
        {
            AppSettings.DataDirOverride = old;
            if (Directory.Exists(data))
            {
                Directory.Delete(data, true);
            }
        }
    }

    // The brush's messages from the window (Stamp once, No limit, Alt + click to flatten or to set the mask's band) are
    // shown in the language in use and read in English; Stamp once's step is named in it.
    [AvaloniaFact]
    public void TheBrushMessagesAreShownInTheLanguageAndReadInEnglish()
    {
        using var pseudo = new PseudoLang();
        var s = TestScenes.Flat(2);
        var w = TestWindows.Editing(s);
        Dispatcher.UIThread.RunJobs();

        void Said(string english)
        {
            Assert.StartsWith(PseudoLang.Open.ToString(), w.MessageText.Text);
            Assert.Matches(english, w.LastMessageEn);
        }
        // Alt + click on the flat ground (30 m high), with a brush: Flatten to there; with Shift, the mask's band around
        // it (switched off again before the stamp).
        w.Tools.Choose(BrushTool.Raise);
        w.View.SetCamera(new System.Numerics.Vector3(0, 150, 1), System.Numerics.Vector3.Zero, 1.6f);
        var q = System.Numerics.Vector4.Transform(new System.Numerics.Vector4(0, 30, 0, 1), w.View.ViewProj);
        var at = new Avalonia.Point((q.X / q.W + 1) / 2 * 1600, (1 - q.Y / q.W) / 2 * 1000);
        w.MouseDown(at, MouseButton.Left, RawInputModifiers.Alt);
        w.MouseUp(at, MouseButton.Left, RawInputModifiers.Alt);
        Said(@"^Flatten to 30\.0 m\.$");
        w.MouseDown(at, MouseButton.Left, RawInputModifiers.Alt | RawInputModifiers.Shift);
        w.MouseUp(at, MouseButton.Left, RawInputModifiers.Alt | RawInputModifiers.Shift);
        Said(@"^Mask: ground between 28\.0 and 32\.0 m\.$");
        w.MaskPanel.OnBox.IsChecked = false;

        w.Tools.Choose(BrushTool.Raise);
        w.Tools.ShapeBox.SelectedIndex = 4;
        w.StampAt(64, 64);
        Said(@"^Stamped [\d.]+ m up\. Ctrl\+Z undoes it\.$");
        Assert.Equal(PseudoLang.Wrap("Stamp: " + PseudoLang.Wrap("Mountain")), s.UndoLabel);

        w.Tools.NoLimitBox.IsChecked = true;
        Said("^No limit: the ground tools go past the game's ±8 m\\.");
        w.Tools.NoLimitBox.IsChecked = false;
        Said("^The ground tools keep to the game's ±8 m\\.$");
    }
}
