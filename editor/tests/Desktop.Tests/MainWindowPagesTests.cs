using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Opening a world: what the status bar said before (the Workshop's blueprint, the last world) is not kept for its
/// areas.
/// </summary>
[Collection("World files")]
public class MainWindowPagesTests
{
    [AvaloniaFact]
    public async Task OpeningAWorldEmptiesTheStatusBar()
    {
        using var r = new MapPageRun();
        r.W.Msg(Said.Raw("Opened “Hut”: 12 piece(s)."));
        await r.Open();
        Assert.Equal("", r.W.MessageText.Text);
        Assert.Equal("", r.W.LastMessageEn);
        await r.W.EditArea(0, 0, 1);
        Assert.Equal("", r.W.MessageText.Text);
    }

    // Claude opens a world from the Workshop: the plot is let go (no Workshop, nothing of it left to undo or save
    // behind the map).
    [AvaloniaFact]
    public async Task OpeningAWorldLetsGoOfTheWorkshop()
    {
        using var r = new ClaudeRun();
        await r.T.OpenWorkshop();
        Assert.True(r.W.InWorkshop);
        await r.T.OpenWorld(r.Dir);
        Assert.False(r.W.InWorkshop);
        Assert.Null(r.W.Session);
        var state = ClaudeRun.J(await r.T.EditorState());
        Assert.Equal("map", state.GetProperty("page").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, state.GetProperty("area").ValueKind);
        Assert.StartsWith("No area is open", await r.T.Undo());
    }
}
