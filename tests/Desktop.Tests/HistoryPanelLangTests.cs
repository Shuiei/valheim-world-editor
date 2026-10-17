using System.Globalization;
using System.Numerics;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The History panel in another language (the made-up one: PseudoLang): empty, then with changes (one applied, one of
/// Claude's, one undone), it shows only catalogue texts besides the steps' own names and times; taking a change out says
/// so in the language in use, its English kept for Claude.
/// </summary>
[Collection("DataDir")]
public sealed class HistoryPanelLangTests
{
    private static readonly Func<Formula.Env, double> Two = Formula.Compile("2", []);

    [AvaloniaFact]
    public void HistoryPanelShowsOnlyCatalogueTexts()
    {
        using var pseudo = new PseudoLang();
        var s = TestScenes.Flat(2, new WorldScene.Thing(1, StableHash.Of("Beech1"), new Vector3(0, 30, 0), Vector3.Zero, 1, false));
        var w = TestWindows.Editing(s);
        w.ShowRight(w.History.Card);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(PseudoLang.Untranslated(w.History.Card));

        s.Shape(30, 30, Two, 3, 0, "raise");
        s.Shape(60, 60, Two, 3, 0, "Claude: raise");
        s.Delete([0]);
        s.Shape(90, 90, Two, 3, 0, "raise more");
        s.UndoList[0].Applied = true;
        s.Undo();
        w.History.Refresh();
        Dispatcher.UIThread.RunJobs();
        Assert.True(w.History.TakeBackClaude.IsVisible);
        // The steps' names are data here (each tool names its own steps); their times are the computer's format.
        string[] data = [.. s.UndoList.Concat(s.RedoList).SelectMany(c => new[] { c.Label, c.Time.ToString("T", CultureInfo.CurrentCulture), c.Time.ToString("g", CultureInfo.CurrentCulture) })];
        Assert.Empty(PseudoLang.Untranslated(w.History.Card, data));

        // Remove on the first change: said in the language in use, Claude reads it in English.
        var first = (Border)w.History.Rows.Children[^1];
        var remove = first.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, Lang.T("history.label.remove")));
        remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith(PseudoLang.Open.ToString(), w.MessageText.Text);
        Assert.Equal("Removed “raise”; everything else is kept. Ctrl+Z brings it back.", w.LastMessageEn);
    }
}
