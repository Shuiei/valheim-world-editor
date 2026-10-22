using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map's Discard: what the status bar said about the changes dropped (the history kept from an earlier session,
/// gone with them live) is not shown again in the next area.
/// </summary>
[Collection("World files")]
public class MainWindowReloadWorldTests
{
    [AvaloniaFact]
    public async Task DiscardingTheWorldEmptiesTheStatusBar()
    {
        using var r = new WorldWindowRun();
        await r.OpenLive();
        r.Raise();
        r.W.Msg(Said.Raw("History from an earlier session: 1 step(s) you can undo or redo, in History."));
        r.W.ShowMap();
        r.Answers.Enqueue(true);
        await r.W.DiscardWorld();
        Assert.Equal((0, 0, 0, 0), r.W.World!.Pending);
        Assert.Equal("", r.Said);
        await r.W.EditArea(0, 0, 1);
        Assert.Equal("", r.Said);
        r.W.MapPage!.Stop();
    }
}
