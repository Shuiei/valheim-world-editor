using System.Numerics;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Xunit;
using static TerrainEditor.Desktop.Tests.SharedButtons;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The Area panel's buttons and selection: copy, cut, paste, save and library asked of the window, the box and polygon modes with Clear, and the count of what a dragged box holds.
/// </summary>
[Collection("World files")]
public class AreaPanelSelectionTests
{
    private static readonly string[] Expected1 = ["copy", "cut", "paste", "save", "library"];
    [AvaloniaFact]
    public void CopyPasteAndBlueprintButtonsAskTheWindow()
    {
        using var r = new SharedAreaPanelRun();
        var asked = new List<string>();
        r.P.CopyAsked += () => asked.Add("copy");
        r.P.CutAsked += () => asked.Add("cut");
        r.P.PasteAsked += () => asked.Add("paste");
        r.P.SaveBlueprintAsked += () => asked.Add("save");
        r.P.LibraryAsked += () => asked.Add("library");
        Click(r.P.CopyButton);
        Click(r.P.CutButton);
        Click(r.P.PasteButton);
        Click(r.P.SaveBlueprintButton);
        Click(r.P.LibraryButton);
        Assert.Equal(Expected1, asked);
    }

    [AvaloniaFact]
    public void BoxPolygonAndClearButtons()
    {
        using var r = new SharedAreaPanelRun();
        Click(r.P.PolyButton);
        Assert.False(r.W.View.Area.Box);
        Assert.Empty(r.W.View.Area.Points);
        Click(r.P.BoxButton);
        Assert.True(r.W.View.Area.Box);
        r.Select(2, 2, 10, 10);
        Click(r.P.ClearButton);
        Assert.Empty(r.W.View.Area.Points);
        Assert.Equal("Selection cleared.", r.Said);
    }

    [AvaloniaFact]
    public void DraggingABoxCountsWhatIsInsideOnceTheMovesAreHandled()
    {
        using var r = new SharedAreaPanelRun(select: false);
        var a = r.W.View.Area;
        a.Box = true;
        Dispatcher.UIThread.RunJobs();
        int before = r.P.Refreshes;
        a.Down(new Vector2(2, 2), 1);
        a.Moved(new Vector2(12, 12));
        a.Moved(new Vector2(22, 22));
        // Not counted at each move: once the moves waiting are handled, once for all of them.
        Assert.DoesNotContain("m² selected", r.P.Info.Text ?? "", StringComparison.Ordinal);
        Assert.Equal(before, r.P.Refreshes);
        Dispatcher.UIThread.RunJobs();
        Assert.StartsWith("400 m² selected", r.P.Info.Text, StringComparison.Ordinal);
        Assert.Equal(before + 1, r.P.Refreshes);
        // Released: counted at once, and the refresh the last move asked for is not run again after it.
        a.Moved(new Vector2(32, 32));
        a.Up();
        Assert.StartsWith("900 m² selected", r.P.Info.Text, StringComparison.Ordinal);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(before + 2, r.P.Refreshes);
    }
}
