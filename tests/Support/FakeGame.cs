using System.Net;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Live mode without a game: a stand-in for the WorldEditorBridge plugin, answering like it does (the web tests'
/// FakeBridge, with the calls the native app makes too: the world snapshot, the players, the ground). It records what
/// the editor sends. The answers to each path are in FakeGame.Requests.cs, FakeGame.Zones.cs and FakeGame.Mods.cs.
/// </summary>
public sealed partial class FakeGame : IDisposable
{
    private readonly HttpListener _listener = new();
    /// <summary>Answering the requests: Dispose waits for it to end, and its errors fail the test.</summary>
    private readonly Task _serving;
    /// <summary>The requests answered on their own (Answer): Dispose waits for them, and their errors fail the test.</summary>
    private readonly List<Task> _answering = [];
    private readonly byte[] _snapshot;

    public int Port { get; }

    /// <summary>Whether the wait for requests or the answer to one ended with an error (Dispose then throws it).</summary>
    public bool Broken
    {
        get
        {
            lock (_answering)
            {
                return _serving.IsFaulted || _answering.Exists(a => a.IsFaulted);
            }
        }
    }
    public string Url => $"http://127.0.0.1:{Port}";
    public string Token { get; } = "test-token";
    public string Players { get; set; } = "[{\"name\":\"Ada\",\"x\":10,\"y\":30,\"z\":-20,\"yaw\":0},{\"name\":\"Bjorn\",\"x\":-40,\"y\":31,\"z\":5,\"yaw\":90}]";
    private int _snapshots;
    public int Snapshots => _snapshots;
    /// <summary>
    /// A label of its own for each game opened (what the window shows; the history kept on disk goes by it), so tests
    /// running together never share one.
    /// </summary>
    public static string Label() => "test " + Guid.NewGuid().ToString("N")[..8];
    public List<(int X, int Z)> Terrain { get; } = [];
    public List<(int Destroy, int Create)> ObjectCalls { get; } = [];
    public List<(int X, int Z, bool Keep, bool Ground)> Resets { get; } = [];
    private uint _next = 1000;
    // Answer this path with this status (an error from the game, or an older plugin without it).
    public Dictionary<string, int> Fail { get; } = [];

    /// <summary>The mods' plugins registered with the bridge (GET /mods); null: a plugin too old to have /mods (404).</summary>
    public List<string>? Mods { get; set; } = [];

    /// <summary>The mods' routes (POST /mods/id/name): the answer to a body, by mod id and route name.</summary>
    public Dictionary<(string Mod, string Name), Func<byte[], byte[]>> Routes { get; } = [];
    // Set: a players question waits for it before the game answers. PlayerReads counts the questions.
    public TaskCompletionSource? HoldPlayers { get; set; }
    private int _playerReads;
    public int PlayerReads => _playerReads;
    // Set: ground sent to the game waits for it before the game answers (TerrainArrived says it came).
    public TaskCompletionSource? HoldTerrain { get; set; }
    public TaskCompletionSource TerrainArrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    // What the game has in its zones, for /watch and /zone: each zone's number (it changes with the zone)
    // and its objects (ZDOID and bytes). Zones not set are not watched.
    public Dictionary<(int X, int Z), (ulong Digest, List<(long User, uint Id, byte[] Bytes)> Objects)> Zones { get; } = [];
    private int _zoneReads;
    public int ZoneReads => _zoneReads;
    // Objects the game no longer had when asked to destroy them.
    public int Missing { get; set; }

    /// <summary>
    /// The status code, content type and body of one answer.
    /// </summary>
    private sealed class Reply
    {
        public int Status { get; set; } = 200;
        public string Type { get; set; } = "application/json";
        public byte[] Body { get; set; } = [];
    }

    public FakeGame()
    {
        _snapshot = File.ReadAllBytes(Snapshot);
        Port = LoopbackPort.Free();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _serving = Task.Run(Serve);
    }

    // The fixture world as the plugin sends it.
    public static string Snapshot
    {
        get
        {
            string? root = AppContext.BaseDirectory;
            while (root != null && !File.Exists(Path.Combine(root, "tests", "fixtures", "CITest.snapshot")))
            {
                root = Path.GetDirectoryName(root);
            }
            Assert.NotNull(root);
            return Path.Combine(root, "tests", "fixtures", "CITest.snapshot");
        }
    }

    /// <summary>
    /// A local TCP port free right now.
    /// </summary>
    public static int FreePort() => LoopbackPort.Free();

    /// <summary>
    /// Stops answering, lets held requests go, and waits until the wait for requests and every request being answered
    /// have ended; an error in them fails the test.
    /// </summary>
    public void Dispose()
    {
        _listener.Close();
        HoldPlayers?.TrySetResult();
        HoldTerrain?.TrySetResult();
        Assert.True(_serving.Wait(TimeSpan.FromSeconds(30)), "the stand-in game did not stop answering");
        Task[] answers;
        lock (_answering)
        {
            answers = [.. _answering];
        }
        Assert.True(Task.WaitAll(answers, TimeSpan.FromSeconds(30)), "a request to the stand-in game was still being answered");
    }
}
