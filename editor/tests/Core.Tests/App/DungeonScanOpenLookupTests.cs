using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The raw reads a dungeon bundle's scan keeps for lookups: whether a ZNetView is enabled, and a GameObject's parent,
/// each answered only for what was read (an unknown id is not enabled and has no parent).
/// </summary>
public class DungeonScanOpenLookupTests
{
    /// <summary>A scan of a bundle with nothing read yet (its bundle is not used by these lookups).</summary>
    private static object EmptyOpen()
    {
        Type open = PrivateMember.Nested(PrivateMember.Nested(typeof(GameCatalogues), "DungeonScan"), "Open");
        object?[] ctorArgs = [null];
        return PrivateMember.New(open, ctorArgs);
    }

    [Fact]
    public void ANetViewIsEnabledOnlyWhenItWasReadEnabled()
    {
        object open = EmptyOpen();
        PrivateMember.Set(open, "_enabled", new Dictionary<long, bool> { [7] = true, [8] = false });
        Type type = open.GetType();
        Assert.True(PrivateMember.Call<bool>(type, "Enabled", open, 7L));
        Assert.False(PrivateMember.Call<bool>(type, "Enabled", open, 8L));
        Assert.False(PrivateMember.Call<bool>(type, "Enabled", open, 9L));
    }

    [Fact]
    public void AGameObjectsParentIsReturnedWhenItWasRead()
    {
        object open = EmptyOpen();
        PrivateMember.Set(open, "_parents", new Dictionary<long, long> { [7] = 3 });
        object?[] args = [7L, null];
        Assert.True(PrivateMember.Call<bool>(open.GetType(), "ParentOf", open, args));
        Assert.Equal(3L, args[1]);
    }

    [Fact]
    public void AGameObjectWithoutARecordedParentHasNone()
    {
        object open = EmptyOpen();
        object?[] args = [9L, null];
        Assert.False(PrivateMember.Call<bool>(open.GetType(), "ParentOf", open, args));
        Assert.Equal(0L, args[1]);
    }
}
