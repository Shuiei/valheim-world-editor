using Avalonia.Headless.XUnit;
using TerrainEditor.Save;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The players of a live world on the map page: a game slow to answer is asked once at a time, a world shown again
/// before its answer is not asked twice, and a broken answer says so.
/// </summary>
[Collection("World files")]
public class MapPagePlayersTests
{
    /// <summary>
    /// A shown main window connecting to a live game, its map page showing the Connecting line.
    /// </summary>
    private static async Task<MainWindow> ConnectingTo(FakeGame game)
    {
        var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
        w.Show();
        await w.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label()), "Connecting…");
        return w;
    }

    [AvaloniaFact]
    public async Task ThePlayersLineSaysWhenTheGameDoesNotAnswer()
    {
        foreach (var broken in new Action<FakeGame>[] { g => g.Fail["/players"] = 500, g => g.Players = "this is not json" })
        {
            using var game = new FakeGame();
            broken(game);
            var w = new MainWindow(load: false) { Width = 1600, Height = 1000 };
            w.Show();
            await w.OpenWorld(() => WorldSession.OpenLive(new LiveBridge(game.Url, game.Token), FakeGame.Label()), "Connecting…");
            await TestWindows.Until(() => w.MapPage!.LiveText.Text == "Live world · the game does not answer right now.");
            w.MapPage!.Stop();
        }
    }

    // The Workshop opened over a live world closes the world: the map no longer asks the closed connection for its
    // players (it did, every few seconds: the players line then said the game did not answer, and the log got a
    // disposed-client line each time).
    [AvaloniaFact]
    public async Task TheWorkshopOpenedOverALiveWorldEndsItsPlayersPolling()
    {
        using var game = new FakeGame();
        var w = await ConnectingTo(game);
        var m = w.MapPage!;
        await TestWindows.Until(() => m.LiveText.Text == "Live world · 2 player(s) online: Ada, Bjorn");
        w.Ask = (_, _, _, _) => Task.FromResult(true);
        w.Tell = _ => Task.CompletedTask;
        await w.OpenWorkshop(null);
        Assert.True(w.InWorkshop);
        Assert.Null(w.World);
        int asked = game.PlayerReads;
        await m.PollPlayers();
        Assert.Equal(asked, game.PlayerReads);
        Assert.Equal("Live world · 2 player(s) online: Ada, Bjorn", m.LiveText.Text);
        Assert.Empty(m.Map.Players);
        w.CloseWithoutAsking();
    }

    // A game slow to answer gets one players question at a time, and its answer, come after another world is
    // shown, does not put its players on that world's map.
    [AvaloniaFact]
    public async Task PlayersAreAskedOnceAtATimeAndAnOldWorldsAnswerIsDropped()
    {
        using var slow = new FakeGame { HoldPlayers = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var other = new FakeGame { Players = "[{\"name\":\"Cyd\",\"x\":0,\"y\":30,\"z\":0,\"yaw\":0}]" };
        var w = await ConnectingTo(slow);
        var m = w.MapPage!;
        await TestWindows.Until(() => slow.PlayerReads == 1);
        // The timer's next ticks while the first question waits: no more questions.
        for (int i = 0; i < 3; i++)
        {
            await m.PollPlayers();
        }
        Assert.Equal(1, slow.PlayerReads);
        // Another world shown; then the slow game answers.
        using var session = await WorldSession.OpenLive(new LiveBridge(other.Url, other.Token), FakeGame.Label());
        m.Show(session);
        await TestWindows.Until(() => m.LiveText.Text == "Live world · 1 player(s) online: Cyd");
        slow.HoldPlayers.SetResult();
        await Task.Delay(300);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.Equal("Live world · 1 player(s) online: Cyd", m.LiveText.Text);
        Assert.Equal(["Cyd"], m.Map.Players.Select(p => p.Name));
        m.Stop();
    }

    // A world shown again (A, B, then A) before its game answered is not asked a second time; its one answer then
    // puts its players on the map.
    [AvaloniaFact]
    public async Task AWorldShownAgainBeforeItsGameAnsweredIsNotAskedTwice()
    {
        using var slow = new FakeGame { HoldPlayers = new(TaskCreationOptions.RunContinuationsAsynchronously), Players = "[{\"name\":\"Ash\",\"x\":0,\"y\":30,\"z\":0,\"yaw\":0}]" };
        using var other = new FakeGame();
        var w = await ConnectingTo(slow);
        var m = w.MapPage!;
        var first = w.World!;
        await TestWindows.Until(() => slow.PlayerReads == 1);
        using var session = await WorldSession.OpenLive(new LiveBridge(other.Url, other.Token), FakeGame.Label());
        m.Show(session);
        await TestWindows.Until(() => other.PlayerReads == 1);
        m.Show(first);
        await m.PollPlayers();
        Assert.Equal(1, slow.PlayerReads);
        slow.HoldPlayers.SetResult();
        await TestWindows.Until(() => m.LiveText.Text == "Live world · 1 player(s) online: Ash");
        Assert.Equal(["Ash"], m.Map.Players.Select(p => p.Name));
        m.Stop();
    }
}
