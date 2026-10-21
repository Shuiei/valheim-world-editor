using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The bridge's catalogue and model queries (LiveBridge) against FakeBridge, which answers a route it does not serve
/// with an empty body: the request goes out, and its answer is read back.
/// </summary>
public class LiveBridgeQueriesTests
{
    [Fact]
    public async Task CatalogueReadsTheBridgesAnswer()
    {
        using var bridge = new FakeBridge();
        using var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token);
        Assert.Equal("", await live.Catalogue());
    }

    [Fact]
    public async Task ModelsSendsThePrefabsAndReadsTheBridgesAnswer()
    {
        using var bridge = new FakeBridge();
        using var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token);
        Assert.Equal("", await live.Models(["Wood", "Stone"]));
    }
}
