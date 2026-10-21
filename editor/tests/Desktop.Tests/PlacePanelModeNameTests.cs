using System.Reflection;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The name shown for each placing mode in the Place panel's info line: line, grid and zone have their own names, and the
/// brush is the name of every other mode.
/// </summary>
public class PlacePanelModeNameTests
{
    /// <summary>The name shown for a placing mode.</summary>
    private static string ModeName(PlaceTool.Modes mode) => (string)typeof(PlacePanel).GetMethod("ModeName", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [mode])!;

    [Fact]
    public void LineGridAndZoneEachHaveTheirOwnName()
    {
        Assert.Equal(TerrainEditor.App.Lang.T("place.mode.line"), ModeName(PlaceTool.Modes.Line));
        Assert.Equal(TerrainEditor.App.Lang.T("place.mode.grid"), ModeName(PlaceTool.Modes.Grid));
        Assert.Equal(TerrainEditor.App.Lang.T("place.mode.zone"), ModeName(PlaceTool.Modes.Zone));
    }

    [Fact]
    public void TheBrushIsNamedAsTheBrush()
    {
        Assert.Equal(TerrainEditor.App.Lang.T("place.mode.brush"), ModeName(PlaceTool.Modes.Brush));
    }
}
