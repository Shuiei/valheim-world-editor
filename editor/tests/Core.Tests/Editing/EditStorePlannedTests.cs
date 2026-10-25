using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// No limit ground over the ground discs the world holds for it (EditStore.PutPlanned): not a change while it stays so,
/// one again once it changes, and forgotten when the world is read again.
/// </summary>
public class EditStorePlannedTests
{
    /// <summary>A zone lifted by <paramref name="metres"/> everywhere.</summary>
    private static ZoneEdit Lifted(float metres)
    {
        var e = new ZoneEdit(2, 3);
        Array.Fill(e.Lift, metres);
        return e;
    }

    [Fact]
    public void PlannedGroundIsNoChangeUntilItChanges()
    {
        var world = new WorldSave { Directory = "none", SaveNumber = 1 };
        var store = new EditStore(world);
        store.PutPlanned(Lifted(12f));
        Assert.True(store.AsPlanned(2, 3));
        Assert.False(store.Get(2, 3)!.Changed);
        Assert.Equal(0, store.ChangedZoneCount);
        // Put again as it is (an area taking its ground back): still none.
        store.Put(Lifted(12f));
        Assert.False(store.Get(2, 3)!.Changed);
        // Changed (an undo): a change, its discs to work out again.
        store.Put(Lifted(4f));
        Assert.True(store.Get(2, 3)!.Changed);
        Assert.False(store.AsPlanned(2, 3));
        // Read again from the world: forgotten.
        store.PutPlanned(Lifted(4f));
        store.ResetFrom(world);
        Assert.Null(store.Get(2, 3));
        store.Put(Lifted(4f));
        Assert.True(store.Get(2, 3)!.Changed);
    }

    [Fact]
    public void AFingerprintFollowsTheLiftAndTheEdits()
    {
        var a = Lifted(3f);
        long f = a.Fingerprint();
        Assert.Equal(f, Lifted(3f).Fingerprint());
        a.Lift[100] = 3.5f;
        Assert.NotEqual(f, a.Fingerprint());
        var b = Lifted(3f);
        b.Modified[7] = true;
        b.Level[7] = 1f;
        Assert.NotEqual(f, b.Fingerprint());
    }

    [Fact]
    public void ALiftIsKeptWithinReachAndNoneUnderAMillimetre()
    {
        Assert.Equal(0f, EditStore.KeepLift(0.0009f));
        Assert.Equal(-2f, EditStore.KeepLift(-2f));
        Assert.Equal(EditStore.MaxLift, EditStore.KeepLift(EditStore.MaxLift + 10));
    }
}
