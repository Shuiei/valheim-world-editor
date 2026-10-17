using System.IO.Compression;
using System.Text;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Lengths and counts read from a file are checked before use, a read that fails because the bytes are damaged is
/// recognised, and gzip data is unpacked only up to its limit.
/// </summary>
public class CheckedReadsTests
{
    [Fact]
    public void ACountThatDoesNotFitInWhatIsLeftIsRefused()
    {
        using var r = new BinaryReader(new MemoryStream([1, 2, 3, 4]));

        Assert.Throws<EndOfStreamException>(() => r.CheckCount(5, 1));
    }

    [Fact]
    public void ANegativeCountIsRefused()
    {
        using var r = new BinaryReader(new MemoryStream([1, 2, 3, 4]));

        Assert.Throws<InvalidDataException>(() => r.CheckCount(-1, 1));
    }

    [Fact]
    public void ACountThatFitsIsReturned()
    {
        using var r = new BinaryReader(new MemoryStream([1, 2, 3, 4]));

        Assert.Equal(4, r.CheckCount(4, 1));
    }

    [Fact]
    public void AShortReadIsDamagedAndAFileErrorIsNot()
    {
        Assert.True(CheckedReads.Damaged(new EndOfStreamException()));
        Assert.False(CheckedReads.Damaged(new IOException()));
    }

    [Fact]
    public void AStringWrittenWithItsLengthReadsBack()
    {
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write("vwe");
        }
        ms.Position = 0;
        using var r = new BinaryReader(ms, Encoding.UTF8);

        Assert.Equal("vwe", r.ReadCheckedString());
    }

    [Fact]
    public void GzipDataUnpacksWithinItsLimit()
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
        {
            gz.Write("hello"u8);
        }

        Assert.Equal("hello"u8.ToArray(), CheckedReads.Gunzip(ms.ToArray(), 100));
    }

    [Fact]
    public void GzipDataLargerThanItsLimitIsRefused()
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
        {
            gz.Write("hello"u8);
        }

        Assert.Throws<InvalidDataException>(() => CheckedReads.Gunzip(ms.ToArray(), 3));
    }

    [Fact]
    public void DamagedGzipDataIsRefused()
    {
        Assert.Throws<InvalidDataException>(() => CheckedReads.Gunzip([1, 2, 3, 4], 100));
    }
}
