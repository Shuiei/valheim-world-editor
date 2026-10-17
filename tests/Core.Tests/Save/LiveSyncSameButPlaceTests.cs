using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Two objects are the same but for where they stand and how they are turned: the prefab and the flags must match.
/// </summary>
public class LiveSyncSameButPlaceTests
{
    private static ZdoData Object(int prefab, ushort flags, Vector3 position) =>
        ZdoData.Parse(ZdoBuilder.Blank(prefab, flags, position, Vector3.Zero, 1f));

    [Fact]
    public void TheSameObjectMovedIsTheSameButPlace()
    {
        Assert.True(LiveSync.SameButPlace(Object(7, 0, Vector3.Zero), Object(7, 0, new Vector3(5, 0, 0))));
    }

    [Fact]
    public void ADifferentPrefabIsNotTheSameButPlace()
    {
        Assert.False(LiveSync.SameButPlace(Object(7, 0, Vector3.Zero), Object(8, 0, Vector3.Zero)));
    }

    [Fact]
    public void DifferentFlagsAreNotTheSameButPlace()
    {
        // 0x0100 is one of the flags an object keeps when it is read (bit 0x0001 is not kept, so it would not differ).
        Assert.False(LiveSync.SameButPlace(Object(7, 0, Vector3.Zero), Object(7, 0x0100, Vector3.Zero)));
    }
}
