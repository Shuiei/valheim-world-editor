using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The status bar: a message on two lines makes it taller, and the panels over the view still stop above it.
/// </summary>
public class MainWindowCursorTests
{
    [AvaloniaFact]
    public void ThePanelsStopAboveATwoLineStatusBar()
    {
        var w = ClipboardSetup.WindowOn(TestScenes.Flat(2), 1280, 720);
        w.Tools.ChooseMode(ToolMode.Place);
        w.ShowRight(w.ViewPanelCard);
        w.MessageText.Text = string.Join(" ", Enumerable.Repeat("A long message that does not fit on one line of the status bar.", 6));
        for (int pass = 0; pass < 3; pass++)
        {
            w.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
        var bar = w.MessageText.FindAncestorOfType<Border>()!;
        double barTop = bar.TranslatePoint(default, w)!.Value.Y;
        Assert.True(w.MessageText.Bounds.Height > 20, "the message is on two lines");
        var column = (Control)w.Tools.Rail.Parent!;
        foreach (var card in column.GetVisualChildren().OfType<Control>().Where(c => c.IsVisible).Append(w.ViewPanelCard))
        {
            double bottom = card.TranslatePoint(new Point(0, card.Bounds.Height), w)!.Value.Y;
            Assert.True(bottom <= barTop, $"{card} ends at {bottom}, the status bar starts at {barTop}");
        }
    }
}
