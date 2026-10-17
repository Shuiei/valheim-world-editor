using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// LiveSync's moves: a saved object moved in the editor moves in the game under its own ZDOID (FakeBridge), and the edits
/// that move things are sent as moves, copies or made again.
/// </summary>
[Collection("Timed answers")]
public class LiveSyncTests
{
    // Moved, the game object goes there under its ZDOID (nothing destroyed or made), and comes back the same way on undo.
    [Fact]
    public async Task AMovedObjectMovesUnderItsOwnIdAndBackOnUndo()
    {
        using var t = new LiveSyncRig();
        var n = t.MoveTree(-1, new Vector3(4, 0, 2));
        string said = (await t.Apply()).Shown;
        Assert.Equal((0, 0), t.Bridge.ObjectCalls.Single());
        var (user, id, prefab, from, to, turn) = t.Bridge.Moves.Single();
        Assert.Equal(t.TreeZdo, (user, id));
        Assert.Equal((t.Tree.Prefab, t.Tree.Position, n.Position, n.Rotation), (prefab, from, to, turn));
        Assert.Contains("1 moved", said);
        Assert.Equal((0, 0), t.Sync.Pending(t.Edits));
        Assert.True(t.Sync.IsLive(-1));
        Assert.True(t.Sync.IsDestroyed(t.Tree.Id));

        t.Undo();
        await t.Apply();
        Assert.Equal((0, 0), t.Bridge.ObjectCalls[1]);
        var (backUser, backId, _, backFrom, backTo, _) = t.Bridge.Moves[1];
        Assert.Equal((t.TreeZdo, n.Position, t.Tree.Position), ((backUser, backId), backFrom, backTo));
        Assert.Equal((0, 0), t.Sync.Pending(t.Edits));
        Assert.False(t.Sync.IsDestroyed(t.Tree.Id));
        Assert.False(t.Sync.IsLive(-1));
    }

    // What the plugin can do is asked at each apply: a check that fails refuses the apply (a move was sent as a destroy
    // and a creation, the object getting a new id), and a plugin that can move since is used to.
    [Fact]
    public async Task WhatThePluginCanDoIsAskedAtEachApply()
    {
        using var t = new LiveSyncRig(canMove: false);
        t.Bridge.StatusFails = true;
        t.MoveTree(-1, new Vector3(4, 0, 2));
        await Assert.ThrowsAsync<InvalidOperationException>(t.Apply);
        Assert.Empty(t.Bridge.ObjectCalls);

        t.Bridge.StatusFails = false;
        t.Bridge.Features.Add("move");
        await t.Apply();
        Assert.Equal((0, 0), t.Bridge.ObjectCalls.Single());
        Assert.Single(t.Bridge.Moves);
    }

    // Deleted objects with no copy are destroyed without their bytes being read: only the moved one is read.
    [Fact]
    public async Task OnlyObjectsThatMayMoveAreRead()
    {
        using var t = new LiveSyncRig();
        int[] others = [.. t.World.Objects.Where(o => o.Id != t.Tree.Id && !t.World.Links.ContainsKey(o.Id)).Take(20).Select(o => o.Id)];
        t.Edits.SetDeleted(others, true);
        t.MoveTree(-1, new Vector3(4, 0, 2));
        await t.Apply();
        Assert.Equal((others.Length, 0), t.Bridge.ObjectCalls.Single());
        Assert.Single(t.Bridge.Moves);
        Assert.Equal(1, t.Sync.SourceReads);
    }

    // Moved on from where it was moved to: the same game object moves again.
    [Fact]
    public async Task AnObjectMovedAgainMovesTheSameGameObject()
    {
        using var t = new LiveSyncRig();
        var first = t.MoveTree(-1, new Vector3(4, 0, 2));
        await t.Apply();
        var second = t.MoveTree(-2, new Vector3(9, 0, -3), from: -1);
        await t.Apply();
        Assert.Equal((0, 0), t.Bridge.ObjectCalls[1]);
        var (user, id, _, from, to, _) = t.Bridge.Moves[1];
        Assert.Equal((t.TreeZdo, first.Position, second.Position), ((user, id), from, to));
        Assert.False(t.Sync.IsLive(-1));
        Assert.True(t.Sync.IsLive(-2));
    }

