using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Taking Claude's changes back (HistoryPanel.TakeBack.cs): what it covers is the open area's history; the message says
/// how many of Claude's changes made in other areas are kept, to be taken back there.
/// </summary>
public class HistoryPanelTakeBackTests
{
    // Claude's step made outside this area is kept aside (it cannot be removed from here): the take back left it
    // without a word while the button said it took out every change Claude made.
    [AvaloniaFact]
    public async Task ClaudesChangesInOtherAreasAreSaidToBeKept()
    {
        var wide = TestScenes.Flat(2);
        wide.Shape(100, 100, _ => 1, 3, 0, "Claude: far hill");
        wide.Shape(20, 20, _ => 1, 3, 0, "Claude: near hill");
        var here = TestScenes.Flat(1);
        Assert.Equal(1, here.Import(wide.Export(), _ => []));
        Assert.Equal(1, here.ShelvedDone(HistoryPanel.ByClaude));
        var p = new HistoryPanel(() => here);
        Said? said = null;
        p.Message += m => said = m;
        await p.TakeBackClaudes();
        Assert.True(Assert.Single(here.UndoList, c => c.Label == "Claude: near hill").Removed);
        Assert.Equal("Took back 1 change(s) Claude made; yours are kept. 1 change(s) Claude made in other areas are kept: open those areas to take them back.", said!.Shown);
    }
}
