using System.Text.Json;
namespace TerrainEditor.Save;

/// <summary>
/// Client for the WorldEditorBridge BepInEx plugin running in the game (usually on the dedicated server, reached
/// through an SSH tunnel such as ssh -L 5182:127.0.0.1:5182 user@server).
/// </summary>
/// <param name="url">The bridge's base address, for example http://127.0.0.1:5182.</param>
/// <param name="token">The plugin's token, sent with every request as the X-Bridge-Token header.</param>
/// <param name="idle">
/// How long a connection may go unused before the next request opens a new one instead (null: <see cref="ConnectionIdle"/>).
/// </param>
public sealed class LiveBridge(string url, string token, TimeSpan? idle = null) : IDisposable
{
    /// <summary>
    /// How long a connection to the bridge may go unused before the next request opens a new one. The plugin's web
    /// server (Mono's HttpListener, like .NET's own) closes a connection it has answered on after 15 seconds without
    /// another request, and a request sent on it at that moment is reset ("Connection reset by peer"); well under
    /// that, the editor never sends on a connection the game is closing.
    /// </summary>
    public static readonly TimeSpan ConnectionIdle = TimeSpan.FromSeconds(10);

    /// <summary>
    /// HTTP client rooted at the bridge address; three minutes timeout, since a whole-world snapshot is slow.
    /// </summary>
    private readonly HttpClient _http = new(new SocketsHttpHandler { PooledConnectionIdleTimeout = idle ?? ConnectionIdle })
    {
        BaseAddress = new Uri(url.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromMinutes(3),
    };

    /// <summary>The bridge address as given.</summary>
    public string Url { get; } = url;

    /// <inheritdoc/>
    public void Dispose() => _http.Dispose();

    /// <summary>A GET request with the token.</summary>
    /// <exception cref="InvalidOperationException">The bridge answered with an error status.</exception>
    private async Task<HttpResponseMessage> Get(string path)
    {
        using HttpRequestMessage req = new(HttpMethod.Get, path);
        req.Headers.Add("X-Bridge-Token", token);
        HttpResponseMessage res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            string body = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"bridge {path}: {(int)res.StatusCode} {body}");
        }
        return res;
    }

    /// <summary>
    /// The whole persistent world as the running game has it (world info, zone data and every persistent object),
    /// gzip-compressed; <c>WorldSave.LoadLive</c> reads it.
    /// </summary>
    public async Task<byte[]> Snapshot() => await (await Get("snapshot")).Content.ReadAsByteArrayAsync();

    /// <summary>The connected players and their positions, as the plugin's JSON array.</summary>
    public async Task<string> Players() => await (await Get("players")).Content.ReadAsStringAsync();

    /// <summary>
    /// Every prefab the running game knows, mods' too, as the plugin's JSON (ModCatalogue reads it); a plugin older
    /// than 1.26.0 answers 404.
    /// </summary>
    public async Task<string> Catalogue() => await (await Get("catalogue")).Content.ReadAsStringAsync();

    /// <summary>
    /// What the game draws of these prefabs (mod prefabs: their meshes', materials' names and places), as the plugin's
    /// JSON (ModCatalogue.TakeModels reads it); a plugin older than 1.27.0 answers 404.
    /// </summary>
    public async Task<string> Models(IEnumerable<string> prefabs)
    {
        using var res = await Post("models", System.Text.Encoding.UTF8.GetBytes(string.Join('\n', prefabs)));
        return await res.Content.ReadAsStringAsync();
    }

    /// <summary>Plugin and world info, as the plugin's JSON.</summary>
    public async Task<string> Status() => await (await Get("status")).Content.ReadAsStringAsync();

    /// <summary>
    /// A quick reachability check before opening a live world: null when the bridge answers, else a plain explanation
    /// of what is wrong.
    /// </summary>
    /// <param name="url">The address to try, with its scheme, for example http://127.0.0.1:5182.</param>
    /// <param name="token">The token to send.</param>
    /// <param name="timeout">How long to wait for an answer.</param>
    public static async Task<string?> Check(string url, string token, TimeSpan timeout)
    {
        Uri uri;
        try
        {
            uri = new Uri(url.TrimEnd('/') + "/status");
        }
        catch (UriFormatException)
        {
            return "That is not a valid address. Use host:port, for example 127.0.0.1:5182.";
        }
        using HttpClient http = new() { Timeout = timeout };
        using HttpRequestMessage req = new(HttpMethod.Get, uri);
        req.Headers.Add("X-Bridge-Token", token);
        string tunnel = $"The bridge only listens on the server itself. Open an SSH tunnel on this computer (ssh -N -L {uri.Port}:127.0.0.1:{uri.Port} user@your-server) and connect to 127.0.0.1:{uri.Port}.";
        bool local = uri.IsLoopback;
        try
        {
            using HttpResponseMessage res = await http.SendAsync(req);
            return (int)res.StatusCode switch
            {
                200 => null,
                401 or 403 => "The server answered but refused the token. Copy the Token from BepInEx/config/Tie.WorldEditorBridge.cfg on the server.",
                409 => "The plugin answered, but the game it runs in does not host the world (it joined another server). Connect to the server that hosts it.",
                _ => $"The server answered with an error ({(int)res.StatusCode}). Is it the WorldEditorBridge plugin?",
            };
        }
        catch (TaskCanceledException)
        {
            return local ? $"No answer from {uri.Host}:{uri.Port} within {timeout.TotalSeconds:0} s. Is the SSH tunnel open, and is the server running?" : $"No answer from {uri.Host}:{uri.Port}. {tunnel}";
        }
        catch (HttpRequestException ex)
        {
            return local
                ? $"Nothing answers on {uri.Host}:{uri.Port} ({ex.Message}). Open the SSH tunnel first (ssh -N -L {uri.Port}:127.0.0.1:{uri.Port} user@your-server), and check that the server runs with BepInEx and WorldEditorBridge."
                : $"Could not reach {uri.Host}:{uri.Port} ({ex.Message}). {tunnel}";
        }
    }

    /// <summary>
    /// New terrain data for zones (TerrainComp.Save format), as a ZPackage: count, then x, z, byte array.
    /// </summary>
    /// <returns>The plugin's answer text.</returns>
    /// <exception cref="BridgeAnswerException">The bridge answered with an error status.</exception>
    public async Task<string> ApplyTerrain(IReadOnlyList<(int X, int Z, byte[] Data)> zones)
    {
        using MemoryStream ms = new();
        using (BinaryWriter w = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(zones.Count);
            foreach (var (x, z, data) in zones)
            {
                w.Write(x);
                w.Write(z);
                w.Write(data.Length);
                w.Write(data);
            }
        }
        using HttpRequestMessage req = new(HttpMethod.Post, "terrain") { Content = new ByteArrayContent(ms.ToArray()) };
        req.Headers.Add("X-Bridge-Token", token);
        HttpResponseMessage res = await _http.SendAsync(req);
        string body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            throw new BridgeAnswerException($"bridge terrain: {(int)res.StatusCode} {body}", (int)res.StatusCode);
        }
        return body;
    }

    /// <summary>
    /// Whether the plugin moves objects in place (<see cref="ApplyObjects"/>'s moves; 1.40.0 on, which lists "move"
    /// among the features in its status). Asked once; a check that fails says no, and is asked again next time.
    /// </summary>
    public Task<bool> CanMove() => Has("move");

    /// <summary>
    /// Whether the plugin does an <see cref="ApplyObjects"/> call sent again under the same call id only once, answering
    /// the second time with what it did the first (1.45.3 on, which lists "once" among the features in its status). Asked
    /// once; a check that fails says no, and is asked again next time.
    /// </summary>
    public Task<bool> CanCallOnce() => Has("once");

    /// <summary>
    /// This run of the plugin, asked now: a new id each time the game starts (1.45.3 on; "" before). A call whose answer
    /// was lost is sent again under its id only to the run it went to: another run does not know what that one did.
    /// </summary>
    public async Task<string> Run()
    {
        using var doc = JsonDocument.Parse(await Status());
        return doc.RootElement.TryGetProperty("run", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString()! : "";
    }

    /// <summary>Whether the plugin's status lists this feature; asked once, a check that fails says no.</summary>
    /// <param name="feature">The feature's name in the status.</param>
    private async Task<bool> Has(string feature)
    {
        if (_features == null)
        {
            try
            {
                using var doc = JsonDocument.Parse(await Status());
                _features = doc.RootElement.TryGetProperty("features", out var f) && f.ValueKind == JsonValueKind.Array
                    ? [.. f.EnumerateArray().Select(e => e.GetString() ?? "")]
                    : [];
            }
            catch (Exception e) when (e is HttpRequestException or InvalidOperationException or JsonException or TaskCanceledException)
            {
                return false;
            }
        }
        return _features.Contains(feature);
    }

    /// <summary>The features the plugin's status lists, once asked.</summary>
    private HashSet<string>? _features;

    /// <summary>
    /// Objects to destroy (live ZDOIDs) and to create (save-format bytes); returns the plugin's JSON. After them, each
    /// destroyed object's prefab and position: another mod may have made the object again under a new ZDOID
    /// (ServersideQoL does, for build pieces, plants, fires and more), and the plugin finds it by those. Plugins from
    /// before 1.15.4 stop reading before them. Then the moves, only for a plugin that can (<see cref="CanMove"/>):
    /// one that cannot would leave them out.
    /// </summary>
    /// <param name="destroy">The objects to destroy.</param>
    /// <param name="create">The objects to make, in the save format.</param>
    /// <param name="moves">The objects to move (none: the call older plugins take).</param>
    /// <param name="callId">
    /// The call's id, for a plugin that can (<see cref="CanCallOnce"/>): the same call sent again under it after its
    /// answer was lost is not done twice. Null: none sent.
    /// </param>
    public async Task<string> ApplyObjects(IReadOnlyList<LiveTarget> destroy, IReadOnlyList<byte[]> create, IReadOnlyList<LiveMove>? moves = null, string? callId = null)
    {
        using MemoryStream ms = new();
        using (BinaryWriter w = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(destroy.Count);
            foreach (LiveTarget t in destroy)
            {
                w.Write(t.User);
                w.Write(t.Id);
            }
            w.Write(create.Count);
            foreach (byte[] o in create)
            {
                w.Write(o.Length);
                w.Write(o);
            }
            foreach (LiveTarget t in destroy)
            {
                w.Write(t.Prefab);
                w.Write(t.Position.X);
                w.Write(t.Position.Y);
                w.Write(t.Position.Z);
            }
            if (moves is { Count: > 0 })
            {
                w.Write(moves.Count);
                foreach (LiveMove m in moves)
                {
                    w.Write(m.From.User);
                    w.Write(m.From.Id);
                    w.Write(m.From.Prefab);
                    Write(w, m.From.Position);
                    Write(w, m.Position);
                    Write(w, m.Rotation);
                    w.Write(m.Fallback.Length);
                    w.Write(m.Fallback);
                }
            }
        }
        static void Write(BinaryWriter w, System.Numerics.Vector3 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
            w.Write(v.Z);
        }
        using HttpRequestMessage req = new(HttpMethod.Post, "objects") { Content = new ByteArrayContent(ms.ToArray()) };
        req.Headers.Add("X-Bridge-Token", token);
        if (callId != null)
        {
            req.Headers.Add("X-Call-Id", callId);
        }
        HttpResponseMessage res = await _http.SendAsync(req);
        string body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"bridge objects: {(int)res.StatusCode} {body}");
        }
        return body;
    }

    /// <summary>
    /// Zones to hand back to the world generator in the running game: int count, then per zone int x, int z, bool keep
    /// buildings, bool reset ground. Returns the plugin's JSON.
    /// </summary>
    public async Task<string> ResetZones(IReadOnlyList<Editing.ZoneReset> zones)
    {
        using MemoryStream ms = new();
        using (BinaryWriter w = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(zones.Count);
            foreach (var z in zones)
            {
                w.Write(z.X);
                w.Write(z.Z);
                w.Write(z.KeepBuildings);
                w.Write(z.Ground);
            }
        }
        using HttpRequestMessage req = new(HttpMethod.Post, "zones/reset") { Content = new ByteArrayContent(ms.ToArray()) };
        req.Headers.Add("X-Bridge-Token", token);
        HttpResponseMessage res = await _http.SendAsync(req);
        string body = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(res.StatusCode == System.Net.HttpStatusCode.NotFound
                ? "the WorldEditorBridge plugin on the server is too old to reset zones live: update it"
                : $"bridge zones/reset: {(int)res.StatusCode} {body}");
        }
        return body;
    }

    /// <summary>The zones asked for as an int count, then int x, int z each.</summary>
    private static byte[] ZoneList(IReadOnlyCollection<(int X, int Z)> zones)
    {
        using MemoryStream ms = new();
        using (BinaryWriter w = new(ms, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            w.Write(zones.Count);
            foreach (var (x, z) in zones)
            {
                w.Write(x);
                w.Write(z);
            }
        }
        return ms.ToArray();
    }

    /// <summary>
    /// The mods' plugins registered with the bridge (WorldEditorBridge.Api: routes to call, listeners told of changes),
    /// and the bridge's API version; none and 0 from a plugin older than 1.35.0.
    /// </summary>
    public async Task<(int Api, List<string> Mods)> Mods()
    {
        using HttpRequestMessage req = new(HttpMethod.Get, "mods");
        req.Headers.Add("X-Bridge-Token", token);
        using HttpResponseMessage res = await _http.SendAsync(req);
        if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return (0, []);
        }
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"bridge mods: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync()}");
        }
        using var doc = JsonDocument.Parse(await res.Content.ReadAsByteArrayAsync());
        int api = doc.RootElement.TryGetProperty("api", out var a) ? a.GetInt32() : 0;
        var mods = doc.RootElement.TryGetProperty("mods", out var m) ? m.EnumerateArray().Select(e => e.GetString()).OfType<string>().ToList() : [];
        return (api, mods);
    }

    /// <summary>
    /// Calls a mod's route in the game (POST /mods/{modId}/{name}, answered by the mod's plugin) and gives back its
    /// answer's bytes. 404 means no such mod or route (the plugin's answer says which), not an old plugin: the caller
    /// asked <see cref="Mods"/> first.
    /// </summary>
    /// <exception cref="InvalidOperationException">The bridge answered with an error status (its message given).</exception>
    public async Task<byte[]> Call(string modId, string name, byte[] body, CancellationToken cancel = default)
    {
        using HttpRequestMessage req = new(HttpMethod.Post, $"mods/{Uri.EscapeDataString(modId)}/{Uri.EscapeDataString(name)}") { Content = new ByteArrayContent(body) };
        req.Headers.Add("X-Bridge-Token", token);
        using HttpResponseMessage res = await _http.SendAsync(req, cancel);
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"bridge mods/{modId}/{name}: {(int)res.StatusCode} {await res.Content.ReadAsStringAsync(cancel)}");
        }
        return await res.Content.ReadAsByteArrayAsync(cancel);
    }

    /// <summary>A POST request with the token and a binary body.</summary>
    /// <exception cref="InvalidOperationException">
    /// The bridge answered with an error status (404 means the plugin is too old for that endpoint).
    /// </exception>
    private async Task<HttpResponseMessage> Post(string path, byte[] body)
    {
        using HttpRequestMessage req = new(HttpMethod.Post, path) { Content = new ByteArrayContent(body) };
        req.Headers.Add("X-Bridge-Token", token);
        HttpResponseMessage res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            string text = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException(res.StatusCode == System.Net.HttpStatusCode.NotFound
                ? $"the WorldEditorBridge plugin on the server is too old for {path}: update it"
                : $"bridge {path}: {(int)res.StatusCode} {text}");
        }
        return res;
    }

    /// <summary>
    /// One number per zone that changes when an object there changes (a plugin from this version on).
    /// </summary>
    public async Task<Dictionary<(int X, int Z), ulong>> Watch(IReadOnlyCollection<(int X, int Z)> zones)
    {
        using var res = await Post("watch", ZoneList(zones));
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var digests = new Dictionary<(int, int), ulong>();
        foreach (var z in doc.RootElement.GetProperty("zones").EnumerateArray())
        {
            digests[(z[0].GetInt32(), z[1].GetInt32())] = ulong.Parse(z[2].GetString()!, System.Globalization.CultureInfo.InvariantCulture);
        }
        return digests;
    }

    /// <summary>The saved objects of the zones as the game has them now: ZDOID and save-format bytes.</summary>
    /// <exception cref="InvalidDataException">The answer is damaged or unpacks to more than a world snapshot can.</exception>
    public async Task<List<(long User, uint Id, byte[] Bytes)>> ZoneObjects(IReadOnlyCollection<(int X, int Z)> zones)
    {
        using var res = await Post("zone", ZoneList(zones));
        // Some zones' objects: never more than the whole world's snapshot unpacks to.
        using var raw = new MemoryStream(CheckedReads.Gunzip(await res.Content.ReadAsByteArrayAsync(), WorldSave.MaxSnapshot));
        using var r = new BinaryReader(raw);
        // Each: ZDOID (8 + 4), then the bytes' length (4).
        int count = r.CheckCount(r.ReadInt32(), 16);
        var list = new List<(long, uint, byte[])>(count);
        for (int i = 0; i < count; i++)
        {
            long user = r.ReadInt64();
            uint id = r.ReadUInt32();
            list.Add((user, id, r.ReadExactBytes(r.ReadInt32())));
        }
        return list;
    }

    /// <summary>Takes a snapshot and loads it as a world, its source named after the bridge address.</summary>
    public async Task<WorldSave> LoadWorld() => WorldSave.LoadLive(await Snapshot(), "live: " + Url);
}
