using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The map's colours: a biome's colour from the game's list, a texel's rounding to a byte, and a texel's mask colour.
/// </summary>
public class MapDataColorsTests
{
    [Fact]
    public void AListedBiomeIsItsGameColour()
    {
        Assert.Equal(((byte)146, (byte)167, (byte)92), MapData.PixelColor(Heightmap.Biome.Meadows));
    }

    [Fact]
    public void AnUnlistedBiomeIsWhite()
    {
        Assert.Equal(((byte)255, (byte)255, (byte)255), MapData.PixelColor(Heightmap.Biome.None));
    }

    [Fact]
    public void AFractionIsRoundedToAByteHeldToItsRange()
    {
        Assert.Equal((byte)128, MapData.Byte(0.5f));
        Assert.Equal((byte)255, MapData.Byte(2f));
        Assert.Equal((byte)0, MapData.Byte(-1f));
    }

    [Fact]
    public void BelowSeaLevelTheMaskIsOnlyTheBlueChannel()
    {
        using var w = new TempWorld();
        var (_, terrain, edits) = MapDataTestWorld.Open(w);
        var map = new MapData(terrain, edits);

        var mask = map.MaskColor(0f, 0f, 10f, Heightmap.Biome.Meadows);

        Assert.Equal((byte)0, mask.Item1);
        Assert.Equal((byte)0, mask.Item2);
    }

    [Fact]
    public void ABlackForestTexelIsRedAboveSeaLevel()
    {
        using var w = new TempWorld();
        var (_, terrain, edits) = MapDataTestWorld.Open(w);
        var map = new MapData(terrain, edits);

        Assert.Equal(((byte)255, (byte)0, (byte)0), map.MaskColor(0f, 0f, 50f, Heightmap.Biome.BlackForest));
    }

    [Fact]
    public void AnOtherBiomeAboveSeaLevelHasNoMask()
    {
        using var w = new TempWorld();
        var (_, terrain, edits) = MapDataTestWorld.Open(w);
        var map = new MapData(terrain, edits);

        Assert.Equal(((byte)0, (byte)0, (byte)0), map.MaskColor(0f, 0f, 50f, Heightmap.Biome.Ocean));
    }
}
