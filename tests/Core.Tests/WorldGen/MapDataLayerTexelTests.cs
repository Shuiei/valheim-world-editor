using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// One texel of a map layer: its biome, mask and paint colours, and the blob the browser reads (heights, then each
/// colour plane).
/// </summary>
public class MapDataLayerTexelTests
{
    /// <summary>
    /// Two texels, the first set to height 1.5 with the biome (1, 2, 3) and the mask (4, 5, 6) and no paint.
    /// </summary>
    private static MapData.Layers LayersWithFirstTexelSet()
    {
        var layers = new MapData.Layers(2, 1);
        layers.Set(0, 1.5f, (1, 2, 3), (4, 5, 6), null);
        return layers;
    }

    [Fact]
    public void ASetTexelHasItsBiomeAndMaskWithFullAlpha()
    {
        var layers = LayersWithFirstTexelSet();

        Assert.Equal(new byte[] { 1, 2, 3, 255 }, layers.Biome[0..4]);
        Assert.Equal(new byte[] { 4, 5, 6, 255 }, layers.Mask[0..4]);
    }

    [Fact]
    public void AnUnpaintedTexelKeepsNoPaint()
    {
        var layers = LayersWithFirstTexelSet();

        Assert.Equal(new byte[] { 0, 0, 0, 0 }, layers.Paint[0..4]);
    }

    [Fact]
    public void APaintedTexelHasItsPaintWithFullAlpha()
    {
        var layers = new MapData.Layers(2, 1);

        layers.Set(1, 0f, (0, 0, 0), (0, 0, 0), (9, 8, 7, 6));

        Assert.Equal(new byte[] { 9, 8, 7, 255 }, layers.Paint[4..8]);
    }

    [Fact]
    public void TheBlobHoldsEachHeightThenEachColourPlane()
    {
        var layers = LayersWithFirstTexelSet();

        byte[] blob = layers.ToBytes();

        // Two texels: 2 bytes of height each, then the biome, mask and paint planes of 4 bytes per texel.
        Assert.Equal(2 * 2 + 2 * 12, blob.Length);
        ushort half = BitConverter.HalfToUInt16Bits((Half)1.5f);
        Assert.Equal(new byte[] { (byte)half, (byte)(half >> 8) }, blob[0..2]);
        Assert.Equal(new byte[] { 1, 2, 3, 255 }, blob[4..8]);
        Assert.Equal(new byte[] { 4, 5, 6, 255 }, blob[12..16]);
    }
}
