using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TerrainEditor.App;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area tool in another language (the made-up one: PseudoLang): its panel with no selection (box and polygon),
/// with a selection under every action, with a heightmap read, and its Reset zones question show only catalogue
/// texts; a message's English is kept for Claude, and Claude's own ground changes are named to Claude in English (the
/// History shows the step in the language, behind its "Claude: " mark).
/// </summary>
[Collection("DataDir")]
public sealed class AreaPanelLangTests
{
    /// <summary>Clicks the button and runs the posted work it starts.</summary>
    private static void Click(Button b)
    {
        b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task AreaPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        string dir = Fixtures.CopyFixture();
        string maps = Path.Combine(Path.GetTempPath(), "vwe-area-lang-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            var scene = WorldScene.Load(dir, 0, 0, 1);
            w.Show();
            w.View.Show(scene, null);
            w.Edit(scene.Session!);
            w.Tools.ChooseMode(ToolMode.Area);
            var p = w.AreaPanel;
            void Check()
            {
                Dispatcher.UIThread.RunJobs();
                // The prefab names of the Replace lists are data, shown as they are.
                string[] data = [.. new[] { p.FromBox, p.ToBox }.SelectMany(b => b.Items.OfType<string>())];
                Assert.Empty(PseudoLang.Untranslated(p.Card, data));
            }

            // No selection: how to make one, as a box and as a polygon.
            Check();
            Click(p.PolyButton);
            Check();
            Click(p.BoxButton);
            var points = w.View.Area.Points;
            lock (points)
            {
                points.Add(new Vector2(2, 2));
                points.Add(new Vector2(62, 62));
            }
            w.View.Area.Notify();
            foreach (var act in Enum.GetValues<AreaPanel.Act>())
            {
                p.Choose(act);
                Check();
            }

            // A heightmap read: what it is fitted to.
            p.Choose(AreaPanel.Act.Heightmap);
            var written = p.ExportHeightmap(maps)!;
            p.LoadPicture(Directory.GetFiles(maps).Single());
            Check();
            Assert.StartsWith(PseudoLang.Open.ToString(), written.Shown);

            // A message: shown in the language, its English kept for Claude.
            Click(p.ClearButton);
            Assert.Equal(PseudoLang.Wrap("Selection cleared."), w.MessageText.Text);
            Assert.Equal("Selection cleared.", w.LastMessageEn);

            // The Reset zones question.
            lock (points)
            {
                points.Add(new Vector2(2, 2));
                points.Add(new Vector2(62, 62));
            }
            w.View.Area.Notify();
            p.Choose(AreaPanel.Act.Reset);
            var applied = p.Apply();
            var d = TestWindows.Shown(w);
            Assert.Empty(PseudoLang.Untranslated(d));
            DialogsTests.Press(d, Lang.T("common.cancel"));
            await applied;
            Assert.Empty(scene.Session!.Resets);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(dir)!, recursive: true);
            if (Directory.Exists(maps))
            {
                Directory.Delete(maps, recursive: true);
            }
        }
    }

    [AvaloniaFact]
    public async Task ClaudesGroundChangesAreNamedInEnglish()
    {
        using var r = new ClaudeRun();
        await r.OpenArea();
        using var pseudo = new PseudoLang();
        string said = await r.T.ShapeGround("restore", -10, -10, 10, 10, edge: 0);
        Assert.StartsWith("Restore the ground: ", said);
        Assert.DoesNotContain(PseudoLang.Open, said[..said.IndexOf(" Now pending", StringComparison.Ordinal)]);
        // The History names the step in the language, behind the English "Claude: " mark it is found by.
        Assert.Equal("Claude: " + PseudoLang.Wrap("restore the ground"), r.W.Session!.UndoLabel);
    }
}
