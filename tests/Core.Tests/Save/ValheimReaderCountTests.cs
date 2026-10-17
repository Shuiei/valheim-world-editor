using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The counted reads of the save reader: a count read as an int and checked against the bytes left, a byte array with
/// its length prefix, and a bool and an unsigned 16-bit int read from their stored bytes.
/// </summary>
public class ValheimReaderCountTests
{
    [Fact]
    public void ACountThatFitsInTheBytesLeftIsReturned()
    {
        // Count 2 of items that take 4 bytes each: 8 bytes are needed and 8 are left.
        using var r = new ValheimReader(new MemoryStream([2, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7, 8]));

        Assert.Equal(2, r.ReadCount(4));
    }

    [Fact]
    public void ACountThatNeedsMoreBytesThanLeftIsRefused()
    {
        // Count 2 of items that take 4 bytes each, but only 7 bytes follow the count.
        using var r = new ValheimReader(new MemoryStream([2, 0, 0, 0, 1, 2, 3, 4, 5, 6, 7]));

        Assert.Throws<EndOfStreamException>(() => r.ReadCount(4));
    }

    [Fact]
    public void ANegativeCountIsRefused()
    {
        using var r = new ValheimReader(new MemoryStream([0xFF, 0xFF, 0xFF, 0xFF, 1, 2, 3, 4]));

        Assert.Throws<InvalidDataException>(() => r.ReadCount(1));
    }

    [Fact]
    public void AByteArrayIsReadAfterItsLengthPrefix()
    {
        using var r = new ValheimReader(new MemoryStream([3, 0, 0, 0, 0xAA, 0xBB, 0xCC]));

        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC }, r.ReadByteArray());
    }

    [Fact]
    public void AByteArrayLongerThanTheBytesLeftIsRefused()
    {
        // The prefix says 5 bytes, but only 2 follow.
        using var r = new ValheimReader(new MemoryStream([5, 0, 0, 0, 0xAA, 0xBB]));

        Assert.Throws<EndOfStreamException>(r.ReadByteArray);
    }

    [Fact]
    public void ANegativeByteArrayLengthIsRefused()
    {
        using var r = new ValheimReader(new MemoryStream([0xFF, 0xFF, 0xFF, 0xFF, 0xAA]));

        Assert.Throws<InvalidDataException>(r.ReadByteArray);
    }

    [Fact]
    public void AnyNonZeroByteReadsAsTrue()
    {
        using var r = new ValheimReader(new MemoryStream([0, 7]));

        Assert.False(r.ReadBool());
        Assert.True(r.ReadBool());
    }

    [Fact]
    public void AnUnsignedShortIsReadLowByteFirst()
    {
        // 0x0102 stored little-endian is 02 01.
        using var r = new ValheimReader(new MemoryStream([0x02, 0x01]));

        Assert.Equal((ushort)0x0102, r.ReadUShort());
    }
}
