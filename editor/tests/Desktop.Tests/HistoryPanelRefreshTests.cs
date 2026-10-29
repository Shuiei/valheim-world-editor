using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The History card's rows (HistoryPanel.Refresh): an area without steps of its own while steps were made elsewhere.
/// </summary>
public class HistoryPanelRefreshTests
{
    private static List<string> Texts(Control c) => [.. c.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? "")];

    // A step made in another area of this session is kept aside here: the card said "No changes yet in this session."
    // above the line counting it.
    [AvaloniaFact]
    public void AnAreaWithoutStepsOfItsOwnSaysSoNotThatThereAreNone()
    {
        var wide = TestScenes.Flat(2);
        wide.Shape(100, 100, _ => 1, 3, 0, "far hill");
        var here = TestScenes.Flat(1);
        Assert.Equal(1, here.Import(wide.Export(), _ => []));
        Assert.Empty(here.UndoList);
        var p = new HistoryPanel(() => here);
        p.Card.IsVisible = true;
        p.Refresh();
        var words = Texts(p.Card);
        Assert.Contains("No changes in this area yet.", words);
        Assert.DoesNotContain("No changes yet in this session.", words);
        Assert.Contains(words, w => w.StartsWith("1 step(s) made in other areas are kept", StringComparison.Ordinal));
        var none = new HistoryPanel(() => TestScenes.Flat(1));
        none.Card.IsVisible = true;
        none.Refresh();
        Assert.Contains("No changes yet in this session.", Texts(none.Card));
    }
}
