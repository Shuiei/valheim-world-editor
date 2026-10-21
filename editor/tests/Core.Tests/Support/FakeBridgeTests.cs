using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The stand-in plugin (FakeBridge) disposed: an error while it answered fails the disposal instead of ending its
/// answering unseen.
/// </summary>
public sealed class FakeBridgeTests
{
    [Fact]
    public void AnErrorWhileAnsweringFailsTheDisposal()
    {
        var bridge = new FakeBridge();
        using var http = new HttpClient();
        // An objects call too short to hold even its count of objects to destroy.
        using var call = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{bridge.Port}/objects") { Content = new ByteArrayContent([1, 2]) };
        call.Headers.Add("X-Bridge-Token", bridge.Token);
        _ = http.SendAsync(call, TestContext.Current.CancellationToken);

        Assert.True(SpinWait.SpinUntil(() => bridge.Broken, TimeSpan.FromSeconds(30)), "the call was never read");
        var e = Assert.Throws<AggregateException>(bridge.Dispose);

        Assert.IsType<EndOfStreamException>(e.InnerException);
    }
}
