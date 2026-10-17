using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The raw reader of a layout tree's bytes (GameCatalogues.Layout.Bytes): integers and floats read at an offset in the
/// file's byte order, and single bytes as they are.
/// </summary>
public class LayoutBytesNumbersTests
{
    [Fact]
    public void AnIntIsReadLittleEndianUnlessTheFileIsBigEndian()
    {
        byte[] bytes = [0x78, 0x56, 0x34, 0x12];

        Assert.Equal(0x12345678, new GameCatalogues.Layout.Bytes(bytes, false).I32(0));
        Assert.Equal(0x78563412, new GameCatalogues.Layout.Bytes(bytes, true).I32(0));
    }

    [Fact]
    public void AnIntIsReadAtItsOffset()
    {
        byte[] bytes = [0xFF, 0x02, 0x00, 0x00, 0x00];

        Assert.Equal(2, new GameCatalogues.Layout.Bytes(bytes, false).I32(1));
    }

    [Fact]
    public void ALongIsReadLittleEndian()
    {
        byte[] bytes = [0x01, 0x02, 0, 0, 0, 0, 0, 0];

        Assert.Equal(0x0201L, new GameCatalogues.Layout.Bytes(bytes, false).I64(0));
    }

    [Fact]
    public void AFloatIsReadFromItsIntBits()
    {
        byte[] bytes = BitConverter.GetBytes(1.5f);

        Assert.Equal(1.5f, new GameCatalogues.Layout.Bytes(bytes, false).F32(0));
    }

    [Fact]
    public void AByteIsReadAsItIs()
    {
        byte[] bytes = [0, 0xAB];

        Assert.Equal((byte)0xAB, new GameCatalogues.Layout.Bytes(bytes, false).U8(1));
    }
}
