using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Two objects are the same but for where they stand only when every value of each kind matches, in any order
/// (LiveSync.SameButPlace): floats, points, turns, ints, longs, texts and bytes.
/// </summary>
public class LiveSyncSameButPlaceValuesTests
{
    /// <summary>An object with one value of each kind, standing at <paramref name="position"/>.</summary>
    private static ZdoData Object(Vector3 position, int number = 7, string text = "sign")
    {
        var z = ZdoData.Parse(ZdoBuilder.Blank(7, 0, position, Vector3.Zero, 1f));
        z.FloatList.Add((1, 1.5f));
        z.Vec3List.Add((2, new Vector3(1, 2, 3)));
        z.QuatList.Add((3, Quaternion.Identity));
        z.IntList.Add((4, number));
        z.LongList.Add((5, 123456789012L));
        z.StringList.Add((6, text));
        z.ByteList.Add((7, [1, 2, 3]));
        return z;
    }

    [Fact]
    public void TheSameValuesInAnotherOrderAreTheSame()
    {
        var a = Object(Vector3.Zero);
        var b = Object(new Vector3(5, 0, 0));
        b.IntList.Add((8, 1));
        a.IntList.Insert(0, (8, 1));

        Assert.True(LiveSync.SameButPlace(a, b));
    }

    [Fact]
    public void AValueThatDiffersIsNotTheSame()
    {
        Assert.False(LiveSync.SameButPlace(Object(Vector3.Zero), Object(Vector3.Zero, number: 8)));
        Assert.False(LiveSync.SameButPlace(Object(Vector3.Zero), Object(Vector3.Zero, text: "other")));
        var fewer = Object(Vector3.Zero);
        fewer.ByteList.Clear();
        Assert.False(LiveSync.SameButPlace(Object(Vector3.Zero), fewer));
    }
}
