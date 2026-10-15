using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// LiveSync's moves: a saved object moved in the editor moves in the game under its own ZDOID (FakeBridge); and a call
/// whose answer was lost is sent again under its id, not done twice.
/// </summary>
[Collection("Timed answers")]
public class LiveSyncTests
{
    /// <summary>A world, its edits and a sync, against a bridge that moves objects unless told otherwise.</summary>
    private sealed class Live : IDisposable
    {
        public FakeBridge Bridge { get; } = new();
        public WorldSave World { get; } = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        public EditStore Edits { get; }
        public LiveSync Sync { get; } = new();
        public LiveBridge Game { get; }
        public (int Id, int Prefab, Vector3 Position, Vector3 Rotation, Vector3 Scale) Tree { get; }

        public Live(bool canMove = true)
        {
            if (canMove)
            {
                Bridge.Features.Add("move");
            }
            Edits = new EditStore(World);
            Game = new LiveBridge($"http://127.0.0.1:{Bridge.Port}", Bridge.Token);
            Tree = World.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
        }

        /// <summary>The tree moved as the editor moves things: deleted, with a copy keeping its data at the new place.</summary>
        public NewObject MoveTree(int newId, Vector3 offset, int? from = null)
        {
            if (from is int old)
            {
                Edits.SetDeleted([old], true);
            }
            else
            {
                Edits.SetDeleted([Tree.Id], true);
            }
            var n = new NewObject(newId, Tree.Prefab, Tree.Position + offset, Tree.Rotation + new Vector3(0, 30, 0), 0f, Tree.Id, Fresh: false);
            Edits.AddObjects([n]);
            return n;
        }

        public (long, uint) TreeZdo => World.ObjectRefs[Tree.Id].LiveId;

        public void Dispose()
        {
            Game.Dispose();
            Bridge.Dispose();
        }
    }

    // Moved, the game object goes there under its ZDOID (nothing destroyed or made), and comes back the same way on undo.
    [Fact]
    public async Task AMovedObjectMovesUnderItsOwnIdAndBackOnUndo()
    {
        using var t = new Live();
        var n = t.MoveTree(-1, new Vector3(4, 0, 2));
        string said = await t.Sync.Apply(t.World, t.Edits, t.Game);
        Assert.Equal((0, 0), t.Bridge.ObjectCalls.Single());
        var (user, id, prefab, from, to, turn) = t.Bridge.Moves.Single();
        Assert.Equal(t.TreeZdo, (user, id));
        Assert.Equal((t.Tree.Prefab, t.Tree.Position, n.Position, n.Rotation), (prefab, from, to, turn));
        Assert.Contains("1 moved", said);
        Assert.Equal((0, 0), t.Sync.Pending(t.Edits));
        Assert.True(t.Sync.IsLive(-1));
        Assert.True(t.Sync.IsDestroyed(t.Tree.Id));

        t.Edits.SetDeleted([-1], true);
        t.Edits.SetDeleted([t.Tree.Id], false);
        await t.Sync.Apply(t.World, t.Edits, t.Game);
        Assert.Equal((0, 0), t.Bridge.ObjectCalls[1]);
        var (backUser, backId, _, backFrom, backTo, _) = t.Bridge.Moves[1];
        Assert.Equal((t.TreeZdo, n.Position, t.Tree.Position), ((backUser, backId), backFrom, backTo));
        Assert.Equal((0, 0), t.Sync.Pending(t.Edits));
        Assert.False(t.Sync.IsDestroyed(t.Tree.Id));
        Assert.False(t.Sync.IsLive(-1));
    }

    // Moved on from where it was moved to: the same game object moves again.
    [Fact]
    public async Task AnObjectMovedAgainMovesTheSameGameObject()
    {
        using var t = new Live();
        var first = t.MoveTree(-1, new Vector3(4, 0, 2));
        await t.Sync.Apply(t.World, t.Edits, t.Game);
        var second = t.MoveTree(-2, new Vector3(9, 0, -3), from: -1);
        await t.Sync.Apply(t.World, t.Edits, t.Game);
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
        using var t = new Live();
        var data = ObjectData.Fields(t.World.ObjectBytes(t.Tree.Id), builder: true);
        t.Edits.SetDeleted([t.Tree.Id], true);
        t.Edits.AddObjects([new NewObject(-1, t.Tree.Prefab, t.Tree.Position + new Vector3(0, 0, 6), t.Tree.Rotation, 0f, t.Tree.Id, Fresh: true, Data: data)]);
        await t.Sync.Apply(t.World, t.Edits, t.Game);
        Assert.Equal((0, 0), t.Bridge.ObjectCalls.Single());
        Assert.Single(t.Bridge.Moves);
    }

