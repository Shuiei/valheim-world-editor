using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The object's lists as they are written: each non-empty list as its count and its key and value pairs, none for an
/// empty list.
/// </summary>
public class ZdoDataWritePartsTests
{
    [Fact]
    public void AnObjectWithNoListsWritesNothing()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        new ZdoData().WriteParts(w);
        w.Flush();

        Assert.Empty(ms.ToArray());
    }

    [Fact]
    public void AFloatListWritesItsCountThenEachKeyAndValue()
    {
        var z = new ZdoData();
        z.FloatList.Add((1, 2f));
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        z.WriteParts(w);
        w.Flush();

        // One item: the count byte (1), the key (int, 4 bytes), the value (float, 4 bytes).
        byte[] bytes = ms.ToArray();
        Assert.Equal(9, bytes.Length);
        Assert.Equal(1, bytes[0]);
        Assert.Equal(1, BitConverter.ToInt32(bytes, 1));
        Assert.Equal(2f, BitConverter.ToSingle(bytes, 5));
    }
}
