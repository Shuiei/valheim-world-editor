using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The History panel marks Claude's changes with a chip; with no area open, taking Claude's changes back does
/// nothing.
/// </summary>
public class HistoryPanelClaudeTests
{
    /// <summary>The panel's words.</summary>
    private static List<string> Texts(Control c) => [.. c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")];

    [AvaloniaFact]
    public async Task ClaudesChangesHaveAChip()
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        // Nothing open: nothing to take back.
        await w.History.TakeBackClaudes();
        var s = TestScenes.Flat(1);
        w.View.Show(s.Scene, null);
        w.Edit(s);
        s.Shape(32, 32, _ => 1, 3, 0, "Raise");
        Assert.DoesNotContain("Claude", Texts(w.History.Card));
        s.Shape(32, 32, _ => 1, 3, 0, "Claude: a hill");
        w.History.Refresh();
        Assert.Contains("Claude", Texts(w.History.Card));
    }
}
