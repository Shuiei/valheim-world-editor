using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Paste panel in another language (the made-up one: PseudoLang): with nothing copied, then with objects copied,
/// stacked, turned and mirrored, and with a blueprint's name, it shows only catalogue texts; and the window's copy,
/// cut and paste messages and its Paste History steps are in that language, while Claude reads the messages in English.
/// </summary>
[Collection("DataDir")]
public sealed class PastePanelLangTests
{
    [AvaloniaFact]
    public void PastePanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var w = TestWindows.Editing(TestScenes.Flat(2, TestScenes.Beech(1, new Vector3(40, 30, 40))));
        var p = w.PastePanel;
        Assert.Empty(PseudoLang.Untranslated(p.Card));

        w.Tools.ChooseSelect();
        w.View.Select([0]);
        w.Copy();
        w.StartPaste();
        p.CopiesBox.Value = 3;
        p.MirrorButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        p.TurnButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.Empty(PseudoLang.Untranslated(p.Card));

        w.View.Paste.Clip!.Name = "My hut";
        p.Refresh();
        Assert.StartsWith(PseudoLang.Open + "My hut: ", p.Info.Text, StringComparison.Ordinal);
        Assert.Empty(PseudoLang.Untranslated(p.Card, "My hut"));
    }

    [AvaloniaFact]
    public void CopyCutAndPasteAreSaidInTheLanguageAndClaudeReadsEnglish()
    {
        using var pseudo = new PseudoLang();
        var s = TestScenes.Flat(2, TestScenes.Beech(1, new Vector3(40, 30, 40)));
        var w = TestWindows.Editing(s);
        void Check(string english)
        {
            Assert.Empty(PseudoLang.Untranslated(w.MessageText));
            Assert.StartsWith(english, w.LastMessageEn, StringComparison.Ordinal);
        }

        w.StartPaste();
        Check("Copy an area first (Area tool, Ctrl+C).");

        // The Select tool's objects: copied, pasted (refused past the area, once, then twice in a row), cut.
        w.Tools.ChooseSelect();
        w.View.Select([]);
        w.Copy();
        Check("Select objects to copy first.");
        w.View.Select([0]);
        w.Copy();
        Check("Copied 1 object(s). Ctrl+V to paste (R turns, F mirrors).");
        w.StartPaste();
        w.PasteAt(new Vector2(-20, -20));
        Check("Not pasted: it goes past the open area.");
        w.PasteAt(new Vector2(100, 100));
        Check("Pasted, 1 object(s). Click again to paste more, Esc when done.");
        Assert.Equal(PseudoLang.Wrap("Paste"), s.UndoLabel);
        w.PastePanel.CopiesBox.Value = 2;
        w.PasteAt(new Vector2(100, 80));
        Check("Pasted 2 copies, 2 object(s). Click again to paste more, Esc when done.");
        Assert.Equal(PseudoLang.Wrap("Paste ×{0}").Replace("{0}", "2", StringComparison.Ordinal), s.UndoLabel);
        w.Tools.ChooseSelect();
        w.View.Select([0]);
        w.Cut();
        Check("Cut 1 object(s): gone from here (Ctrl+Z brings them back). Ctrl+V to paste them, here or in another area (R turns, F mirrors).");

        // The Area tool: no area, an area with nothing in it, the first paste's tree copied and cut.
        w.Tools.ChooseMode(ToolMode.Area);
        w.Copy();
        Check("Select an area first.");
        w.View.Area.Points.Add(new Vector2(10, 10));
        w.View.Area.Points.Add(new Vector2(20, 20));
        w.Copy();
        Check("Nothing to copy here: the area holds no objects");
        w.View.Area.Points.Clear();
        w.View.Area.Points.Add(new Vector2(96, 96));
        w.View.Area.Points.Add(new Vector2(104, 104));
        w.Copy();
        Check("Copied ");
        w.Cut();
        Check("Cut ");
    }
}
