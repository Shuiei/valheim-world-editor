using System.Buffers.Binary;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The raw object reader behind the bundle's tree (GameCatalogues.Layout.Bytes): a Transform's child ids are read
/// after their count, in either byte order.
/// </summary>
public class LayoutBytesTests
{
    /// <summary>
    /// An object's bytes with a child count at 52 and the given child path ids 12 bytes apart from 60.
    /// </summary>
    private static byte[] TransformBytes(int count, bool bigEndian, params long[] ids)
    {
        byte[] data = new byte[84];
        if (bigEndian)
        {
            BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(52), count);
        }
        else
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(52), count);
        }
        for (int i = 0; i < ids.Length; i++)
        {
            if (bigEndian)
            {
                BinaryPrimitives.WriteInt64BigEndian(data.AsSpan(60 + 12 * i), ids[i]);
            }
            else
            {
                BinaryPrimitives.WriteInt64LittleEndian(data.AsSpan(60 + 12 * i), ids[i]);
            }
        }
        return data;
    }

    [Fact]
    public void ChildIdsReadTheCountThenEachIdTwelveBytesApart()
    {
        byte[] data = TransformBytes(2, false, 7, 9);
        var bytes = new GameCatalogues.Layout.Bytes(data, false);
        Assert.Equal(new long[] { 7, 9 }, bytes.ChildIds(52));
    }

    [Fact]
    public void ChildIdsOfNoChildrenAreEmpty()
    {
        byte[] data = TransformBytes(0, false);
        var bytes = new GameCatalogues.Layout.Bytes(data, false);
        Assert.Empty(bytes.ChildIds(52));
    }

    [Fact]
    public void ChildIdsHonourBigEndianFiles()
    {
        byte[] data = TransformBytes(2, true, 7, 9);
        var bytes = new GameCatalogues.Layout.Bytes(data, true);
        Assert.Equal(new long[] { 7, 9 }, bytes.ChildIds(52));
    }
}
