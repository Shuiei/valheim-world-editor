using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The player's own game: what the log says of a bridge that does not answer (the start page asks every 2.5 seconds).
/// The rest of LocalGame is checked on a pretend home (HomeFolderTests).
/// </summary>
public sealed class LocalGameTests
{
    // A bridge that does not answer is written to the log once for the same reason, not at every probe.
    [Fact]
    public async Task ABridgeThatDoesNotAnswerIsLoggedOnceForTheSameReason()
    {
        var closed = new LocalGame.Bridge("cfg", "here", FakeGame.FreePort(), "t");
        using var log = Log.Listen();

        Assert.Null(await LocalGame.FindRunning([closed]));
        Assert.Null(await LocalGame.FindRunning([closed]));
        Assert.Single(log.Lines, l => l.Contains($"while asking the bridge on port {closed.Port} whether the game runs", StringComparison.Ordinal));
    }
}
