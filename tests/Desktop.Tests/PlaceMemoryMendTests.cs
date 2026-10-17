using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// PlaceMemory's mending of a hand-edited place.json: null lists become empty, null names are left out of the name lists, and
/// presets without a name or kinds are left out.
/// </summary>
public class PlaceMemoryMendTests
{
    private static PlaceMemory Mend(PlaceMemory memory) => PrivateInvoke.Call<PlaceMemory>(typeof(PlaceMemory), "Mended", memory);

    [Fact]
    public void NullListsBecomeEmptyOnes()
    {
        var memory = new PlaceMemory { Presets = null!, Favourites = null!, Recent = null!, Chosen = null!, Weights = null!, Builders = null! };

        var mended = Mend(memory);

        Assert.Empty(mended.Presets);
        Assert.Empty(mended.Favourites);
        Assert.Empty(mended.Recent);
        Assert.Empty(mended.Chosen);
        Assert.Empty(mended.Weights);
        Assert.Empty(mended.Builders);
    }

    [Fact]
    public void NullNamesAreLeftOutOfTheNameLists()
    {
        var memory = new PlaceMemory { Favourites = ["Keep", null!], Recent = [null!, "Also"] };

        var mended = Mend(memory);

        Assert.Equal(["Keep"], mended.Favourites);
        Assert.Equal(["Also"], mended.Recent);
    }

    [Fact]
    public void PresetsWithoutANameOrWithoutKindsAreLeftOut()
    {
        var memory = new PlaceMemory
        {
            Presets =
            [
                new PlaceTool.Preset(null!, [], 1, 1, 1, 1, 0),
                new PlaceTool.Preset("Keep", [], 1, 1, 1, 1, 0),
                new PlaceTool.Preset("NoKinds", null!, 1, 1, 1, 1, 0),
            ],
        };

        var mended = Mend(memory);

        Assert.Equal(["Keep"], mended.Presets.Select(p => p.Name));
    }
}
