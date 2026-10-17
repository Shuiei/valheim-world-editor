using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// An object's values read and written at their edges: two-byte counts, the old pre-chunk format, setting and removing values,
/// and the numbers and vectors read as they are set.
/// </summary>
public class ZdoDataValueTests
{
    [Fact]
    public void ManyValuesAreCountedInTwoBytes()
    {
        var z = new ZdoData { Prefab = 123, Position = new Vector3(1.5f, 2, 3) };
        for (int i = 0; i < 200; i++)
        {
            z.IntList.Add((i, i * 3));
        }
        var back = ZdoData.Parse(z.Serialize());
        Assert.Equal(200, back.IntList.Count);
        Assert.Equal((199, 597), back.IntList[^1]);
    }

    [Fact]
    public void UnusedHighFlagBitsComeBackAsSaved()
    {
        // ZdoBuilder keeps the bits 0x4000 and 0x8000; ZdoData dropped them on the way through the inspector.
        var z = new ZdoData { Prefab = 123, BaseFlags = 0x0100 | 0x4000 | 0x8000 };
        z.IntList.Add((1, 2));
        var back = ZdoData.Parse(z.Serialize());
        Assert.Equal(0x0100 | 0x4000 | 0x8000, back.BaseFlags);
        Assert.Equal(0x0100 | 0x4000 | 0x8000 | ZdoData.Ints, BitConverter.ToUInt16(back.Serialize(), 0));
    }

    [Fact]
    public void ValuesAreSetReplacedAndRemoved()
    {
        var z = new ZdoData();
        int k = StableHash.Of("text");
        z.Set("strings", k, "one");
        z.Set("strings", k, "two");
        Assert.Equal((k, "two"), Assert.Single(z.StringList));
        z.Set("strings", k, null);
        Assert.Empty(z.StringList);
        // Removing what is not there is fine.
        z.Set("ints", k, null);
        z.Set("bytes", k, Convert.ToBase64String(new byte[] { 1, 2, 3 }));
        Assert.Equal(new byte[] { 1, 2, 3 }, z.GetBytes(k));
        z.Set("quats", k, "0 0 0 1");
        Assert.Equal(Quaternion.Identity, z.QuatList.Single().Value);
        Assert.Throws<ArgumentException>(() => z.Set("colours", k, "1"));
        Assert.Throws<FormatException>(() => z.Set("vec3", k, "1 2"));
        // Numbers the game cannot use are refused, whole or in a vector.
        Assert.Throws<FormatException>(() => z.Set("floats", k, "NaN"));
        Assert.Throws<FormatException>(() => z.Set("floats", k, "-Infinity"));
        Assert.Throws<FormatException>(() => z.Set("floats", k, "1e40"));
        Assert.Throws<FormatException>(() => z.Set("vec3", k, "1 Infinity 3"));
        Assert.False(ZdoData.Valid("floats", "NaN"));
    }

    [Fact]
    public void AnObjectIsReadFromItsPartOfALargerBuffer()
    {
        var z = new ZdoData { Prefab = 77, Position = new Vector3(1, 2, 3) };
        z.IntList.Add((5, 42));
        byte[] own = z.Serialize();
        // Bytes around it that would read as more values if the read went past its end.
        byte[] buffer = [0x10, 0x01, .. own, 0x01, 0x05, 0x00, 0x00, 0x00];
        var read = ZdoData.Parse(buffer, 2, own.Length, 41);
        Assert.Equal(new Vector3(1, 2, 3), read.Position);
        Assert.Equal((5, 42), Assert.Single(read.IntList));
        Assert.Equal(own, read.Serialize());
    }

    [Fact]
    public void AVectorIsReplacedByItsKeyOrAdded()
    {
        var z = new ZdoData();
        z.Vec3List.Add((1, Vector3.One));
        z.Vec3List.Add((2, Vector3.One));
        z.SetVec3(2, new Vector3(4, 5, 6));
        z.SetVec3(3, Vector3.UnitY);
        Assert.Equal([(1, Vector3.One), (2, new Vector3(4, 5, 6)), (3, Vector3.UnitY)], z.Vec3List);
    }

    [Fact]
    public void AnObjectOfAnOldSaveIsRead()
    {
        // Before chunked saves (version 40): a sector before the position, and a full rotation.
        using var ms = new MemoryStream();
        using (var w = new BinaryWriter(ms))
        {
            w.Write((ushort)(0x1000 | 0x10));
            w.Write((short)3); w.Write((short)-2);
            w.Write(10f); w.Write(31f); w.Write(-20f);
            w.Write(777);
            w.Write(0f); w.Write(45f); w.Write(0f);
            w.Write((byte)1);
            w.Write(5); w.Write(42);
        }
        var z = ZdoData.Parse(ms.ToArray(), 39);
        Assert.Equal(new Vector3(10, 31, -20), z.Position);
        Assert.Equal(777, z.Prefab);
        Assert.Equal(new Vector3(0, 45, 0), z.Rotation);
        Assert.Equal((5, 42), Assert.Single(z.IntList));
    }

    [Fact]
    public void NumbersAreReadAsSetReadsThem()
    {
        // One reader of "x y z" for the save and for extensions' copies (CopyView had its own, without ';').
        Assert.Equal([1f, 2.5f, -3f], ZdoData.Numbers("1 2.5,-3", 3)!);
        Assert.Equal([1f, 2f, 3f, 4f], ZdoData.Numbers("1;2;3;4", 4)!);
        Assert.Null(ZdoData.Numbers("1 2", 3));
        Assert.Null(ZdoData.Numbers("1 x 3", 3));
        Assert.Null(ZdoData.Numbers(null, 3));
        Assert.True(ZdoData.Valid("vec3", "1;2;3"));
        Assert.False(ZdoData.Valid("vec3", "1 2"));
    }

    [Fact]
    public void AnIntIsFoundByItsKeyFirstValueFirst()
    {
        var z = new ZdoData();
        Assert.Null(z.GetInt(StableHash.Of("pose")));
        z.IntList.Add((StableHash.Of("pose"), 3));
        z.IntList.Add((StableHash.Of("pose"), 7));
        Assert.Equal(3, z.GetInt(StableHash.Of("pose")));
        Assert.True(ZdoData.TryGet(z.IntList, StableHash.Of("pose"), out int pose));
        Assert.Equal(3, pose);
        Assert.False(ZdoData.TryGet(z.StringList, StableHash.Of("pose"), out _));
    }
}
