using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A pasted copy's object as extensions see it (CopyView): its values read from their data text and set back as the
/// clipboard keeps them, a value taken away kept with no value, damaged text read as none.
/// </summary>
public class CopyViewTests
{
    [Fact]
    public void ValuesAreReadAndSetAsDataText()
    {
        var data = new List<ObjectField> { new("ints", StableHash.Of("n"), "7"), new("vec3", StableHash.Of("v"), "1 2 3"), new("bytes", StableHash.Of("b"), "AQI="),
            new("floats", StableHash.Of("bad"), "x") };
        var c = new CopyView("Cart", new Vector3(1, 2, 3), Vector3.Zero, data);

        Assert.True(c.TryGetInt("n", out int n) && n == 7);
        Assert.True(c.TryGetVector3("v", out var v) && v == new Vector3(1, 2, 3));
        Assert.True(c.TryGetBytes("b", out byte[] b) && b.SequenceEqual(new byte[] { 1, 2 }));
        Assert.False(c.TryGetFloat("bad", out _));
        Assert.False(c.TryGetString("none", out _));

        c.SetInt("n", 8);
        c.SetVector3("w", new Vector3(0.5f, 0, -1));
        c.SetBytes("b", null);
        c.SetString("s", "hi");
        c.SetLong("l", 1L << 40);
        c.SetQuaternion("q", new Quaternion(0, 0.5f, 0, -1));

        Assert.Equal("8", data.Single(f => f.Key == StableHash.Of("n")).Value);
        Assert.Equal("0.5 0 -1", data.Single(f => f.Key == StableHash.Of("w")).Value);
        Assert.Null(data.Single(f => f.Key == StableHash.Of("b")).Value);
        Assert.False(c.Holds(StableHash.Of("b")));
        Assert.True(c.TryGetString("s", out string s) && s == "hi");
        Assert.True(c.TryGetLong("l", out long l) && l == 1L << 40);
        Assert.Equal("0 0.5 0 -1", data.Single(f => f.Key == StableHash.Of("q")).Value);
        Assert.True(c.TryGetQuaternion("q", out var q) && q == new Quaternion(0, 0.5f, 0, -1));
        c.SetQuaternion("q", null);
        Assert.False(c.Holds(StableHash.Of("q")));
        Assert.Equal("Cart", c.Prefab);
    }
}
