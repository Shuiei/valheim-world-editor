using System.Net;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The stand-in game (FakeGame) disposed: requests held by a test are let go and waited for, and an error while
/// answering one fails the disposal instead of passing unseen.
/// </summary>
public sealed class FakeGameTests
{
    /// <summary>A request to the game with its token, not waited for (it is answered or cut off later).</summary>
    private static Task<HttpResponseMessage> Ask(HttpClient http, FakeGame game, string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, game.Url + path);
        request.Headers.Add("X-Bridge-Token", game.Token);
        return http.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void AHeldRequestIsLetGoAndWaitedForOnDisposal()
    {
        var game = new FakeGame { HoldPlayers = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var http = new HttpClient();
        _ = Ask(http, game, "/players");
        Assert.True(SpinWait.SpinUntil(() => game.PlayerReads == 1, TimeSpan.FromSeconds(30)), "the request never came");

        // Returns: the held answer ended (its client is cut off with the listener), without an error.
        game.Dispose();

        Assert.True(game.HoldPlayers.Task.IsCompletedSuccessfully);
        Assert.False(game.Broken);
    }

    [Fact]
    public void AnErrorWhileAnsweringFailsTheDisposal()
    {
        var game = new FakeGame();
        // Not a status an HTTP answer can have: setting it throws.
        game.Fail["/status"] = 42;
        using var http = new HttpClient();
        _ = Ask(http, game, "/status");

        Assert.True(SpinWait.SpinUntil(() => game.Broken, TimeSpan.FromSeconds(30)), "the request was never answered");
        var e = Assert.Throws<AggregateException>(game.Dispose);

        Assert.IsType<ProtocolViolationException>(e.InnerException);
    }
}
