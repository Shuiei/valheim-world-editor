using System.Net;
using System.Net.Sockets;
using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The status of a game running the WorldEditorBridge plugin: the start page's state, and the bridges probed for one that answers.
/// </summary>
[Collection("DataDir")]
public sealed class HomeFolderStatusTests : HomeFolderTestBase
{
    [Fact]
    public void AStatusServerThatFailsToAnswerFailsItsDisposal()
    {
        // Not a status an HTTP answer can have: setting it throws.
        var broken = new HomeFolderStatusServer(42, "").Start();
        using var http = new System.Net.Http.HttpClient();
        _ = http.GetAsync($"http://127.0.0.1:{broken.Port}/status", TestContext.Current.CancellationToken);

        Assert.True(SpinWait.SpinUntil(() => broken.Broken, TimeSpan.FromSeconds(30)), "the request was never answered");
        var e = Assert.Throws<AggregateException>(broken.Dispose);

        Assert.IsType<ProtocolViolationException>(e.InnerException);
    }

    [Fact]
    public async Task TheStartPagesStateFollowsWhatIsInstalledAndRunning()
    {
        string bep = Local("profile", "BepInEx");
        var settings = new AppSettings { BepInExFolders = { Local("profile") } };
        async Task<string> State() => (string)(await LocalGame.Status(settings)).GetType().GetProperty("state")!.GetValue(await LocalGame.Status(settings))!;
        Assert.Equal("no-bepinex", await State());
        Directory.CreateDirectory(Path.Combine(bep, "core"));
        Assert.Equal("no-plugin", await State());
        Plugin(bep);
        Assert.Equal("plugin-not-started", await State());
        using var refusing = new HomeFolderStatusServer(401, "no").Start();
        Plugin(bep, refusing.Port, "t");
        Assert.Equal("installed", await State());
        using var running = new HomeFolderStatusServer(200, "{\"world\":\"Midgard\",\"players\":3}").Start();
        Plugin(bep, running.Port, "t");
        object status = await LocalGame.Status(settings);
        var t = status.GetType();
        Assert.Equal("running", t.GetProperty("state")!.GetValue(status));
        Assert.Equal("Midgard", t.GetProperty("world")!.GetValue(status));
        Assert.Equal(3, t.GetProperty("players")!.GetValue(status));
        Assert.Equal("\"profile\"", t.GetProperty("where")!.GetValue(status));
        // A status without the world and players: still running, with blanks.
        using var bare = new HomeFolderStatusServer(200, "{}").Start();
        Plugin(bep, bare.Port, "t");
        var r = await LocalGame.FindRunning(LocalGame.FindBridges(settings, out _, out _));
        Assert.Equal("", r!.World);
        Assert.Equal(0, r.Players);
    }

    [Fact]
    public async Task BridgesAreProbedTogetherAndTheFirstThatAnswersIsTaken()
    {
        // Three ports that take the connection but never answer: asked one after another they would cost 4.5 s.
        var silent = Enumerable.Range(0, 3).Select(_ => new TcpListener(IPAddress.Loopback, 0)).ToList();
        try
        {
            foreach (var l in silent)
            {
                l.Start();
            }
            using var first = new HomeFolderStatusServer(200, "{\"world\":\"First\"}").Start();
            using var second = new HomeFolderStatusServer(200, "{\"world\":\"Second\"}").Start();
            var bridges = silent.Select(l => new LocalGame.Bridge("cfg", "here", ((IPEndPoint)l.LocalEndpoint).Port, "t"))
                .Append(new LocalGame.Bridge("cfg", "here", first.Port, "t"))
                .Append(new LocalGame.Bridge("cfg", "here", second.Port, "t"));
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var r = await LocalGame.FindRunning(bridges);
            Assert.Equal("First", r!.World);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(4), $"{clock.Elapsed}");
        }
        finally
        {
            foreach (var l in silent)
            {
                l.Stop();
            }
        }
    }
}
