using System.Net;
using System.Net.Sockets;
using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The live bridge client (LiveBridge) against FakeBridge.
/// </summary>
public class LiveBridgeTests
{
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
        Assert.Equal("", await sync.Apply(world, edits, live));
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
}