    // A paste of a cut: a new copy holding all the object's data is a move too.
    [Fact]
    public async Task APastedCutMoves()
    {
        using var t = new LiveSyncRig();
        var data = ObjectData.Fields(t.World.ObjectBytes(t.Tree.Id), builder: true);
        t.Edits.SetDeleted([t.Tree.Id], true);
        t.Edits.AddObjects([new NewObject(-1, t.Tree.Prefab, t.Tree.Position + new Vector3(0, 0, 6), t.Tree.Rotation, 0f, t.Tree.Id, Fresh: true, Data: data)]);
        await t.Apply();
        Assert.Equal((0, 0), t.Bridge.ObjectCalls.Single());
        Assert.Single(t.Bridge.Moves);
    }

    // A copy that differs by more than its place (a value changed), or a plugin that cannot move: destroyed and made.
    [Fact]
    public async Task AChangedCopyOrAnOlderPluginIsMadeAgain()
    {
        using (var t = new LiveSyncRig())
        {
            t.Edits.SetDeleted([t.Tree.Id], true);
            t.Edits.AddObjects([new NewObject(-1, t.Tree.Prefab, t.Tree.Position + new Vector3(4, 0, 2), t.Tree.Rotation, 0f, t.Tree.Id, Fresh: false,
                Data: [new ObjectField("strings", StableHash.Of("text"), "changed")])]);
            await t.Apply();
            Assert.Equal((1, 1), t.Bridge.ObjectCalls.Single());
            Assert.Empty(t.Bridge.Moves);
        }
        using (var t = new LiveSyncRig(canMove: false))
        {
            t.MoveTree(-1, new Vector3(4, 0, 2));
            await t.Apply();
            Assert.Equal((1, 1), t.Bridge.ObjectCalls.Single());
            Assert.Empty(t.Bridge.Moves);
        }
    }

    // A player has both places loaded: the plugin makes it again there (a new id), and an undo moves that one back.
    [Fact]
    public async Task AMoveMadeAgainIsFollowedByItsNewId()
    {
        using var t = new LiveSyncRig();
        t.Bridge.MoveInPlace = false;
        t.MoveTree(-1, new Vector3(4, 0, 2));
        string said = (await t.Apply()).Shown;
        Assert.Contains("0 moved (1 more made again", said);
        Assert.True(t.Sync.IsLive(-1));
        var made = t.Sync.NewObjects().Single().Target;
        Assert.NotEqual(t.TreeZdo, (made.User, made.Id));

        t.Bridge.MoveInPlace = true;
        t.Undo();
        await t.Apply();
        var (user, id, _, _, _, _) = t.Bridge.Moves[1];
        Assert.Equal((made.User, made.Id), (user, id));
        // Back in the game under that id, which the world follows it by now.
        Assert.Equal((made.User, made.Id), t.World.ObjectRefs[t.Tree.Id].LiveId);
    }

    // The moved object's game object is the editor's now: a zone read again does not take it back in as the saved one.
    [Fact]
    public async Task AMovedObjectIsNotReadBackInAsTheSavedOne()
    {
        using var t = new LiveSyncRig();
        var n = t.MoveTree(-1, new Vector3(4, 0, 2));
        await t.Apply();
        var z = ZdoData.Parse(t.World.ObjectBytes(t.Tree.Id), t.World.ObjectRefs[t.Tree.Id].File.WorldVersion);
        z.Position = n.Position;
        var (user, id) = t.TreeZdo;
        var merged = t.World.MergeLive([t.World.ObjectRefs[t.Tree.Id].Zone], [(user, id, z.Serialize())], t.Sync.IsDestroyed, t.Sync.IsOurs)!;
        Assert.DoesNotContain(t.Tree.Id, merged.Updated);
        Assert.Empty(merged.Added);
        Assert.Equal(t.Tree.Position, t.World.ObjectRefs[t.Tree.Id].Position);
    }
}
