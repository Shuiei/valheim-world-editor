using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;
using K = TerrainEditor.App.MasterMap.Kind;

namespace WorldEditor.Tests;

/// <summary>
/// MasterMap: the legend's kinds painted, filled and counted; a map saved as a PNG reads back the same; a picture drawn
/// elsewhere is snapped to the legend (colours far from it counted), scaled to the map's size, and its start colour
/// taken as the start point (the ground under it the kind round it).
/// </summary>
public class MasterMapTests
{
    [Fact]
    public void ANewMapIsAllDeepSeaWithItsStartInTheMiddle()
    {
        var map = new MasterMap();

        Assert.Equal(MasterMap.Size * MasterMap.Size, map.Counts()[(int)K.DeepSea]);
        Assert.Null(map.Start);
        Assert.Equal((0f, 0f), map.StartPoint);
    }

    [Fact]
    public void ADiscPaintsCellsWithinItsRadiusAndSaysWhichChanged()
    {
        var map = new MasterMap();

        var box = map.Disc(100, 200, 3, K.Meadows);

        Assert.Equal((97, 197, 7, 7), box);
        Assert.Equal(K.Meadows, map.At(100, 200));
        Assert.Equal(K.Meadows, map.At(103, 200));
        Assert.Equal(K.DeepSea, map.At(103, 203));
        Assert.Null(map.Disc(100, 200, 3, K.Meadows));
    }

    [Fact]
    public void AFillTakesTheTouchingCellsOfOneKindOnly()
    {
        var map = new MasterMap();
        map.Disc(500, 500, 20, K.Meadows);
        map.Disc(900, 900, 20, K.Meadows);

        var box = map.Fill(500, 500, K.Plains);

        Assert.Equal((480, 480, 41, 41), box);
        Assert.Equal(K.Plains, map.At(500, 500));
        Assert.Equal(K.Meadows, map.At(900, 900));
        Assert.Null(map.Fill(500, 500, K.Plains));
    }

    [Fact]
    public void ASavedMapReadsBackTheSame()
    {
        var map = new MasterMap();
        map.Disc(1000, 1000, 300, K.Meadows);
        map.Disc(1100, 900, 80, K.Mountain);
        map.Disc(950, 1050, 2, K.River);
        map.Start = (1000, 1010);

        var read = MasterMap.Read(map.ToPng());

        Assert.Equal(map.Stamp(), read.Map.Stamp());
        Assert.Equal((1000, 1010), read.Map.Start);
        Assert.Equal(K.Meadows, read.Map.At(1000, 1010));
        Assert.Equal(0, read.Unknown);
        Assert.Equal(0, read.Starts);
    }

    [Fact]
    public void APictureOfAnotherSizeIsScaledAndItsColoursSnappedToTheLegend()
    {
        // A 4 × 4 picture: the top half Meadows a little off its colour, the bottom half an orange the legend lacks.
        var meadows = MasterMap.Legend[(int)K.Meadows];
        byte[] rgba = new byte[4 * 4 * 4];
        for (int i = 0; i < 16; i++)
        {
            (byte r, byte g, byte b) = i < 8 ? ((byte)(meadows.R + 6), (byte)(meadows.G - 5), meadows.B) : ((byte)255, (byte)140, (byte)0);
            (rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]) = (r, g, b, 255);
        }

        var read = MasterMap.Read(Png.WriteRgba8(4, 4, rgba));

        Assert.Equal(K.Meadows, read.Map.At(0, 0));
        Assert.Equal(K.Meadows, read.Map.At(MasterMap.Size - 1, MasterMap.Size / 2 - 1));
        Assert.Equal(8, read.Unknown);
        Assert.Equal((0, 2), read.FirstUnknown);
        // The orange is taken as the nearest colour of the legend all the same.
        Assert.Equal(MasterMap.Nearest(255, 140, 0).Kind, read.Map.At(0, MasterMap.Size - 1));
    }

    [Fact]
    public void TheStartColourIsTheStartAndTheGroundUnderItIsTheKindRoundIt()
    {
        var mountain = MasterMap.Legend[(int)K.Mountain];
        byte[] rgba = Filled(64, mountain.R, mountain.G, mountain.B);
        Set(rgba, 64, 10, 20, MasterMap.StartColour.R, MasterMap.StartColour.G, MasterMap.StartColour.B);

        var read = MasterMap.Read(Png.WriteRgba8(64, 64, rgba));

        // The picture is 32 times smaller than the map: its pixel (10, 20) covers cells 320..351 and 640..671, one start
        // at their middle.
        Assert.Equal((336, 656), read.Map.Start);
        Assert.Equal(K.Mountain, read.Map.At(336, 656));
        Assert.Equal(0, read.Starts);
    }

    [Fact]
    public void StartsDrawnFarApartAreCountedAndTheFirstKept()
    {
        byte[] rgba = Filled(2048, 0x92, 0xA7, 0x5C);
        foreach (var (x, y) in new[] { (100, 100), (101, 100), (900, 900), (1500, 200) })
        {
            Set(rgba, 2048, x, y, 255, 0, 255);
        }

        var read = MasterMap.Read(Png.WriteRgba8(2048, 2048, rgba));

        // The two touching pixels are one start, at their middle.
        Assert.Equal((101, 100), read.Map.Start);
        Assert.Equal(3, read.Starts);
    }

    [Fact]
    public void APictureThatIsNotSquareIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => MasterMap.Read(Png.WriteRgba8(4, 2, new byte[4 * 2 * 4])));
    }

    [Fact]
    public void CellsAndWorldPointsMeetAtTheCellsMiddles()
    {
        var (x, z) = MasterMap.CenterOf(0, 0);

        Assert.Equal((-MasterMap.Half + MasterMap.CellMetres / 2, MasterMap.Half - MasterMap.CellMetres / 2), (x, z));
        Assert.Equal((0, 0), MasterMap.CellOf(x, z));
        Assert.Equal((MasterMap.Size / 2, MasterMap.Size / 2), MasterMap.CellOf(1f, -1f));
        Assert.Equal((MasterMap.Size - 1, MasterMap.Size - 1), MasterMap.CellOf(20000f, -20000f));
    }

    private static byte[] Filled(int n, byte r, byte g, byte b)
    {
        byte[] rgba = new byte[n * n * 4];
        for (int i = 0; i < n * n; i++)
        {
            (rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]) = (r, g, b, 255);
        }
        return rgba;
    }

    private static void Set(byte[] rgba, int n, int x, int y, byte r, byte g, byte b)
    {
        int i = (y * n + x) * 4;
        (rgba[i], rgba[i + 1], rgba[i + 2]) = (r, g, b);
    }
}
