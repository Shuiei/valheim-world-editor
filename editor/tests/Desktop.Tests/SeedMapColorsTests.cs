using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The seed map with a world generation mod's own map colours: a cell's own colour shades like the game's, the sea
/// keeps its look whatever colour it is given, and the picture takes the cells' colours and the rules' sea level.
/// </summary>
public class SeedMapColorsTests
{
    [Fact]
    public void ACellsOwnColourTakesTheBiomesPlace()
    {
        const float Water = 27f;
        Assert.Equal(((byte)10, (byte)20, (byte)30), SeedMap.Shade(0, Water, Water, (10, 20, 30)));
        Assert.Equal(SeedMap.Shade(0, Water, Water), SeedMap.Shade(0, Water, Water, null));
        // Shaded by height like the game's colour: lighter above the water, tinted under it.
        var (hr, hg, hb) = SeedMap.Shade(0, Water + 60, Water, (10, 20, 30));
        Assert.True(hr > 10 && hg > 20 && hb > 30);
        Assert.Equal(((byte)((10 + 96) / 3), (byte)((20 + 196) / 3), (byte)((30 + 300) / 3)), SeedMap.Shade(0, Water - 1, Water, (10, 20, 30)));
        // The sea is the sea, whatever colour it is given.
        Assert.Equal(SeedMap.Shade(SeedPreview.Ocean, Water - 10, Water), SeedMap.Shade(SeedPreview.Ocean, Water - 10, Water, (10, 20, 30)));
    }

    // The headless bitmap cannot be read back; the picture is made with and without colours, a cell a pixel.
    [AvaloniaFact]
    public void ThePictureTakesTheCellsColours()
    {
        using var bmp = SeedMap.Picture([0, 0], [27f, 27f], 2, 1, [10, 20, 30, 40, 50, 60], 27f);
        Assert.Equal(new Avalonia.PixelSize(2, 1), bmp.PixelSize);
        using var plain = SeedMap.Picture([0], [27f], 1, 1, null, 27f);
        Assert.Equal(new Avalonia.PixelSize(1, 1), plain.PixelSize);
    }
}
