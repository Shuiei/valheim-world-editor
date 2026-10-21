using System.IO.Compression;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A PNG's compressed picture data unpacked to the size the picture needs (Png.Inflate): exactly that size is read,
/// more is refused, and damaged data is refused.
/// </summary>
public class PngInflateTests
{
    /// <summary>The zlib-compressed form of the data, as a PNG's picture data is kept.</summary>
    private static byte[] Zlib(byte[] data)
    {
        using var packed = new MemoryStream();
        using (var z = new ZLibStream(packed, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(data);
        }
        return packed.ToArray();
    }

    [Fact]
    public void DataOfTheNeededSizeIsUnpackedWhole()
    {
        byte[] data = [.. Enumerable.Range(0, 10).Select(i => (byte)i)];

        Assert.Equal(data, PrivateMember.Call<byte[]>(typeof(Png), "Inflate", null, Zlib(data), 10));
    }

    [Fact]
    public void DataLargerThanTheNeededSizeIsRefused()
    {
        byte[] packed = Zlib([.. Enumerable.Range(0, 10).Select(i => (byte)i)]);

        Assert.Throws<InvalidDataException>(() => PrivateMember.Call(typeof(Png), "Inflate", null, packed, 4));
    }

    [Fact]
    public void DamagedDataIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => PrivateMember.Call(typeof(Png), "Inflate", null, new byte[] { 1, 2, 3, 4, 5, 6 }, 4));
    }
}
