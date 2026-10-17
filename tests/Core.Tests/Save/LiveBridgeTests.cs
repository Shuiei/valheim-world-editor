using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The live bridge client (LiveBridge) against FakeBridge.
/// </summary>
[Collection("Timed answers")]
public class LiveBridgeTests
{
    // The plugin's HttpListener closes a connection left 15 s without a request; a request sent on it at that
    // moment is reset. So a connection is used again for the next request only while it has not sat idle.
    [Fact]
    public async Task AConnectionLeftIdleIsNotUsedAgain()
    {
        using var bridge = new FakeBridge();
        using var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token, idle: TimeSpan.FromMilliseconds(300));
        await live.Players();
        await live.Players();
        Assert.Single(bridge.Connections.Distinct());
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        await live.Players();
        Assert.Equal(3, bridge.Connections.Count);
        Assert.Equal(2, bridge.Connections.Distinct().Count());
        Assert.True(LiveBridge.ConnectionIdle < TimeSpan.FromSeconds(15), "under the listener's 15 s");
    }

    [Fact]
    public async Task CheckAcceptsAnAnsweringBridge()
    {
        using var bridge = new FakeBridge();
        Assert.Null(await LiveBridge.Check($"http://127.0.0.1:{bridge.Port}", bridge.Token, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task CheckExplainsAWrongToken()
    {
        using var bridge = new FakeBridge();
        string? why = await LiveBridge.Check($"http://127.0.0.1:{bridge.Port}", "wrong", TimeSpan.FromSeconds(5));
        Assert.Contains("refused the token", why);
    }

    [Fact]
    public async Task CheckExplainsAGameThatDoesNotHostTheWorld()
    {
        using var bridge = new FakeBridge { Status = 409 };
        Assert.Contains("does not host the world", await LiveBridge.Check($"http://127.0.0.1:{bridge.Port}", bridge.Token, TimeSpan.FromSeconds(5)));
    }

    [Theory]
    // A scheme of its own (the "http" typo'd), which SendAsync refused with an exception.
    [InlineData("httpsrv:5182", "x", "not a valid address")]
    [InlineData("ftp://127.0.0.1:5182", "x", "not a valid address")]
    // A line break in the token, which Headers.Add refused with an exception.
    [InlineData("http://127.0.0.1:5182", "to\nken", "token cannot be sent")]
    public async Task CheckExplainsAnAddressOrTokenItCannotSend(string url, string token, string why)
    {
        Assert.Contains(why, await LiveBridge.Check(url, token, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task CheckExplainsNothingListening()
    {
        int port = FakeBridge.FreePort();
        Assert.Contains("SSH tunnel", await LiveBridge.Check($"http://127.0.0.1:{port}", "x", TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task CheckGivesUpOnASilentServer()
    {
        // Accepts the connection, never answers.
        var silent = new TcpListener(IPAddress.Loopback, 0);
        silent.Start();
        int port = ((IPEndPoint)silent.LocalEndpoint).Port;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string? why = await LiveBridge.Check($"http://127.0.0.1:{port}", "x", TimeSpan.FromSeconds(2));
        silent.Stop();
        Assert.Contains("No answer", why);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(6), "gives up after the timeout");
    }

    [Fact]
    public async Task CheckRejectsAnInvalidAddress()
    {
        Assert.Contains("not a valid address", await LiveBridge.Check("not a url:::", "x", TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void SnapshotLoadsLikeTheSave()
    {
        var live = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        Assert.True(live.IsLive);
        Assert.Equal("CITest", live.Name);
        Assert.Equal(WorldSave.Load(Fixtures.World).ObjectCount, live.ObjectCount);
    }

    [Fact]
    public async Task ApplySendsOnlyTheDifferenceAndUndoWorksAfterApplying()
    {
        using var bridge = new FakeBridge();
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(world);
        var sync = new LiveSync();
        var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token);
        var (id, prefab, position, rotation, scale) = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));

        // Plant one, delete one: one call, with one creation and one destruction.
        edits.AddObjects([new NewObject(-1, Fixtures.Hash("Beech1"), position + new Vector3(3, 0, 3), Vector3.Zero, 0f)]);
        edits.SetDeleted([id], true);
        Assert.Equal((1, 1), sync.Pending(edits));
        await sync.Apply(world, edits, live);
        Assert.Equal((1, 1), bridge.ObjectCalls.Single());
        Assert.Equal((0, 0), sync.Pending(edits));

        // Nothing new: nothing is sent.
        Assert.Equal("", (await sync.Apply(world, edits, live)).Shown);
        Assert.Single(bridge.ObjectCalls);

        // Undo both: the planted one is destroyed in the game (by its live id), the deleted one comes back.
        edits.SetDeleted([-1], true);
        edits.SetDeleted([id], false);
        Assert.Equal((1, 1), sync.Pending(edits));
        await sync.Apply(world, edits, live);
        Assert.Equal((1, 1), bridge.ObjectCalls[1]);
        Assert.Contains((77L, 1000u), bridge.Destroyed);
        // With what it is and where: a mod that made it again under a new id has it there.
        Assert.Contains((Fixtures.Hash("Beech1"), position + new Vector3(3, 0, 3)), bridge.Places);
        Assert.Contains((prefab, position), bridge.Places);
        Assert.Equal((0, 0), sync.Pending(edits));
    }

    [Fact]
    public async Task ZoneResetsAreSentToTheGame()
    {
        using var bridge = new FakeBridge();
        var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token);
        string reply = await live.ResetZones([new ZoneReset(3, -2, true, false), new ZoneReset(-7, 9, false, true)]);
        Assert.Contains("\"zones\":2", reply);
        Assert.Equal(new[] { (3, -2, true, false), (-7, 9, false, true) }, bridge.Resets);
    }

    [Fact]
    public async Task AnErrorFromTheGameSaysWhatFailed()
    {
        using var bridge = new FakeBridge { Status = 500 };
        using var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token);
        var e = await Assert.ThrowsAsync<InvalidOperationException>(() => live.Watch([(1, 2)]));
        Assert.StartsWith("bridge watch: 500", e.Message);
        // Not found: the plugin is older than the call.
        bridge.Status = 404;
        e = await Assert.ThrowsAsync<InvalidOperationException>(() => live.ZoneObjects([(1, 2)]));
        Assert.Contains("too old for zone", e.Message);
    }

    [Fact]
    public async Task ObjectsAnotherModMadeAgainAreToldOf()
    {
        using var bridge = new FakeBridge { Refound = 1 };
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "test");
        var edits = new EditStore(world);
        var sync = new LiveSync();
        using var live = new LiveBridge($"http://127.0.0.1:{bridge.Port}", bridge.Token);
        edits.SetDeleted([world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1")).Id], true);
        string said = (await sync.Apply(world, edits, live)).Shown;
        Assert.Contains("1 made again by another mod", said);
    }

    // The mods' plugins registered with the bridge, and a call to one of their routes: its answer's bytes; no such
    // route or mod, and a plugin too old to have /mods (none registered, API 0), each told apart.
    [Fact]
    public async Task ModsAreListedAndTheirRoutesCalled()
    {
        using var game = new FakeGame();
        game.Mods!.Add("ExampleVehicles");
        game.Routes[("ExampleVehicles", "ping")] = body => [.. Encoding.UTF8.GetBytes("pong "), .. body];
        using var live = new LiveBridge(game.Url, game.Token);

        var (api, mods) = await live.Mods();
        Assert.Equal(1, api);
        Assert.Equal(["ExampleVehicles"], mods);
        Assert.Equal("pong hi", Encoding.UTF8.GetString(await live.Call("ExampleVehicles", "ping", Encoding.UTF8.GetBytes("hi"), TestContext.Current.CancellationToken)));
        var missing = await Assert.ThrowsAsync<InvalidOperationException>(() => live.Call("ExampleVehicles", "nothing", [], TestContext.Current.CancellationToken));
        Assert.Contains("mods/ExampleVehicles/nothing: 404 the mod has no route of that name", missing.Message);
        var unknown = await Assert.ThrowsAsync<InvalidOperationException>(() => live.Call("Other", "ping", [], TestContext.Current.CancellationToken));
        Assert.Contains("mods/Other/ping: 404 no mod of that id", unknown.Message);

        game.Mods = null;
        var (oldApi, oldMods) = await live.Mods();
        Assert.Equal(0, oldApi);
        Assert.Empty(oldMods);
    }
}
