using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The top bar in windows of several widths: a long name line ends in "…" and the buttons stay inside the window,
/// dropping their words only when the window is too narrow for them.
/// </summary>
public class MainWindowTopBarFitTests
{
    [AvaloniaTheory]
    [InlineData(800, true)]
    [InlineData(1600, false)]
    public void TheButtonsStayInsideTheWindow(int width, bool compact)
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        w.Width = width;
        // A Workshop cost line's length.
        w.SubtitleText.Text = string.Join(" · ", Enumerable.Repeat("Black Marble 7342", 12));
        for (int pass = 0; pass < 3; pass++)
        {
            w.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
        var help = w.HelpButton;
        double right = help.TranslatePoint(new Point(help.Bounds.Width, 0), w)!.Value.X;
        Assert.True(right <= width, $"? ends at {right} of {width}");
        Assert.Equal(compact, w.TopBarCompact);
    }

    [AvaloniaFact]
    public void CompactHidesTheButtonsWordsAndBringsThemBack()
    {
        var w = TestWindows.Editing(TestScenes.Flat(2));
        w.SetTopBarCompact(true);
        Assert.Equal("", VisibleWords(w.HistoryButton));
        // The driver still finds it by its words.
        Assert.Equal(Lang.T("history.title"), Driver.TextOf(w.HistoryButton));
        w.SetTopBarCompact(false);
        Assert.NotEqual("", VisibleWords(w.HistoryButton));
    }

    /// <summary>The words a button shows (its visible texts).</summary>
    private static string VisibleWords(Avalonia.Controls.Button b) =>
        string.Concat(Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(b).OfType<Avalonia.Controls.TextBlock>().Where(t => t.IsVisible).Select(t => t.Text));
}