    // A copy that differs by more than its place (a value changed), or a plugin that cannot move: destroyed and made.
    [Fact]
    public async Task AChangedCopyOrAnOlderPluginIsMadeAgain()
    {
        using (var t = new Live())
        {
            t.Edits.SetDeleted([t.Tree.Id], true);
            t.Edits.AddObjects([new NewObject(-1, t.Tree.Prefab, t.Tree.Position + new Vector3(4, 0, 2), t.Tree.Rotation, 0f, t.Tree.Id, Fresh: false,
                Data: [new ObjectField("strings", StableHash.Of("text"), "changed")])]);
            await t.Sync.Apply(t.World, t.Edits, t.Game);
            Assert.Equal((1, 1), t.Bridge.ObjectCalls.Single());
            Assert.Empty(t.Bridge.Moves);
        }
        using (var t = new Live(canMove: false))
        {
            t.MoveTree(-1, new Vector3(4, 0, 2));
            await t.Sync.Apply(t.World, t.Edits, t.Game);
            Assert.Equal((1, 1), t.Bridge.ObjectCalls.Single());
            Assert.Empty(t.Bridge.Moves);
        }
    }

    // A player has both places loaded: the plugin makes it again there (a new id), and an undo moves that one back.
    [Fact]
    public async Task AMoveMadeAgainIsFollowedByItsNewId()
    {
        using var t = new Live();
        t.Bridge.MoveInPlace = false;
        t.MoveTree(-1, new Vector3(4, 0, 2));
        string said = await t.Sync.Apply(t.World, t.Edits, t.Game);
        Assert.Contains("0 moved (1 more made again", said);
        Assert.True(t.Sync.IsLive(-1));
        var made = t.Sync.NewObjects().Single().Target;
        Assert.NotEqual(t.TreeZdo, (made.User, made.Id));

        t.Bridge.MoveInPlace = true;
        t.Edits.SetDeleted([-1], true);
        t.Edits.SetDeleted([t.Tree.Id], false);
        await t.Sync.Apply(t.World, t.Edits, t.Game);
        var (user, id, _, _, _, _) = t.Bridge.Moves[1];
        Assert.Equal((made.User, made.Id), (user, id));
        // Back in the game under that id, which the world follows it by now.
        Assert.Equal((made.User, made.Id), t.World.ObjectRefs[t.Tree.Id].LiveId);
    }

    // The moved object's game object is the editor's now: a zone read again does not take it back in as the saved one.
    [Fact]
    public async Task AMovedObjectIsNotReadBackInAsTheSavedOne()
    {
        using var t = new Live();
        var n = t.MoveTree(-1, new Vector3(4, 0, 2));
        await t.Sync.Apply(t.World, t.Edits, t.Game);
        var z = ZdoData.Parse(t.World.ObjectBytes(t.Tree.Id), t.World.ObjectRefs[t.Tree.Id].File.WorldVersion);
        z.Position = n.Position;
        var (user, id) = t.TreeZdo;
        var merged = t.World.MergeLive([t.World.ObjectRefs[t.Tree.Id].Zone], [(user, id, z.Serialize())], t.Sync.IsDestroyed, t.Sync.IsOurs)!;
        Assert.DoesNotContain(t.Tree.Id, merged.Updated);
        Assert.Empty(merged.Added);
        Assert.Equal(t.Tree.Position, t.World.ObjectRefs[t.Tree.Id].Position);
    }

    // The game does the call but its answer is lost (a dropped tunnel): the next apply sends it again under its id, and
    // the plugin answers with what it did instead of making the objects twice.
    [Fact]
    public async Task ACallWhoseAnswerWasLostIsNotDoneTwice()
    {
        using var t = new Live(canMove: false);
        t.Bridge.Features.Add("once");
        t.MoveTree(-1, new Vector3(4, 0, 2));
        t.Bridge.LoseNextAnswer = true;
        await Assert.ThrowsAnyAsync<Exception>(() => t.Sync.Apply(t.World, t.Edits, t.Game));
        Assert.Equal((1, 1), t.Bridge.ObjectCalls.Single());
        Assert.False(t.Sync.IsLive(-1));

        string said = await t.Sync.Apply(t.World, t.Edits, t.Game);
        Assert.Equal("1 object(s) removed, 1 created", said);
        Assert.Single(t.Bridge.ObjectCalls);
        Assert.True(t.Sync.IsLive(-1));
        Assert.True(t.Sync.IsDestroyed(t.Tree.Id));
        Assert.Equal((0, 0), t.Sync.Pending(t.Edits));
        Assert.Equal("", await t.Sync.Apply(t.World, t.Edits, t.Game));
        Assert.Single(t.Bridge.ObjectCalls);
    }
}
