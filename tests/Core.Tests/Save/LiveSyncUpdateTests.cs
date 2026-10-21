using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// LiveSync's changes in place, with a plugin that can ("update"): an object replaced by a changed copy of it (a mod's
/// tool's change) takes the copy's data under its own ZDOID, as a move that carries data, and its own data back on undo;
/// a change of that copy, or of one of the editor's new objects, changes the same game object again.
/// </summary>
[Collection("Timed answers")]
public sealed class LiveSyncUpdateTests
{
    private static readonly ObjectField[] Changed = [new ObjectField("strings", StableHash.Of("text"), "changed")];

    /// <summary>A rig whose plugin moves objects and changes their data in place.</summary>
    private static LiveSyncRig Rig()
    {
        var t = new LiveSyncRig();
        t.Bridge.Features.Add("update");
        return t;
    }

    /// <summary>A copy of an object, where it stands, with other data.</summary>
    private static NewObject ChangedCopy(int id, int source, int prefab, Vector3 at, Vector3 turn, ObjectField[]? data = null) =>
        new(id, prefab, at, turn, 0f, source, Fresh: false, Data: data ?? Changed);

    [Fact]
    public async Task AChangedCopyChangesTheGameObjectInPlaceAndBackOnUndo()
    {
        using var t = Rig();
        t.Edits.SetDeleted([t.Tree.Id], true);
        t.Edits.AddObjects([ChangedCopy(-1, t.Tree.Id, t.Tree.Prefab, t.Tree.Position, t.Tree.Rotation)]);
        string said = (await t.Apply()).Shown;
        Assert.Equal((0, 0), t.Bridge.ObjectCalls.Single());
        var (user, id, _, _, to, _) = t.Bridge.Moves.Single();
        Assert.Equal((t.TreeZdo, t.Tree.Position), ((user, id), to));
        Assert.True(t.Bridge.MoveData.Single());
        Assert.Equal("changed", ZdoData.Parse(t.Bridge.MoveBytes.Single()).StringList.Single(s => s.Key == StableHash.Of("text")).Value);
        Assert.Contains("1 changed", said);
        Assert.Equal((0, 0), t.Sync.Pending(t.Edits));

        // Undone: the same game object takes its own data back.
        t.Undo();
        await t.Apply();
        Assert.Equal((0, 0), t.Bridge.ObjectCalls[1]);
        Assert.Equal(t.TreeZdo, (t.Bridge.Moves[1].User, t.Bridge.Moves[1].Id));
        Assert.True(t.Bridge.MoveData[1]);
        Assert.Equal((0, 0), t.Sync.Pending(t.Edits));
    }

    [Fact]
    public async Task AChangeOfTheChangedCopyChangesTheSameGameObject()
    {
        using var t = Rig();
        t.Edits.SetDeleted([t.Tree.Id], true);
        t.Edits.AddObjects([ChangedCopy(-1, t.Tree.Id, t.Tree.Prefab, t.Tree.Position, t.Tree.Rotation)]);
        await t.Apply();

        // Changed again, back to its own data: still sent with data, as the game object has the first change's.
        t.Edits.SetDeleted([-1], true);
        t.Edits.AddObjects([ChangedCopy(-2, -1, t.Tree.Prefab, t.Tree.Position, t.Tree.Rotation, [])]);
        await t.Apply();
        Assert.Equal((0, 0), t.Bridge.ObjectCalls[1]);
        Assert.Equal(t.TreeZdo, (t.Bridge.Moves[1].User, t.Bridge.Moves[1].Id));
        Assert.True(t.Bridge.MoveData[1]);
        Assert.True(t.Sync.IsLive(-2));
        Assert.False(t.Sync.IsLive(-1));
        Assert.Equal((0, 0), t.Sync.Pending(t.Edits));
    }

    [Fact]
    public async Task ANewObjectChangedChangesItsGameObject()
    {
        using var t = Rig();
        var at = t.Tree.Position + new Vector3(0, 0, 6);
        t.Edits.AddObjects([new NewObject(-1, t.Tree.Prefab, at, Vector3.Zero, 0f, null, Fresh: true)]);
        await t.Apply();
        Assert.Equal((0, 1), t.Bridge.ObjectCalls.Single());

        t.Edits.SetDeleted([-1], true);
        t.Edits.AddObjects([ChangedCopy(-2, -1, t.Tree.Prefab, at, Vector3.Zero)]);
        await t.Apply();
        Assert.Equal((0, 0), t.Bridge.ObjectCalls[1]);
        Assert.True(t.Bridge.MoveData.Single());
        Assert.Equal(77L, t.Bridge.Moves.Single().User);
        Assert.True(t.Sync.IsLive(-2));
        Assert.False(t.Sync.IsLive(-1));
        Assert.Equal((0, 0), t.Sync.Pending(t.Edits));
    }

    [Fact]
    public async Task APlainMoveCarriesNoData()
    {
        using var t = Rig();
        t.MoveTree(-1, new Vector3(4, 0, 2));
        await t.Apply();
        Assert.False(t.Bridge.MoveData.Single());
    }
}
