using System.Text;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Png: what cannot make a valid picture is refused when writing, a file without a header says so when read, and chunks
/// of other kinds are passed over.
/// </summary>
public class PngTests
{
    [Fact]
    public void WhatCannotMakeAValidPictureIsRefused()
    {
        // Filter 5 wrote a picture Read refused; too few values and notes PNG cannot hold went through too.
        Assert.Throws<ArgumentOutOfRangeException>(() => Png.WriteGray16(1, 1, [0], filter: 5));
        Assert.Throws<ArgumentException>(() => Png.WriteGray16(2, 2, [0, 1, 2]));
        Assert.Throws<ArgumentException>(() => Png.WriteGray16(0, 1, []));
        Assert.Throws<ArgumentException>(() => Png.WriteGray16(1, 1, [0], new Dictionary<string, string> { [""] = "v" }));
        Assert.Throws<ArgumentException>(() => Png.WriteGray16(1, 1, [0], new Dictionary<string, string> { [" k"] = "v" }));
        Assert.Throws<ArgumentException>(() => Png.WriteGray16(1, 1, [0], new Dictionary<string, string> { ["k\0"] = "v" }));
        Assert.Throws<ArgumentException>(() => Png.WriteGray16(1, 1, [0], new Dictionary<string, string> { [new string('k', 80)] = "v" }));
        Assert.Throws<ArgumentException>(() => Png.WriteGray16(1, 1, [0], new Dictionary<string, string> { ["k"] = "Ā" }));
        Assert.Equal("é", Png.Read(Png.WriteGray16(1, 1, [0], new Dictionary<string, string> { ["vwe-é"] = "é" })).Text["vwe-é"]);
    }

    [Fact]
    public void AFileWithoutAHeaderSaysSo()
    {
        // It said "0-bit PNG pictures are not supported".
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0, .. "IEND"u8, 0xAE, 0x42, 0x60, 0x82];

        Assert.Contains("no header", Assert.Throws<InvalidDataException>(() => Png.Read(png)).Message);
    }

    // Chunks the reader has no use for (another program's gamma or pixel size, or a kind PNG does not name) are passed
    // over: the picture reads as without them.
    [Theory]
    [InlineData("gAMA")]
    [InlineData("pHYs")]
    [InlineData("zzZz")]
    public void ChunksOfOtherKindsArePassedOver(string type)
    {
        byte[] plain = Png.WriteGray16(2, 2, [0, 1000, 2000, 65535], new Dictionary<string, string> { ["k"] = "v" });
        // After the signature (8 bytes) and the header chunk (12 + 13): 4 bytes of data, the checksum not checked.
        byte[] chunk = [0, 0, 0, 4, .. Encoding.ASCII.GetBytes(type), 1, 2, 3, 4, 0, 0, 0, 0];
        byte[] with = [.. plain[..33], .. chunk, .. plain[33..]];

        Png.Image want = Png.Read(plain), got = Png.Read(with);

        Assert.Equal((want.Width, want.Height), (got.Width, got.Height));
        Assert.Equal(want.Values, got.Values);
        Assert.Equal(want.Text, got.Text);
    }
}
