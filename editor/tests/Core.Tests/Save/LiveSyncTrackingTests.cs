using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The live sync's count of the game objects it has put under its own control: a live id counts once for its object,
/// setting an id again replaces what it held, and an id that is not live changes nothing.
/// </summary>
public class LiveSyncTrackingTests
{
    private static readonly LiveTarget Tree = new(5, 9, 1, Vector3.Zero);

    /// <summary>The live sync's count of its own objects, by game object.</summary>
    private static Dictionary<(long User, uint Id), int> Ours(LiveSync sync) => (Dictionary<(long User, uint Id), int>)PrivateCall.Field(sync, "_ours")!;

    /// <summary>The live sync's target of each live id.</summary>
    private static Dictionary<int, LiveTarget> LiveIds(LiveSync sync) => (Dictionary<int, LiveTarget>)PrivateCall.Field(sync, "_liveIds")!;

    [Fact]
    public void ASetLiveIdCountsOnceForItsGameObject()
    {
        var sync = new LiveSync();
        PrivateCall.Instance(sync, "SetLive", 1, Tree);
        Assert.Equal(Tree, LiveIds(sync)[1]);
        Assert.Equal(1, Ours(sync)[(5L, 9u)]);
    }

    [Fact]
    public void SettingTheSameIdAgainKeepsOneCount()
    {
        var sync = new LiveSync();
        PrivateCall.Instance(sync, "SetLive", 1, Tree);
        PrivateCall.Instance(sync, "SetLive", 1, Tree);
        Assert.Equal(1, Ours(sync)[(5L, 9u)]);
    }

    [Fact]
    public void TwoIdsOnOneGameObjectCountTwiceAndEachDropTakesOneOff()
    {
        var sync = new LiveSync();
        PrivateCall.Instance(sync, "SetLive", 1, Tree);
        PrivateCall.Instance(sync, "SetLive", 2, Tree);
        Assert.Equal(2, Ours(sync)[(5L, 9u)]);
        PrivateCall.Instance(sync, "DropLive", 1);
        Assert.Equal(1, Ours(sync)[(5L, 9u)]);
        PrivateCall.Instance(sync, "DropLive", 2);
        Assert.False(Ours(sync).ContainsKey((5L, 9u)));
    }

    [Fact]
    public void DroppingAnIdThatIsNotLiveChangesNothing()
    {
        var sync = new LiveSync();
        PrivateCall.Instance(sync, "SetLive", 1, Tree);
        PrivateCall.Instance(sync, "DropLive", 7);
        Assert.Equal(1, Ours(sync)[(5L, 9u)]);
        Assert.Single(LiveIds(sync));
    }
}
