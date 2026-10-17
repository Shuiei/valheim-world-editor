using System.Numerics;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// LiveSync's identity: the game objects the editor has are followed by their ZDOIDs, and a call whose answer was lost is
/// sent again under its id, not done twice.
/// </summary>
[Collection("Timed answers")]
public class LiveSyncIdentityTests
{
    // IsOurs follows the game objects the editor has: moved there, moved on (counted once), forgotten, and reset.
    [Fact]
    public async Task IsOursFollowsTheGameObjectsTheEditorHas()
    {
        using var t = new LiveSyncRig();
        Assert.False(t.Sync.IsOurs(t.TreeZdo));
        t.MoveTree(-1, new Vector3(4, 0, 2));
        await t.Apply();
        Assert.True(t.Sync.IsOurs(t.TreeZdo));
        t.MoveTree(-2, new Vector3(9, 0, -3), from: -1);
        await t.Apply();
        Assert.True(t.Sync.IsOurs(t.TreeZdo));
        t.Sync.Forget(-2);
        Assert.False(t.Sync.IsOurs(t.TreeZdo));

        using var u = new LiveSyncRig();
        u.MoveTree(-1, new Vector3(4, 0, 2));
        await u.Apply();
        Assert.True(u.Sync.IsOurs(u.TreeZdo));
        u.Sync.Reset();
        Assert.False(u.Sync.IsOurs(u.TreeZdo));
    }

    // Without moves, IsOurs follows what the game makes (a new object, then the deleted one made again) and what it
    // destroys again (the new object on undo, the made-again one on a second delete).
    [Fact]
    public async Task IsOursFollowsWhatIsMadeAndDestroyed()
    {
        using var t = new LiveSyncRig(canMove: false);
        t.MoveTree(-1, new Vector3(4, 0, 2));
        await t.Apply();
        var made = t.Sync.NewObjects().Single().Target;
        Assert.True(t.Sync.IsOurs((made.User, made.Id)));
        t.Undo();
        await t.Apply();
        Assert.False(t.Sync.IsOurs((made.User, made.Id)));
        // The tree made again: the bridge's next id.
        (long, uint) again = (made.User, made.Id + 1);
        Assert.True(t.Sync.IsOurs(again));
        t.Edits.SetDeleted([t.Tree.Id], true);
        await t.Apply();
        Assert.False(t.Sync.IsOurs(again));
    }

    // The game does the call but its answer is lost (a dropped tunnel): the next apply sends it again under its id, and
    // the plugin answers with what it did instead of making the objects twice.
    [Fact]
    public async Task ACallWhoseAnswerWasLostIsNotDoneTwice()
    {
        using var t = new LiveSyncRig(canMove: false);
        t.Bridge.Features.Add("once");
        t.MoveTree(-1, new Vector3(4, 0, 2));
        t.Bridge.LoseNextAnswer = true;
        await Assert.ThrowsAnyAsync<Exception>(t.Apply);
        Assert.Equal((1, 1), t.Bridge.ObjectCalls.Single());
        Assert.False(t.Sync.IsLive(-1));

        string said = (await t.Apply()).Shown;
        Assert.Equal("1 object(s) removed, 1 created", said);
        Assert.Single(t.Bridge.ObjectCalls);
        Assert.True(t.Sync.IsLive(-1));
        Assert.True(t.Sync.IsDestroyed(t.Tree.Id));
        Assert.Equal((0, 0), t.Sync.Pending(t.Edits));
        Assert.Equal("", (await t.Apply()).Shown);
        Assert.Single(t.Bridge.ObjectCalls);
    }

    // The game started again after a call whose answer was lost: the new run does not know whether the last one did it,
    // so the apply fails without sending anything until the world is read again.
    [Fact]
    public async Task ACallWhoseAnswerWasLostIsNotSentToTheGameStartedAgain()
    {
        using var t = new LiveSyncRig(canMove: false);
        t.Bridge.Features.Add("once");
        t.MoveTree(-1, new Vector3(4, 0, 2));
        t.Bridge.LoseNextAnswer = true;
        await Assert.ThrowsAnyAsync<Exception>(t.Apply);
        t.Bridge.Run = "run2";

        await Assert.ThrowsAsync<InvalidOperationException>(t.Apply);
        Assert.Single(t.Bridge.ObjectCalls);
        Assert.False(t.Sync.IsLive(-1));
    }
}
