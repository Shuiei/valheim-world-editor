using System.Net;
using System.Net.Sockets;
using System.Text;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Live mode without a game: a stand-in for the WorldEditorBridge plugin, answering like it does (the web tests'
/// FakeBridge, with the calls the native app makes too: the world snapshot, the players, the ground). It records what
/// the editor sends.
/// </summary>
public sealed class FakeGame : IDisposable
{
    private readonly HttpListener _listener = new();
    /// <summary>Answering the requests: Dispose waits for it to end, and its errors fail the test.</summary>
    private readonly Task _serving;
    private readonly byte[] _snapshot;

    public int Port { get; }
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

    public FakeGame()
    {
        _snapshot = File.ReadAllBytes(Snapshot);
        Port = FreePort();
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

    private async Task Serve()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync();
            }
            catch (Exception) when (!_listener.IsListening)
            {
                // Closed (Dispose): the wait for the next request ends with an error, and so does the serving.
                return;
            }
            // Each request on its own: a held one (HoldTerrain) or a broken connection never keeps the others
            // from being answered.
            _ = Task.Run(() => Answer(ctx));
        }
    }

    /// <summary>One request, answered as the plugin would.</summary>
    private async Task Answer(HttpListenerContext ctx)
    {
        byte[] body = [];
        string type = "application/json";
        int status = ctx.Request.Headers["X-Bridge-Token"] == Token ? 200 : 401;
        string path = ctx.Request.Url!.AbsolutePath;
        try
        {
            if (status != 200)
            {
                body = Encoding.UTF8.GetBytes("{\"error\":\"token\"}");
            }
            else if (Fail.TryGetValue(path, out int failed))
            {
                status = failed;
                body = Encoding.UTF8.GetBytes("broken");
            }
            else if (path == "/status")
            {
                body = Encoding.UTF8.GetBytes("{\"plugin\":\"0.3.0\",\"world\":\"CITest\",\"players\":2}");
            }
            else if (path == "/snapshot")
            {
                Interlocked.Increment(ref _snapshots);
                body = _snapshot;
                type = "application/octet-stream";
            }
            else if (path == "/players")
            {
                Interlocked.Increment(ref _playerReads);
                if (HoldPlayers is { } hold)
                {
                    await hold.Task;
                }
                body = Encoding.UTF8.GetBytes(Players);
            }
            else if (path == "/terrain")
            {
                using var r = new BinaryReader(ctx.Request.InputStream);
                int n = r.ReadInt32();
                lock (Terrain)
                {
                    for (int i = 0; i < n; i++)
                    {
                        int x = r.ReadInt32(), z = r.ReadInt32();
                        r.ReadBytes(r.ReadInt32());
                        Terrain.Add((x, z));
                    }
                }
                if (HoldTerrain is { } hold)
                {
                    TerrainArrived.TrySetResult();
                    await hold.Task;
                }
                body = Encoding.UTF8.GetBytes($"{{\"zones\":{n}}}");
            }
            else if (path == "/objects")
            {
                using var r = new BinaryReader(ctx.Request.InputStream);
                int destroy = r.ReadInt32();
                for (int i = 0; i < destroy; i++)
                {
                    r.ReadInt64();
                    r.ReadUInt32();
                }
                int create = r.ReadInt32();
                var ids = new List<string>();
                for (int i = 0; i < create; i++)
                {
                    r.ReadBytes(r.ReadInt32());
                    ids.Add($"\"77:{Interlocked.Increment(ref _next) - 1}\"");
                }
                lock (ObjectCalls)
                {
                    ObjectCalls.Add((destroy, create));
                }
                body = Encoding.UTF8.GetBytes($"{{\"destroyed\":{destroy - Missing},\"missing\":{Missing},\"created\":[{string.Join(",", ids)}]}}");
            }
            else if (path is "/watch" or "/zone")
            {
                using var r = new BinaryReader(ctx.Request.InputStream);
                int n = r.ReadInt32();
                var asked = new List<(int, int)>();
                for (int i = 0; i < n; i++)
                {
                    asked.Add((r.ReadInt32(), r.ReadInt32()));
                }
                lock (Zones)
                {
                    if (path == "/watch")
                    {
                        // Zones not set are left out (as the plugin leaves out zones outside the world):
                        // the editor then has nothing to read again there.
                        body = Encoding.UTF8.GetBytes("{\"zones\":[" + string.Join(",", asked.Where(Zones.ContainsKey).Select(z => $"[{z.Item1},{z.Item2},\"{Zones[z].Digest}\"]")) + "]}");
                    }
                    else
                    {
                        Interlocked.Increment(ref _zoneReads);
                        var all = asked.SelectMany(z => Zones.TryGetValue(z, out var e) ? e.Objects : []).ToList();
                        using var raw = new MemoryStream();
                        using (var w = new BinaryWriter(raw, Encoding.UTF8, leaveOpen: true))
                        {
                            w.Write(all.Count);
                            foreach (var (user, id, bytes) in all)
                            {
                                w.Write(user);
                                w.Write(id);
                                w.Write(bytes.Length);
                                w.Write(bytes);
                            }
                        }
                        using var packed = new MemoryStream();
                        using (var gz = new System.IO.Compression.GZipStream(packed, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
                        {
                            gz.Write(raw.ToArray());
                        }
                        body = packed.ToArray();
                        type = "application/octet-stream";
                    }
                }
            }
            else if (path == "/mods")
            {
                if (Mods == null)
                {
                    status = 404;
                    type = "text/plain";
                    body = Encoding.UTF8.GetBytes("unknown path");
                }
                else
                {
                    body = Encoding.UTF8.GetBytes("{\"api\":1,\"mods\":[" + string.Join(",", Mods.Select(m => $"\"{m}\"")) + "]}");
                }
            }
            else if (path.StartsWith("/mods/", StringComparison.Ordinal))
            {
                string[] parts = path.Split('/');
                using var ms = new MemoryStream();
                await ctx.Request.InputStream.CopyToAsync(ms);
                type = "text/plain";
                if (Mods == null || parts.Length != 4 || !Mods.Contains(parts[2]))
                {
                    status = 404;
                    body = Encoding.UTF8.GetBytes("no mod of that id is registered with the bridge (GET /mods lists them)");
                }
                else if (!Routes.TryGetValue((parts[2], parts[3]), out var route))
                {
                    status = 404;
                    body = Encoding.UTF8.GetBytes("the mod has no route of that name");
                }
                else
                {
                    type = "application/octet-stream";
                    body = route(ms.ToArray());
                }
            }
            else if (path == "/zones/reset")
            {
                using var r = new BinaryReader(ctx.Request.InputStream);
                int n = r.ReadInt32();
                lock (Resets)
                {
                    for (int i = 0; i < n; i++)
                    {
                        Resets.Add((r.ReadInt32(), r.ReadInt32(), r.ReadBoolean(), r.ReadBoolean()));
                    }
                }
                body = Encoding.UTF8.GetBytes($"{{\"zones\":{n},\"destroyed\":0,\"locations\":0}}");
            }
            else
            {
                status = 404;
            }
        }
        catch (Exception ex)
        {
            status = 500;
            body = Encoding.UTF8.GetBytes(ex.Message);
        }
        try
        {
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = type;
            await ctx.Response.OutputStream.WriteAsync(body);
            ctx.Response.Close();
        }
        // The editor gave up on the answer (its connection closed), or the game was disposed meanwhile.
        catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException)
        {
        }
    }

    /// <summary>
    /// A local TCP port free right now.
    /// </summary>
    public static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        int p = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return p;
    }

    /// <summary>Stops answering and waits until the wait for requests has ended; an error in it fails the test.</summary>
    public void Dispose()
    {
        _listener.Close();
        Assert.True(_serving.Wait(TimeSpan.FromSeconds(30)), "the stand-in game did not stop answering");
    }
}
