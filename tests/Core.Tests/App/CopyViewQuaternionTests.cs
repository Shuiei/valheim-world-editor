using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A copied object's quaternion value: four numbers, read whole or not at all (CopyView.TryGetQuaternion).
/// </summary>
public class CopyViewQuaternionTests
{
    /// <summary>A copy of an object at (1, 2, 3) carrying these data fields.</summary>
    private static CopyView Copy(params ObjectField[] fields) =>
        new("Cart", new Vector3(1, 2, 3), Vector3.Zero, [.. fields]);

    [Fact]
    public void FourNumbersAreReadAsAQuaternionInXYZWOrder()
    {
        var c = Copy(new ObjectField("quats", StableHash.Of("q"), "0 0 0 1"));

        Assert.True(c.TryGetQuaternion("q", out var q));
        Assert.Equal(Quaternion.Identity, q);
    }

    [Fact]
    public void AMissingQuaternionIsNotRead()
    {
        Assert.False(Copy().TryGetQuaternion("q", out _));
    }

    [Fact]
    public void ThreeNumbersAreNotAQuaternion()
    {
        var c = Copy(new ObjectField("quats", StableHash.Of("q"), "0 0 1"));

        Assert.False(c.TryGetQuaternion("q", out _));
    }
}
