using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// PlaceTool.ShownName: one of your own presets is listed as you named it, and a built-in one under its catalogue name.
/// </summary>
public class PlaceToolPresetNameTests
{
    [Fact]
    public void YourOwnPresetIsListedAsYouNamedIt()
    {
        var mine = new PlaceTool.Preset("My spot", [], 1, 1, 1, 1, 0);

        Assert.Equal("My spot", PlaceTool.ShownName(mine));
    }

    [Fact]
    public void ABuiltInPresetIsListedUnderItsCatalogueName()
    {
        // The third built-in preset is Swamp; its name is the one the catalogue gives that key.
        Assert.Equal(Lang.T("place.preset.swamp"), PlaceTool.ShownName(PlaceTool.BuiltIn[2]));
    }
}
