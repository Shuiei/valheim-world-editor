using ValheimGen;
using Xunit;
using static WorldEditor.Tests.MapDataTestWorld;

namespace WorldEditor.Tests;

/// <summary>
/// The layers of the world map as bytes (half floats, then the three colour layers), the overview picture of the terrain, and the
/// zone helpers agreeing with each other.
/// </summary>
[Collection("World files")]
public class MapDataLayersTests
{
    [Fact]
    public void LayersAreWrittenAsHalfFloatsThenThreeColourLayers()
    {
        var l = new MapData.Layers(2, 1);
        l.Set(0, 31.5f, (1, 2, 3), (4, 5, 6), null);
        l.Set(1, -2f, (7, 8, 9), (10, 11, 12), (13, 14, 15, 16));
        byte[] b = l.ToBytes();
        Assert.Equal(2 * 2 + 2 * 12, b.Length);
        Assert.Equal(31.5f, (float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(b, 0)));
        Assert.Equal(-2f, (float)BitConverter.UInt16BitsToHalf(BitConverter.ToUInt16(b, 2)));
        Assert.Equal(new byte[] { 1, 2, 3, 255, 7, 8, 9, 255 }, b[4..12]);
        Assert.Equal(new byte[] { 4, 5, 6, 255, 10, 11, 12, 255 }, b[12..20]);
        // Paint: only the painted texel, always opaque.
        Assert.Equal(new byte[] { 0, 0, 0, 0, 13, 14, 15, 255 }, b[20..28]);
    }

    [Fact]
    public void TheOverviewIsAPngOfTheRequestedSize()
    {
        using var w = new TempWorld();
        var (_, terrain, _) = Open(w);
        using var png = SkiaSharp.SKBitmap.Decode(terrain.OverviewPng);
        Assert.Equal(64, png.Width);
        Assert.Equal(64, png.Height);
        // The corners are outside the round world: transparent; the middle is land or sea, opaque.
        Assert.Equal(0, png.GetPixel(0, 0).Alpha);
        Assert.Equal(255, png.GetPixel(32, 32).Alpha);
        Assert.Same(terrain.OverviewPng, terrain.OverviewPng);
    }

    [Fact]
    public void ZoneLevelHelpersAgreeWithEachOther()
    {
        using var w = new TempWorld();
        var (_, terrain, _) = Open(w);
        // Raw and base ground are the same where no location flattens it (no modifiers given).
        Assert.Equal(terrain.RawZone(5, 5), terrain.BaseZone(5, 5));
        // Vertex biomes and the zone's corner biomes come from the same generator.
        int[] vb = terrain.VertexBiomes(5, 5);
        Assert.Equal(65 * 65, vb.Length);
        Assert.Equal((int)terrain.BiomeAt(5 * 64 - 32, 5 * 64 - 32), vb[0]);
        Assert.Equal(4, terrain.CornerBiomes(5, 5).Length);
        // Corners beyond the world's edge are ocean.
        Assert.All(terrain.CornerBiomes(170, 170), b => Assert.Equal((int)Heightmap.Biome.Ocean, b));
        Assert.Equal(65 * 65 * 4, terrain.BaseMask(5, 5).Length);
    }
}
