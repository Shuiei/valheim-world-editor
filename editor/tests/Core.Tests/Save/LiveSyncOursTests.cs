using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What the next Apply live sends, as the window counts it (LiveSync.Pending): applied changes undone count as what the
/// game is then asked to do.
/// </summary>
[Collection("Timed answers")]
public class LiveSyncOursTests
{
    // A tree planted and applied, then undone, is destroyed in the game: a deletion, not an addition. A tree deleted and
    // applied, then undone, is made again there: an addition. (They were counted the other way round, so the top bar
    // said "1 added" for a tree about to be taken out of the game.)
    [Fact]
    public async Task AppliedChangesUndoneCountAsWhatTheGameIsAskedToDo()
    {
        using var bridge = new FakeBridge();
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(world);
        var sync = new LiveSync();
        using var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token);
        var (id, _, position, _, _) = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
        edits.AddObjects([new NewObject(-1, Fixtures.Hash("Beech1"), position + new Vector3(3, 0, 3), Vector3.Zero, 0f)]);
        await sync.Apply(world, edits, live);
        Assert.Equal((0, 0), sync.Pending(edits));
        edits.SetDeleted([-1], true);
        Assert.Equal((1, 0), sync.Pending(edits));
        edits.SetDeleted([-1], false);
        edits.SetDeleted([id], true);
        await sync.Apply(world, edits, live);
        Assert.Equal((0, 0), sync.Pending(edits));
        edits.SetDeleted([id], false);
        Assert.Equal((0, 1), sync.Pending(edits));
    }
}
