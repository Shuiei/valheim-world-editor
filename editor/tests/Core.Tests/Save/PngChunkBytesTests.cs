using System.Text;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Png's byte helpers: a big-endian int written at an offset, and the CRC-32 of a chunk's type and data (the value the
/// PNG specification gives for the IEND chunk).
/// </summary>
public class PngChunkBytesTests
{
    [Fact]
    public void BigEndianWritesTheHighByteFirstAtTheOffset()
    {
        byte[] buffer = new byte[6];

        PrivateMember.Call(typeof(Png), "BigEndian", null, buffer, 2, 0x01020304);

        Assert.Equal(new byte[] { 0, 0, 1, 2, 3, 4 }, buffer);
    }

    [Fact]
    public void CrcOfTheIendTypeIsTheSpecificationValue()
    {
        // The IEND chunk has no data; its CRC is over the type alone, and is AE 42 60 82 in the PNG specification.
        uint crc = PrivateMember.Call<uint>(typeof(Png), "Crc", null, 0xFFFFFFFFu, Encoding.ASCII.GetBytes("IEND")) ^ 0xFFFFFFFFu;

        Assert.Equal(0xAE426082u, crc);
    }

    [Fact]
    public void CrcWithNoDataKeepsTheStartingValue()
    {
        Assert.Equal(0x12345678u, PrivateMember.Call<uint>(typeof(Png), "Crc", null, 0x12345678u, Array.Empty<byte>()));
    }
}
