using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's block-compressed textures unpacked to pixels (GameTextures.Rgba): every format the game uses gives four
/// bytes a pixel, a one-channel texture read as grey, and the formats without transparency opaque.
/// </summary>
public class GameTexturesTests
{
    /// <summary>One 4 × 4 block of each format: DXT1 (8 bytes), DXT5, BC7 and BC5 (16 bytes), BC4 (8 bytes).</summary>
    [Theory]
    [InlineData(10, 8)]
    [InlineData(12, 16)]
    [InlineData(25, 16)]
    [InlineData(26, 8)]
    [InlineData(27, 16)]
    public void EachBlockFormatGivesFourBytesAPixel(int format, int blockBytes)
    {
        byte[]? rgba = GameTextures.Rgba(new GameBundles.TextureLevel("t", format, 4, 4, new byte[blockBytes]));

        Assert.NotNull(rgba);
        Assert.Equal(4 * 4 * 4, rgba.Length);
        if (format is 10 or 26)
        {
            Assert.All(Enumerable.Range(0, 16), i => Assert.Equal(255, rgba[i * 4 + 3]));
        }
    }

    [Fact]
    public void AOneChannelTextureReadsAsGrey()
    {
        // BC4: one channel, 8 bytes a block; every endpoint 200: every pixel 200.
        byte[] block = [200, 200, 0, 0, 0, 0, 0, 0];
        byte[] rgba = GameTextures.Rgba(new GameBundles.TextureLevel("grey", 26, 4, 4, block))!;

        Assert.All(Enumerable.Range(0, 16), i => Assert.Equal([200, 200, 200, 255], rgba.AsSpan(i * 4, 4).ToArray()));
    }
}
