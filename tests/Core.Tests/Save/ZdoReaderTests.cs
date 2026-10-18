using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The one reader of an object's layout: its head, then every value in file order with where it is, and nothing past
/// the object read.
/// </summary>
public class ZdoReaderTests
{
    [Fact]
    public void TheHeadThenEveryValueComeInFileOrder()
    {
        var z = new ZdoData { Position = new Vector3(4, 0, -8), SmallPosition = true, Prefab = 77, Rotation = new Vector3(0, 90, 0), Connection = [1, 2, 3, 4, 5] };
        z.FloatList.Add((1, 1.5f));
        z.Vec3List.Add((2, new Vector3(1, 2, 3)));
        z.QuatList.Add((3, Quaternion.Identity));
        z.IntList.Add((4, 40));
        z.IntList.Add((5, 50));
        z.LongList.Add((6, 60L));
        z.StringList.Add((7, "seven"));
        z.ByteList.Add((8, [0xFF, 0xFE]));
        byte[] bytes = [.. z.Serialize(), 0xEE];
        using ValheimReader pkg = new(new MemoryStream(bytes));
        ZdoReader read = new(pkg, 41);
        Assert.True(read.HasData);
        Assert.True(read.SmallPosition);
        Assert.Equal(new Vector3(4, 0, -8), read.Position);
        Assert.Equal(77, read.Prefab);
        Assert.Equal(new Vector3(0, 90, 0), read.Rotation);
        // Flags, two shorts, the prefab, a two-byte rotation.
        Assert.Equal(12, read.DataStart);
        List<(ushort Section, int Key)> seen = [];
        while (read.Next())
        {
            seen.Add((read.Section, read.Key));
            switch (read.Section)
            {
                case ZdoData.Connections: Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, read.Connection); break;
                case ZdoData.Floats: Assert.Equal(1.5f, read.FloatValue); break;
                case ZdoData.Vec3s: Assert.Equal(new Vector3(1, 2, 3), read.Vec3Value); break;
                case ZdoData.Quats: Assert.Equal(Quaternion.Identity, read.QuatValue); break;
                case ZdoData.Ints: Assert.Equal(read.Key * 10, read.IntValue); break;
                case ZdoData.Longs: Assert.Equal(60L, read.LongValue); break;
                case ZdoData.Strings: Assert.Equal("seven", read.StringValue); break;
                default:
                    Assert.Equal(new byte[] { 0xFF, 0xFE }, read.BytesValue);
                    // At the length, then the stream right after the value.
                    Assert.Equal(bytes.Length - 1 - 6, read.ValueStart);
                    Assert.Equal(bytes.Length - 1, pkg.Position);
                    break;
            }
        }
        List<(ushort Section, int Key)> expected =
            [(ZdoData.Connections, 0), (ZdoData.Floats, 1), (ZdoData.Vec3s, 2), (ZdoData.Quats, 3), (ZdoData.Ints, 4), (ZdoData.Ints, 5), (ZdoData.Longs, 6), (ZdoData.Strings, 7), (ZdoData.ByteArrays, 8)];
        Assert.Equal(expected, seen);
        Assert.Equal(bytes.Length - 1, pkg.Position);
    }

    [Fact]
    public void AnObjectWithNoDataGivesNoValues()
    {
        byte[] bytes = ZdoBuilder.Blank(77, 0x0100, new Vector3(1, 2, 3), Vector3.Zero, 0f);
        using ValheimReader pkg = new(new MemoryStream(bytes));
        ZdoReader read = new(pkg, 41);
        Assert.False(read.HasData);
        Assert.False(read.Next());
        Assert.Equal(bytes.Length, read.DataStart);
        Assert.Equal(bytes.Length, pkg.Position);
    }
}
