using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The View panel, the help card and the status bar's readouts in another language (the made-up one: PseudoLang),
/// with an area open and objects selected: they show only catalogue texts but for the world's builders (data); the
/// 3D resolution's message is shown in the language while Claude reads its English; the question before a save is
/// asked in it; and the walking and flying lines, a kind switched on, picking a kind and the paste height are said in
/// it with English for Claude; and so is the help card's frame rate line, given a report as the 3D view gives it.
/// </summary>
[Collection("DataDir")]
public sealed class MainWindowViewLangTests
{
    [AvaloniaFact]
    public async Task ViewPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        using (var r = new WorldWindowRun())
        {
            await r.Open();
            string[] builders = [.. Builders.Players(r.W.Session!.Scene.World!).Select(p => p.Label)];
            Assert.Empty(PseudoLang.Untranslated(r.W.ViewPanelCard, builders));
            Assert.Empty(PseudoLang.Untranslated(r.W.HelpCard));

            // The message names the list's choice in each language.
            r.W.ResolutionBox.SelectedIndex = 2;
            var (width, h) = r.W.View.RenderSize();
            Assert.Equal(PseudoLang.Wrap($"3D resolution: {PseudoLang.Wrap("Fast")}, {width}×{h} pixels."), r.W.MessageText.Text);
            Assert.Equal($"3D resolution: Fast, {width}×{h} pixels.", r.W.LastMessageEn);

            // The status bar with a selection said in it.
            r.W.View.Select([0, 1]);
            var bar = (Control)r.W.MessageText.Parent!;
            Assert.Empty(PseudoLang.Untranslated(bar));
        }

        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        var saving = w.ConfirmSave(Lang.T("common.keepThem"));
        var d = TestWindows.Shown(w);
        Assert.Empty(PseudoLang.Untranslated(d));
        DialogsTests.Press(d, Lang.T("common.cancel"));
        Assert.False(await saving);
        w.Close();
    }

    [AvaloniaFact]
    public async Task ViewMessagesAreSaidInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        using var r = new WorldWindowRun();
        await r.Open();
        var w = r.W;

        // Walking, then flying: the status bar's line in the language.
        var bar = (Control)w.MessageText.Parent!;
        foreach (var eye in new[] { GlView.EyeMode.Walk, GlView.EyeMode.Fly })
        {
            w.View.CycleEye();
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(eye, w.View.Eye);
            Assert.Empty(PseudoLang.Untranslated(bar));
        }

        // A kind switched off in View, placed: switched on, its name and the sentence in the language.
        w.KindBoxes[ObjectKind.Pickables].IsChecked = false;
        var s = w.Session!;
        var stone = new NewObject(0, StableHash.Of("Pickable_Stone"), new Vector3(s.Scene.X0 * 64 - 12, 30, s.Scene.Z0 * 64 - 12), Vector3.Zero, 1f);
        s.Commit("Placed", null, [], [(stone, false)]);
        Dispatcher.UIThread.RunJobs();
        // (Said after the message before it.)
        Assert.EndsWith(PseudoLang.Wrap($"Switched on {PseudoLang.Wrap("Pickables")} in View, so what you placed stays visible."), w.MessageText.Text);
        Assert.EndsWith("Switched on Pickables in View, so what you placed stays visible.", w.LastMessageEn);

        // Picking a kind (for the panels' Replace): asked, nothing picked, cancelled.
        const string label = "Replace";
        w.PickKind(label, _ => { });
        Assert.Equal(PseudoLang.Wrap("Click an object to pick its kind for Replace. Esc cancels."), w.MessageText.Text);
        Assert.Equal("Click an object to pick its kind for Replace. Esc cancels.", w.LastMessageEn);
        w.View.PickObjectOnce!(null);
        Assert.Equal(PseudoLang.Wrap("Nothing picked: click right on an object (only things that are shown can be picked)."), w.MessageText.Text);
        Assert.Equal("Nothing picked: click right on an object (only things that are shown can be picked).", w.LastMessageEn);
        w.PickKind(label, _ => { });
        w.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        Assert.Equal(PseudoLang.Wrap("Picking cancelled."), w.MessageText.Text);
        Assert.Equal("Picking cancelled.", w.LastMessageEn);

        // Ctrl + wheel in the Paste tool: the paste height.
        w.View.Paste.Clip = BlueprintsPanelRun.Clip("Hut", "Beech1");
        w.Tools.ChooseMode(ToolMode.Paste);
        w.PastePanel.OffsetBox.Value = 0;
        Assert.True(w.View.CtrlWheel!(1, false));
        Assert.Equal(PseudoLang.Wrap("Paste height: +0.5 m from the ground clicked (Ctrl + wheel; Shift: 0.1 m)."), w.MessageText.Text);
        Assert.Equal("Paste height: +0.5 m from the ground clicked (Ctrl + wheel; Shift: 0.1 m).", w.LastMessageEn);
    }

    [AvaloniaFact]
    public void TheFrameRateLineIsInTheLanguage()
    {
        using var pseudo = new PseudoLang();
        var w = new MainWindow(load: false) { Width = 1200, Height = 900 };
        w.Show();
        const string Rate = "60 frames/s · 4.5 ms of work each · 1,234 objects (5,678 model parts in 90 draws)";
        string Line() => Assert.Single(w.HelpCard.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text?.Contains("frames/s", StringComparison.Ordinal) == true).Text!;

        // Model kinds still loading: said after the rates, in the language too.
        w.ShowStats(new GlView.Stats(60, 4.5, 1234, 90, 5678, 3));
        Assert.Equal(PseudoLang.Wrap(Rate) + " " + PseudoLang.Wrap("· 3 kinds loading"), Line());
        Assert.Empty(PseudoLang.Untranslated(w.HelpCard));
        // Everything loaded: the rates alone.
        w.ShowStats(new GlView.Stats(60, 4.5, 1234, 90, 5678, 0));
        Assert.Equal(PseudoLang.Wrap(Rate), Line());
        w.Close();
    }
}
