using System.Numerics;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The watch calls: a zone's two numbers (what stays put, and what moves or ticks on its own), the movers read alone,
/// and a plugin from before the second number.
/// </summary>
public sealed class LiveBridgeWatchTests
{
    [Fact]
    public async Task AZoneHasAStillNumberAndAMovingNumberAndItsMoversAreReadAlone()
    {
        using var game = new FakeGame();
        using var live = new LiveBridge(game.Url, game.Token);
        var at = new Vector3(640, 30, 640);
        game.Zones[(10, 10)] = (7, [(4242, 1, MadeObjects.Zdo("woodwall", at))]);
        game.Movers[(10, 10)] = (3, [(4242, 2, MadeObjects.Zdo("Deer", at + new Vector3(1, 0, 0)))]);
        game.Zones[(11, 10)] = (9, []);

        var digests = await live.Watch([(10, 10), (11, 10)]);
        Assert.Equal(new LiveBridge.Digests(7, 3), digests[(10, 10)]);
        Assert.Equal(new LiveBridge.Digests(9, 0), digests[(11, 10)]);

        var movers = await live.MovingObjects([(10, 10), (11, 10)]);
        var (user, id, bytes) = Assert.Single(movers);
        Assert.Equal((4242L, 2u), (user, id));
        Assert.Equal(at + new Vector3(1, 0, 0), ZdoData.Parse(bytes).Position);
        Assert.Equal(2, (await live.ZoneObjects([(10, 10)])).Count);
    }

    // A plugin from before 1.57.2 gives one number per zone: the second is 0, and the movers are never asked for.
    [Fact]
    public async Task AnOlderPluginGivesOneNumberAndNoMovers()
    {
        using var game = new FakeGame { FollowsMovers = false };
        using var live = new LiveBridge(game.Url, game.Token);
        game.Zones[(10, 10)] = (7, []);

        var digests = await live.Watch([(10, 10)]);
        Assert.Equal(new LiveBridge.Digests(7, 0), digests[(10, 10)]);

        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => live.MovingObjects([(10, 10)]));
        Assert.Contains("too old for zone/movers", e.Message);
    }
}
