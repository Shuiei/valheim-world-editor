using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Sending new terrain to the live game (LiveBridge.ApplyTerrain) against FakeBridge: a bridge answer that is not a
/// success is raised with its status, not passed on as an answer.
/// </summary>
public class LiveBridgeTerrainTests
{
    [Fact]
    public async Task ASuccessfulTerrainAnswerIsReturned()
    {
        using var bridge = new FakeBridge();
        using var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token);
        Assert.Equal("", await live.ApplyTerrain([(0, 0, [1, 2, 3])]));
    }

    [Fact]
    public async Task AFailedTerrainAnswerIsRaisedWithItsStatus()
    {
        using var bridge = new FakeBridge { Status = 500 };
        using var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token);
        var e = await Assert.ThrowsAsync<BridgeAnswerException>(() => live.ApplyTerrain([(0, 0, [1])]));
        Assert.Equal(500, e.Status);
        Assert.Contains("bridge terrain: 500", e.Message);
    }
}
