using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The height an object is placed at by its elevation setting: at the one height chosen, a set distance above the ground,
/// or on the ground (which sits five centimetres under it, as the piece's bottom is taken as that far down). A name with no
/// snap points has no bottom, so it is taken as the ground's five centimetres.
/// </summary>
public class PlaceToolElevatedHeightTests
{
    [Fact]
    public void AtTheChosenElevationTheHeightIsTheChosenHeight()
    {
        var tool = new PlaceTool { Elevation = PlaceTool.Elevations.At, Elev = 3.5f };
        Assert.Equal(3.5f, tool.Elevated("no_such_piece", 10f), 3);
    }

    [Fact]
    public void OnTheGroundTheHeightIsTheGroundLessTheBottomOffset()
    {
        var tool = new PlaceTool { Elevation = PlaceTool.Elevations.Ground, Elev = 2f };
        Assert.Equal(9.95f, tool.Elevated("no_such_piece", 10f), 3);
    }

    [Fact]
    public void AboveTheGroundTheHeightIsTheGroundPlusTheChosenDistance()
    {
        var tool = new PlaceTool { Elevation = PlaceTool.Elevations.Above, Elev = 2f };
        Assert.Equal(11.95f, tool.Elevated("no_such_piece", 10f), 3);
    }
}
