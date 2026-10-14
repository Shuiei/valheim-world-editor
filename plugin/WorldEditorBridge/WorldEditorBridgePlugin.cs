using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using WorldEditorBridge.Api;

namespace WorldEditorBridge;

/// <summary>
/// Live bridge for the Valheim world editor. Runs on the dedicated server (or a host) and answers on a
/// local HTTP port, protected by a token:
/// <code>
///   GET /snapshot   the whole persistent world: world info, ZoneSystem data (generated zones, locations)
///                   and every persistent object in the save file's chunk format, gzip-compressed
///   GET /players    connected players and their positions (JSON)
///   GET /status     plugin and world info (JSON)
///   POST /terrain   new terrain data for zones: int count, then per zone int x, int z, int length + TCData
///                   (TerrainComp.Save format); every player's game reloads those zones' ground
///   POST /objects   int n + n x (long user, uint id) objects to destroy, then int m + m x (int length +
///                   object in save format) objects to create; answers with the new objects' ids
///   POST /zones/reset  int n + n x (int x, int z, bool keep buildings, bool reset ground): the zones'
///                   objects are removed and the zones are generated again (at once where players are)
///   POST /watch     int n + n x (int x, int z): one change number per zone (JSON)
///   POST /zone      the same zone list: those zones' saved objects, gzip-compressed
///   GET /catalogue  every prefab the game knows, mods' too (JSON, see WorldEditorBridgePlugin.Catalogue.cs)
///   POST /models    prefab names, one per line: what the game draws of each (JSON, see
///                   WorldEditorBridgePlugin.Models.cs)
///   GET /mods       the mods' plugins registered with the bridge (WorldEditorBridge.Api; JSON, see
///                   WorldEditorBridgePlugin.Mods.cs)
///   POST /mods/&lt;id&gt;/&lt;name&gt;  a call to one of a mod's own routes, answered by its plugin
/// </code>
/// Game objects are only read on Unity's main thread; the HTTP thread waits for the answer.
/// </summary>
[BepInPlugin(PluginGuid, "WorldEditorBridge", Version)]
[BepInDependency(JotunnGuid, BepInDependency.DependencyFlags.SoftDependency)]
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Unity owns its lifetime; OnDestroy stops the listener.")]
public sealed partial class WorldEditorBridgePlugin : BaseUnityPlugin
{
    /// <summary>BepInEx plugin id; it also names the settings file in BepInEx/config.</summary>
    public const string PluginGuid = "Tie.WorldEditorBridge";

    /// <summary>The id before 0.41.0, which named the settings file (Awake moves that file to the new name).</summary>
    private const string OldGuid = "local.worldeditorbridge";

    /// <summary>The plugin's version: the editor's, from the build. /status reports it.</summary>
    public const string Version = BuildInfo.Version;

    /// <summary>Format version written after "VWEB" at the start of a snapshot.</summary>
    private const int SnapshotVersion = 1;

    /// <summary>Address the listener binds to (Http.BindAddress).</summary>
    private ConfigEntry<string> _bind;

    /// <summary>TCP port of the bridge (Http.Port).</summary>
    private ConfigEntry<int> _port;

    /// <summary>
    /// Secret every request must carry (X-Bridge-Token header or ?token=); made on first start when empty.
    /// </summary>
    private ConfigEntry<string> _token;

    /// <summary>The HTTP listener on the bind address and port (also localhost when bound to 127.0.0.1).</summary>
    private HttpListener _listener;

    /// <summary>The background thread accepting requests (<c>Serve</c>).</summary>
    private Thread _thread;

    /// <summary>Cleared by OnDestroy so that <c>Serve</c> stops.</summary>
    private volatile bool _running;

    /// <summary>Work the HTTP threads hand to Unity's main thread; <c>Update</c> runs it.</summary>
    private readonly ConcurrentQueue<Job> _jobs = new();

    /// <summary>ZDOMan's private dictionary of every object the game knows, by id.</summary>
    private static readonly FieldInfo ObjectsById = typeof(ZDOMan).GetField("m_objectsByID", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>
    /// The objects each apply destroyed, looked at again a second later (<c>Update</c>, <c>CheckDestroyed</c>): whether
    /// the game still has any, or another of the same prefab stands at its place; with the objects the apply created,
    /// which are not such. Main thread only.
    /// </summary>
    private readonly Queue<(float Due, List<AppliedObject> Gone, List<AppliedObject> Made)> _destroyChecks = new();

    /// <summary>ZoneSystem's private set of the zones already generated.</summary>
    private static readonly FieldInfo GeneratedZones = typeof(ZoneSystem).GetField("m_generatedZones", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>ZDO keeps its rotation as Euler angles; reading them avoids a quaternion round trip.</summary>
    private static readonly FieldInfo ZdoRotation = typeof(ZDO).GetField("m_rotation", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>ZoneSystem's private set of the world's global keys, written into the snapshot.</summary>
    private static readonly FieldInfo GlobalKeys = typeof(ZoneSystem).GetField("m_globalKeys", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>One piece of work for the main thread and its outcome, which the HTTP thread waits for.</summary>
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Both threads hold Done; it is left to the GC, as before.")]
    private sealed class Job
    {
        /// <summary>Runs on the main thread; returns the response body.</summary>
        public Func<byte[]> Work;

        /// <summary>What <c>Work</c> returned.</summary>
        public byte[] Result;

        /// <summary>What <c>Work</c> threw, thrown again on the HTTP thread.</summary>
        public Exception Error;

        /// <summary>Set once <c>Work</c> has run (or failed).</summary>
        public readonly ManualResetEventSlim Done = new(false);

        /// <summary>0 waiting, 1 taken by the main thread, 2 given up by the caller (never runs).</summary>
        public int State;
    }

    /// <summary>
    /// Moves the settings made under the old id, binds the settings (making a token when there is none) and starts
    /// listening on its own thread. Unity calls it by name.
    /// </summary>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Unity calls it by name.")]
    private void Awake()
    {
        // Settings made under the old id (port, token) move to the new file, so editors that saved the
        // token keep connecting.
        string old = Path.Combine(Paths.ConfigPath, OldGuid + ".cfg");
        if (File.Exists(old) && !File.Exists(Config.ConfigFilePath))
        {
            try
            {
                File.Move(old, Config.ConfigFilePath);
                Config.Reload();
                Logger.LogInfo($"Settings moved from {OldGuid}.cfg to {PluginGuid}.cfg");
            }
            catch (IOException ex)
            {
                Logger.LogWarning($"Could not move {old}: {ex.Message}");
            }
        }
        _bind = Config.Bind("Http", "BindAddress", "127.0.0.1", "Address to listen on. Keep 127.0.0.1 and reach it through an SSH tunnel (ssh -L 5182:127.0.0.1:5182 user@server).");
        _port = Config.Bind("Http", "Port", 5182, "TCP port of the bridge.");
        _token = Config.Bind("Http", "Token", "", "Secret the editor must send (X-Bridge-Token header or ?token=). Generated on first start when empty.");
        if (string.IsNullOrWhiteSpace(_token.Value))
        {
            byte[] b = new byte[18];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
            {
                rng.GetBytes(b);
            }
            _token.Value = Convert.ToBase64String(b).Replace('+', 'A').Replace('/', 'B').TrimEnd('=');
            Config.Save();
        }
        EnableModQuery();
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{_bind.Value}:{_port.Value}/");
            if (_bind.Value == "127.0.0.1")
            {
                _listener.Prefixes.Add($"http://localhost:{_port.Value}/");
            }
            _listener.Start();
            _running = true;
            _thread = new Thread(Serve) { IsBackground = true, Name = "WorldEditorBridge" };
            _thread.Start();
            Logger.LogInfo($"WorldEditorBridge {Version} listening on http://{_bind.Value}:{_port.Value}/ (token in BepInEx/config/{PluginGuid}.cfg)");
        }
        catch (Exception ex)
        {
            Logger.LogError($"WorldEditorBridge could not listen on {_bind.Value}:{_port.Value}: {ex.Message}");
        }
    }

    /// <summary>Stops the listener, which ends <c>Serve</c>. Unity calls it by name.</summary>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Unity calls it by name.")]
    private void OnDestroy()
    {
        _running = false;
        try
        {
            _listener?.Stop();
        }
        catch
        {
        }
    }

    /// <summary>
    /// Runs at most four queued jobs per frame on Unity's main thread, skipping those their caller gave up on.
    /// Unity calls it by name.
    /// </summary>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Unity calls it by name.")]
    private void Update()
    {
        if (_destroyChecks.Count > 0 && Time.realtimeSinceStartup >= _destroyChecks.Peek().Due)
        {
            var (_, gone, made) = _destroyChecks.Dequeue();
            CheckDestroyed(gone, made);
        }
        // At most a few jobs per frame; each is quick except the snapshot.
        for (int i = 0; i < 4 && _jobs.TryDequeue(out Job job); i++)
        {
            if (Interlocked.CompareExchange(ref job.State, 1, 0) != 0)
            {
                continue;
            }
            try
            {
                job.Result = job.Work();
            }
            catch (Exception ex)
            {
                job.Error = ex;
            }
            job.Done.Set();
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> on Unity's main thread and waits for it. Given up on after the timeout, a job
    /// never runs later (the editor, told it failed, sends it again: objects would be made twice); one the main
    /// thread already took is waited for, as it is quick once started.
    /// </summary>
    /// <param name="work">Reads or changes the game; returns the response body.</param>
    /// <param name="timeoutMs">Milliseconds to wait before giving up on a job the main thread has not taken yet.</param>
    /// <returns>What <paramref name="work"/> returned; what it threw is thrown here, and a
    /// <see cref="TimeoutException"/> when the game did not take the job in time.</returns>
    private byte[] OnMainThread(Func<byte[]> work, int timeoutMs = 30000)
    {
        Job job = new() { Work = work };
        _jobs.Enqueue(job);
        // Given up on, a job never runs later (the editor, told it failed, sends it again: objects would be
        // made twice). One the main thread already took is waited for: it is quick once started.
        if (!job.Done.Wait(timeoutMs))
        {
            if (Interlocked.CompareExchange(ref job.State, 2, 0) == 0)
            {
                throw new TimeoutException("the game did not answer in time");
            }
            job.Done.Wait();
        }
        if (job.Error != null)
        {
            throw job.Error;
        }
        return job.Result;
    }

    /// <summary>
    /// The listener thread's loop: each request is answered on the thread pool, until the plugin stops.
    /// </summary>
    private void Serve()
    {
        while (_running)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = _listener.GetContext();
            }
            catch
            {
                if (!_running)
                {
                    return;
                }
                continue;
            }
            ThreadPool.QueueUserWorkItem(_ => Handle(ctx));
        }
    }

    /// <summary>
    /// Answers one request by its path, after checking the token and that this game hosts a loaded world (a client
    /// connected to someone else's server only knows the objects around it). A failure answers 500 with its message.
    /// </summary>
    private void Handle(HttpListenerContext ctx)
    {
        HttpListenerResponse res = ctx.Response;
        try
        {
            string token = ctx.Request.Headers["X-Bridge-Token"] ?? ctx.Request.QueryString["token"];
            if (token != _token.Value)
            {
                Reply(res, 401, "text/plain", Encoding.UTF8.GetBytes("bad or missing token"));
                return;
            }
            if (ZNet.instance == null || ZDOMan.instance == null || ZoneSystem.instance == null)
            {
                Reply(res, 503, "text/plain", Encoding.UTF8.GetBytes("no world loaded yet"));
                return;
            }
            // A client connected to someone else's server only knows the objects around it.
            if (!ZNet.instance.IsServer())
            {
                Reply(res, 409, "text/plain", Encoding.UTF8.GetBytes("this game is connected to a server: install WorldEditorBridge on that server, or host the world yourself"));
                return;
            }
            string path = ctx.Request.Url.AbsolutePath;
            if (path.StartsWith("/mods/", StringComparison.Ordinal))
            {
                if (ctx.Request.HttpMethod != "POST")
                {
                    Reply(res, 405, "text/plain", Encoding.UTF8.GetBytes("POST only"));
                    return;
                }
                // A mod's body is bounded, and refused before it is held whole.
                byte[] body = ctx.Request.ContentLength64 > MaxModBody ? null : ReadAtMost(ctx.Request.InputStream, MaxModBody);
                if (body == null)
                {
                    Reply(res, 413, "text/plain", Encoding.UTF8.GetBytes("the body is too large"));
                    return;
                }
                CallMod(res, path, body);
                return;
            }
            switch (path)
            {
                case "/mods":
                    Reply(res, 200, "application/json", Encoding.UTF8.GetBytes(Mods()));
                    break;
                case "/status":
                    Reply(res, 200, "application/json", OnMainThread(Status));
                    break;
                case "/players":
                    Reply(res, 200, "application/json", OnMainThread(Players));
                    break;
                case "/catalogue":
                    Reply(res, 200, "application/json", OnMainThread(Catalogue, 120000));
                    break;
                case "/terrain":
                {
                    if (ctx.Request.HttpMethod != "POST")
                    {
                        Reply(res, 405, "text/plain", Encoding.UTF8.GetBytes("POST only"));
                        break;
                    }
                    byte[] body = ReadAll(ctx.Request.InputStream);
                    Reply(res, 200, "application/json", OnMainThread(() => Applied(ApplyTerrain(body))));
                    break;
                }
                case "/objects":
                {
                    if (ctx.Request.HttpMethod != "POST")
                    {
                        Reply(res, 405, "text/plain", Encoding.UTF8.GetBytes("POST only"));
                        break;
                    }
                    byte[] body = ReadAll(ctx.Request.InputStream);
                    Reply(res, 200, "application/json", OnMainThread(() => Applied(ApplyObjects(body))));
                    break;
                }
                case "/models":
                {
                    if (ctx.Request.HttpMethod != "POST")
                    {
                        Reply(res, 405, "text/plain", Encoding.UTF8.GetBytes("POST only"));
                        break;
                    }
                    byte[] body = ReadAll(ctx.Request.InputStream);
                    Reply(res, 200, "application/json", OnMainThread(() => Models(body), 120000));
                    break;
                }
                case "/zones/reset":
                {
                    if (ctx.Request.HttpMethod != "POST")
                    {
                        Reply(res, 405, "text/plain", Encoding.UTF8.GetBytes("POST only"));
                        break;
                    }
                    byte[] body = ReadAll(ctx.Request.InputStream);
                    Reply(res, 200, "application/json", OnMainThread(() => Applied(ResetZones(body))));
                    break;
                }
                case "/watch":
                {
                    if (ctx.Request.HttpMethod != "POST")
                    {
                        Reply(res, 405, "text/plain", Encoding.UTF8.GetBytes("POST only"));
                        break;
                    }
                    byte[] body = ReadAll(ctx.Request.InputStream);
                    Reply(res, 200, "application/json", OnMainThread(() => Watch(body)));
                    break;
                }
                case "/zone":
                {
                    if (ctx.Request.HttpMethod != "POST")
                    {
                        Reply(res, 405, "text/plain", Encoding.UTF8.GetBytes("POST only"));
                        break;
                    }
                    byte[] body = ReadAll(ctx.Request.InputStream);
                    byte[] raw = OnMainThread(() => ZoneObjects(body));
                    using MemoryStream packed = new();
                    using (GZipStream gz = new(packed, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
                    {
                        gz.Write(raw, 0, raw.Length);
                    }
                    Reply(res, 200, "application/octet-stream", packed.ToArray());
                    break;
                }
                case "/snapshot":
                {
                    byte[] raw = OnMainThread(Snapshot, 120000);
                    using MemoryStream packed = new();
                    using (GZipStream gz = new(packed, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
                    {
                        gz.Write(raw, 0, raw.Length);
                    }
                    Reply(res, 200, "application/octet-stream", packed.ToArray());
                    break;
                }
                default:
                    Reply(res, 404, "text/plain", Encoding.UTF8.GetBytes("unknown path"));
                    break;
            }
        }
        catch (Exception ex)
        {
            // The path comes from the request: keep line breaks out so it cannot forge log lines.
            string path = ctx.Request.Url.AbsolutePath.Replace("\r", "").Replace("\n", "");
            Logger.LogWarning($"WorldEditorBridge request {path} failed: {ex}");
            try
            {
                Reply(res, 500, "text/plain", Encoding.UTF8.GetBytes(ex.Message));
            }
            catch
            {
            }
        }
    }

    /// <summary>A request body, read to its end.</summary>
    private static byte[] ReadAll(Stream s)
    {
        using MemoryStream ms = new();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>A stream's bytes, up to a limit; null once more than that came (reading stops there).</summary>
    private static byte[] ReadAtMost(Stream s, int limit)
    {
        using MemoryStream ms = new();
        byte[] buffer = new byte[64 << 10];
        int n;
        while ((n = s.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (ms.Length + n > limit)
            {
                return null;
            }
            ms.Write(buffer, 0, n);
        }
        return ms.ToArray();
    }

    /// <summary>Prefab hash of _TerrainCompiler, the per-zone object holding the zone's ground edits (TCData).</summary>
    private static readonly int TerrainCompilerPrefab = "_TerrainCompiler".GetStableHashCode();

    /// <summary>
    /// POST /terrain: sets the terrain data of each zone's terrain compiler object (creating it when the zone has
    /// none). The game syncs the object to everyone, and TerrainComp reloads when its data changes.
    /// </summary>
    /// <returns>JSON: how many zones were applied and how many compilers were made; and what the mods are told.</returns>
    private (byte[] Json, AppliedChanges Changes) ApplyTerrain(byte[] body)
    {
        ZPackage pkg = new(body);
        int count = pkg.ReadInt();
        var dict = (Dictionary<ZDOID, ZDO>)ObjectsById.GetValue(ZDOMan.instance);
        Dictionary<Vector2s, ZDO> compilers = [];
        foreach (ZDO zdo in dict.Values)
        {
            if (zdo.GetPrefab() == TerrainCompilerPrefab)
            {
                Vector2s zone = ZoneSystem.GetZone(zdo.GetPosition());
                // With duplicates (the game cleans them up itself), keep the one with data.
                if (!compilers.TryGetValue(zone, out ZDO other) || other.GetByteArray(ZDOVars.s_TCData) == null)
                {
                    compilers[zone] = zdo;
                }
            }
        }
        int applied = 0, created = 0;
        List<Zone> zones = [];
        for (int i = 0; i < count; i++)
        {
            int zx = pkg.ReadInt(), zz = pkg.ReadInt();
            byte[] data = pkg.ReadByteArray();
            zones.Add(new Zone(zx, zz));
            Vector2s key = new((short)zx, (short)zz);
            if (!compilers.TryGetValue(key, out ZDO tc))
            {
                tc = CreateTerrainCompiler(key);
                compilers[key] = tc;
                created++;
            }
            tc.Set(ZDOVars.s_TCData, data);
            applied++;
        }
        Logger.LogInfo($"WorldEditorBridge: applied terrain to {applied} zone(s) ({created} new)");
        return (Encoding.UTF8.GetBytes($"{{\"applied\":{applied},\"created\":{created}}}"), new AppliedChanges([], [], zones, []));
    }

    /// <summary>
    /// POST /objects: destroys objects (as their owner, so every peer drops them) and creates new ones from objects
    /// in the save file format (flags, position, prefab, rotation, data), like ZDO.Load but for a new object.
    /// Editors from 1.15.4 add each destroyed object's prefab and position: another mod may have made it again under
    /// a new ZDOID (ServersideQoL does, for build pieces, plants, fires and more), and then the object of that prefab
    /// at that place goes instead. Without this, undoing a placed piece found it "already gone" while every player
    /// kept seeing the new one.
    /// </summary>
    /// <returns>JSON: destroyed (refound included), refound, missing (already gone) and the created objects' ids as
    /// "user:id" strings, in the order they were sent; and what the mods are told.</returns>
    private (byte[] Json, AppliedChanges Changes) ApplyObjects(byte[] body)
    {
        ZPackage pkg = new(body);
        long session = ZDOMan.GetSessionID();
        var all = (Dictionary<ZDOID, ZDO>)ObjectsById.GetValue(ZDOMan.instance);
        int before = all.Count;
        int destroyCount = pkg.ReadInt();
        var destroyIds = new ZDOID[destroyCount];
        for (int i = 0; i < destroyCount; i++)
        {
            destroyIds[i] = new ZDOID(pkg.ReadLong(), pkg.ReadUInt());
        }
        int createCount = pkg.ReadInt();
        // Every object to make is read first: a malformed one fails the call before anything changed,
        // so the editor, which records nothing then, can send it again without doubles.
        var creates = new Func<ZDO>[createCount];
        for (int i = 0; i < createCount; i++)
        {
            creates[i] = ReadSaveFormat(new ZPackage(pkg.ReadByteArray()));
        }
        (int Prefab, Vector3 Position)[] where = null;
        if (pkg.GetPos() < pkg.Size())
        {
            where = new (int, Vector3)[destroyCount];
            for (int i = 0; i < destroyCount; i++)
            {
                where[i] = (pkg.ReadInt(), pkg.ReadVector3());
            }
        }
        int destroyed = 0, refound = 0;
        List<int> lost = [];
        List<AppliedObject> gone = [];
        for (int i = 0; i < destroyCount; i++)
        {
            ZDO zdo = ZDOMan.instance.GetZDO(destroyIds[i]);
            if (zdo == null)
            {
                lost.Add(i);
                continue;
            }
            gone.Add(Applied(zdo));
            zdo.SetOwner(session);
            ZDOMan.instance.DestroyZDO(zdo);
            destroyed++;
        }
        if (lost.Count > 0 && where != null)
        {
            refound = DestroyAtPlace([.. lost.Select(i => where[i])], session, gone);
        }
        int missing = lost.Count - refound;
        StringBuilder ids = new();
        List<AppliedObject> made = [];
        for (int i = 0; i < createCount; i++)
        {
            ZDO zdo = creates[i]();
            made.Add(Applied(zdo));
            if (i > 0)
            {
                ids.Append(',');
            }
            ids.Append('"').Append(zdo.m_uid.UserID.ToString(CultureInfo.InvariantCulture)).Append(':').Append(zdo.m_uid.ID.ToString(CultureInfo.InvariantCulture)).Append('"');
        }
        // The game drops a destroyed object on its next update, so the count after is the one before plus what was
        // created; the check a second later says whether the destroyed ones went.
        Logger.LogInfo($"WorldEditorBridge: destroyed {destroyed + refound} object(s) ({refound} found again at their place under a new id, {missing} already gone), created {createCount}; {before} -> {all.Count} object(s) known, {Peers()} peer(s)");
        if (gone.Count > 0)
        {
            _destroyChecks.Enqueue((Time.realtimeSinceStartup + 1f, gone, made));
        }
        return (Encoding.UTF8.GetBytes($"{{\"destroyed\":{destroyed + refound},\"refound\":{refound},\"missing\":{missing},\"created\":[{ids}]}}"),
            new AppliedChanges(made, gone, [], []));
    }

    /// <summary>How many peers (players' games) are connected to this game.</summary>
    private static int Peers() => ZNet.instance?.GetPeers().Count ?? 0;

    /// <summary>
    /// Logs, a second after an apply destroyed objects, how many of them the game still has and how many other objects
    /// of the same prefab stand within 10 cm of their places (made again by something on the server, or standing
    /// there all along; the objects the same apply created are not counted), with the object and peer counts: what a
    /// "the pieces stayed for the players" report needs. One pass over the game's objects, each looked up by the metre
    /// cell of its place, so a large apply costs no more than a small one per object.
    /// </summary>
    /// <param name="gone">The objects the apply destroyed.</param>
    /// <param name="made">The objects the same apply created.</param>
    private void CheckDestroyed(List<AppliedObject> gone, List<AppliedObject> made)
    {
        if (ZDOMan.instance == null)
        {
            return;
        }
        var dict = (Dictionary<ZDOID, ZDO>)ObjectsById.GetValue(ZDOMan.instance);
        HashSet<ZDOID> ids = [];
        foreach (AppliedObject o in made)
        {
            ids.Add(new ZDOID(o.User, o.Id));
        }
        // The destroyed places by prefab and metre cell: an object is checked against those of its own cell and the
        // eight around it only.
        Dictionary<(int Prefab, int X, int Z), List<Vector3>> cells = [];
        HashSet<int> prefabs = [];
        int still = 0;
        foreach (AppliedObject o in gone)
        {
            ZDOID id = new(o.User, o.Id);
            ids.Add(id);
            if (dict.ContainsKey(id))
            {
                still++;
            }
            Vector3 p = new(o.X, o.Y, o.Z);
            var key = (o.Prefab, Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
            if (!cells.TryGetValue(key, out var list))
            {
                cells[key] = list = [];
            }
            list.Add(p);
            prefabs.Add(o.Prefab);
        }
        bool Near(int prefab, Vector3 p)
        {
            int cx = Mathf.FloorToInt(p.x), cz = Mathf.FloorToInt(p.z);
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (cells.TryGetValue((prefab, cx + dx, cz + dz), out var list))
                    {
                        int at = list.FindIndex(w => (w - p).sqrMagnitude < 0.01f);
                        if (at >= 0)
                        {
                            list.RemoveAt(at);
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        int nearby = 0;
        foreach (ZDO zdo in dict.Values)
        {
            if (!ids.Contains(zdo.m_uid) && prefabs.Contains(zdo.GetPrefab()) && Near(zdo.GetPrefab(), zdo.GetPosition()))
            {
                nearby++;
            }
        }
        Logger.LogInfo($"WorldEditorBridge: 1 s after the apply: {still} of {gone.Count} destroyed object(s) still exist, {nearby} other object(s) of their prefab within 10 cm of their place (made again, or there all along); {dict.Count} object(s), {Peers()} peer(s)");
    }

    /// <summary>
    /// Destroys the objects of these prefabs at these places (within 10 cm: a re-made object keeps its position),
    /// each at most once.
    /// </summary>
    /// <param name="wanted">Prefab hash and position of each object to find.</param>
    /// <param name="session">This game's session id, made the objects' owner so the destroy reaches every peer.</param>
    /// <param name="gone">Where the objects destroyed are added, for the mods' listeners.</param>
    /// <returns>How many were found and destroyed.</returns>
    private static int DestroyAtPlace(List<(int Prefab, Vector3 Position)> wanted, long session, List<AppliedObject> gone)
    {
        Dictionary<int, List<Vector3>> byPrefab = [];
        foreach (var (prefab, pos) in wanted)
        {
            if (!byPrefab.TryGetValue(prefab, out var list))
            {
                byPrefab[prefab] = list = [];
            }
            list.Add(pos);
        }
        var dict = (Dictionary<ZDOID, ZDO>)ObjectsById.GetValue(ZDOMan.instance);
        int found = 0;
        foreach (ZDO zdo in new List<ZDO>(dict.Values))
        {
            if (!byPrefab.TryGetValue(zdo.GetPrefab(), out var places) || places.Count == 0)
            {
                continue;
            }
            Vector3 p = zdo.GetPosition();
            int at = places.FindIndex(w => (w - p).sqrMagnitude < 0.01f);
            if (at < 0)
            {
                continue;
            }
            places.RemoveAt(at);
            gone.Add(Applied(zdo));
            zdo.SetOwner(session);
            ZDOMan.instance.DestroyZDO(zdo);
            found++;
        }
        return found;
    }

    /// <summary>Prefab hash of players' tombstones, which a zone reset always keeps.</summary>
    private static readonly int TombstonePrefab = "Player_tombstone".GetStableHashCode();

    /// <summary>
    /// POST /zones/reset, like the editor's offline reset: every saved object of the zones goes (player-built pieces
    /// stay when asked, the ground edits only when asked, players' tombstones always stay), the zones are no longer
    /// marked as generated and their locations as not placed, so the game builds them again.
    /// </summary>
    /// <returns>JSON: zones reset, objects removed and locations to place again.</returns>
    private (byte[] Json, AppliedChanges Changes) ResetZones(byte[] body)
    {
        ZPackage pkg = new(body);
        int count = pkg.ReadInt();
        Dictionary<Vector2s, (bool Keep, bool Ground)> zones = [];
        for (int i = 0; i < count; i++)
        {
            Vector2s key = new((short)pkg.ReadInt(), (short)pkg.ReadInt());
            zones[key] = (pkg.ReadBool(), pkg.ReadBool());
        }
        var dict = (Dictionary<ZDOID, ZDO>)ObjectsById.GetValue(ZDOMan.instance);
        long session = ZDOMan.GetSessionID();
        int destroyed = 0;
        foreach (ZDO zdo in new List<ZDO>(dict.Values))
        {
            if (!zdo.Persistent || !zones.TryGetValue(ZoneSystem.GetZone(zdo.GetPosition()), out var r))
            {
                continue;
            }
            int prefab = zdo.GetPrefab();
            bool terrain = prefab == TerrainCompilerPrefab;
            if (prefab == TombstonePrefab || (terrain && !r.Ground) || (!terrain && r.Keep && zdo.GetLong(ZDOVars.s_creator) != 0))
            {
                continue;
            }
            zdo.SetOwner(session);
            ZDOMan.instance.DestroyZDO(zdo);
            destroyed++;
        }
        ZoneSystem zs = ZoneSystem.instance;
        var generated = (HashSet<Vector2s>)GeneratedZones.GetValue(zs);
        int locations = 0;
        foreach (Vector2s zone in zones.Keys)
        {
            generated.Remove(zone);
            if (zs.m_locationInstances.TryGetValue(zone, out ZoneSystem.LocationInstance li) && li.m_placed)
            {
                li.m_placed = false;
                zs.m_locationInstances[zone] = li;
                locations++;
            }
        }
        Logger.LogInfo($"WorldEditorBridge: reset {zones.Count} zone(s): {destroyed} object(s) removed, {locations} location(s) to place again");
        return (Encoding.UTF8.GetBytes($"{{\"zones\":{zones.Count},\"destroyed\":{destroyed},\"locations\":{locations}}}"),
            new AppliedChanges([], [], [], [.. zones.Keys.Select(z => new Zone(z.x, z.y))]));
    }

    /// <summary>
    /// An object as the save writes it (ZDO.Save), read whole into what makes it: nothing is made in the game until
    /// that is called.
    /// </summary>
    /// <returns>Creates the object (main thread only), relinking it when it had a link, and returns it.</returns>
    private static Func<ZDO> ReadSaveFormat(ZPackage p)
    {
        int flags = p.ReadUShort();
        Vector3 pos;
        if ((flags & 0x2000) != 0)
        {
            Vector2s v = p.ReadVector2s();
            pos = new Vector3(v.x, 0f, v.y);
        }
        else
        {
            pos = p.ReadVector3();
        }
        int prefab = p.ReadInt();
        Vector3 euler = (flags & 0x1000) != 0 ? p.ReadSmallRotation() : Vector3.zero;
        List<Action<ZDO>> values = [];
        var linkType = ZDOExtraData.ConnectionType.None;
        int linkHash = 0;
        if ((flags & 0xFF) != 0)
        {
            if ((flags & 0x1) != 0)
            {
                linkType = (ZDOExtraData.ConnectionType)p.ReadByte();
                linkHash = p.ReadInt();
            }
            void Each(int flag, Func<int, Action<ZDO>> read)
            {
                if ((flags & flag) == 0)
                {
                    return;
                }
                int n = p.ReadNumItems();
                for (int k = 0; k < n; k++)
                {
                    values.Add(read(p.ReadInt()));
                }
            }
            Each(0x2, key => { float v = p.ReadSingle(); return z => z.Set(key, v); });
            Each(0x4, key => { Vector3 v = p.ReadVector3(); return z => z.Set(key, v); });
            Each(0x8, key => { Quaternion v = p.ReadQuaternion(); return z => z.Set(key, v); });
            Each(0x10, key => { int v = p.ReadInt(); return z => z.Set(key, v); });
            Each(0x20, key => { long v = p.ReadLong(); return z => z.Set(key, v); });
            Each(0x40, key => { string v = p.ReadString(); return z => z.Set(key, v); });
            Each(0x80, key => { byte[] v = p.ReadByteArray(); return z => z.Set(key, v); });
        }
        return () =>
        {
            ZDO zdo = ZDOMan.instance.CreateNewZDO(pos, prefab);
            zdo.Persistent = (flags & 0x100) != 0;
            zdo.Distant = (flags & 0x200) != 0;
            zdo.Type = (ZDO.ObjectType)((flags >> 10) & 3);
            zdo.SetPrefab(prefab);
            zdo.SetRotation(Quaternion.Euler(euler));
            foreach (Action<ZDO> set in values)
            {
                set(zdo);
            }
            if (linkType != ZDOExtraData.ConnectionType.None)
            {
                Relink(zdo, linkType, linkHash);
            }
            return zdo;
        };
    }

    /// <summary>Like ZNetView.Awake for the _TerrainCompiler prefab, at the zone centre.</summary>
    private static ZDO CreateTerrainCompiler(Vector2s zone)
    {
        GameObject prefab = ZNetScene.instance.GetPrefab(TerrainCompilerPrefab);
#pragma warning disable IDE0031 // Unity's == null also catches destroyed objects; ?. does not
        ZNetView view = prefab != null ? prefab.GetComponent<ZNetView>() : null;
#pragma warning restore IDE0031
        ZDO zdo = ZDOMan.instance.CreateNewZDO(ZoneSystem.GetZonePos(zone), TerrainCompilerPrefab);
        zdo.Persistent = view == null || view.m_persistent;
        zdo.Type = view != null ? view.m_type : ZDO.ObjectType.Terrain;
        zdo.Distant = view != null && view.m_distant;
        zdo.SetPrefab(TerrainCompilerPrefab);
        return zdo;
    }

    /// <summary>Writes a whole response and closes it.</summary>
    private static void Reply(HttpListenerResponse res, int code, string type, byte[] body)
    {
        res.StatusCode = code;
        res.ContentType = type;
        res.ContentLength64 = body.Length;
        res.OutputStream.Write(body, 0, body.Length);
        res.OutputStream.Close();
    }

    /// <summary>A JSON string literal: backslashes, quotes and control characters escaped.</summary>
    private static string Json(string s)
    {
        StringBuilder sb = new("\"");
        foreach (char c in s)
        {
            _ = c switch
            {
                '\\' => sb.Append("\\\\"),
                '"' => sb.Append("\\\""),
                < ' ' => sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)),
                _ => sb.Append(c),
            };
        }
        return sb.Append('"').ToString();
    }

    /// <summary>A float for JSON: round-trip form, invariant culture.</summary>
    private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>
    /// GET /status: plugin version, world and seed names, object and player counts and the network time, as JSON.
    /// </summary>
    private byte[] Status()
    {
        World w = ZNet.World;
        var dict = (Dictionary<ZDOID, ZDO>)ObjectsById.GetValue(ZDOMan.instance);
        string json = $"{{\"plugin\":{Json(Version)},\"world\":{Json(w?.m_name ?? "")},\"seedName\":{Json(w?.m_seedName ?? "")},\"objects\":{dict.Count},\"players\":{ZNet.instance.GetPlayerList().Count},\"netTime\":{ZNet.instance.GetTimeSeconds().ToString("R", CultureInfo.InvariantCulture)}}}";
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// GET /players: each connected player's name, position and yaw (degrees), as a JSON array. The position is the
    /// character object's when the server has it, else the one the player reported.
    /// </summary>
    private byte[] Players()
    {
        StringBuilder sb = new("[");
        bool first = true;
        foreach (ZNet.PlayerInfo p in ZNet.instance.GetPlayerList())
        {
            ZDO zdo = p.m_characterID.IsNone() ? null : ZDOMan.instance.GetZDO(p.m_characterID);
            Vector3 pos = zdo != null ? zdo.GetPosition() : p.m_position;
            float yaw = zdo != null ? zdo.GetRotation().eulerAngles.y : 0f;
            if (!first)
            {
                sb.Append(',');
            }
            first = false;
            sb.Append($"{{\"name\":{Json(p.m_name ?? "")},\"x\":{F(pos.x)},\"y\":{F(pos.y)},\"z\":{F(pos.z)},\"yaw\":{F(yaw)}}}");
        }
        return Encoding.UTF8.GetBytes(sb.Append(']').ToString());
    }

    /// <summary>
    /// GET /snapshot. Layout (all little endian, strings as .NET BinaryWriter strings):
    /// <code>
    ///   "VWEB" int version, string world, string seedName, int seed, int worldGenVersion, double netTime
    ///   int length + ZoneSystem package (as ZoneSystem.Save writes it, uncompressed)
    ///   short 41, int count, count objects (ZDO.Save chunk format), then count x (long user, uint id)
    /// </code>
    /// Only persistent objects are written; the HTTP thread gzips it.
    /// </summary>
    private byte[] Snapshot()
    {
        World w = ZNet.World;
        ZPackage pkg = new();
        pkg.Write((byte)'V'); pkg.Write((byte)'W'); pkg.Write((byte)'E'); pkg.Write((byte)'B');
        pkg.Write(SnapshotVersion);
        pkg.Write(w.m_name);
        pkg.Write(w.m_seedName);
        pkg.Write(w.m_seed);
        pkg.Write(w.m_worldGenVersion);
        pkg.Write(ZNet.instance.GetTimeSeconds());
        byte[] zones = ZoneSystemData();
        pkg.Write(zones.Length);
        foreach (byte b in zones)
        {
            pkg.Write(b);
        }
        var dict = (Dictionary<ZDOID, ZDO>)ObjectsById.GetValue(ZDOMan.instance);
        List<ZDO> zdos = new(dict.Count);
        foreach (ZDO zdo in dict.Values)
        {
            if (zdo.Persistent)
            {
                zdos.Add(zdo);
            }
        }
        var links = SnapshotLinks();
        pkg.Write((short)41);
        pkg.Write(zdos.Count);
        foreach (ZDO zdo in zdos)
        {
            WriteZdo(pkg, zdo, links);
        }
        foreach (ZDO zdo in zdos)
        {
            pkg.Write(zdo.m_uid.UserID);
            pkg.Write(zdo.m_uid.ID);
        }
        return pkg.GetArray();
    }

    /// <summary>Same layout as ZoneSystem.Save: generated zones, location version, global keys, locations.</summary>
    private static byte[] ZoneSystemData()
    {
        ZoneSystem zs = ZoneSystem.instance;
        ZPackage p = new();
        var generated = (HashSet<Vector2s>)GeneratedZones.GetValue(zs);
        p.Write(generated.Count);
        foreach (Vector2s z in generated)
        {
            p.Write(z);
        }
        p.Write(zs.m_locationVersion);
        var keys = (HashSet<string>)GlobalKeys.GetValue(zs);
        p.Write(keys.Count);
        foreach (string k in keys)
        {
            p.Write(k);
        }
        p.Write(zs.LocationsGenerated);
        p.Write(zs.m_locationInstances.Count);
        foreach (ZoneSystem.LocationInstance li in zs.m_locationInstances.Values)
        {
            p.Write(li.m_location?.m_prefabName.GetStableHashCode() ?? 0);
            p.Write(li.m_position.x);
            p.Write(li.m_position.y);
            p.Write(li.m_position.z);
            p.Write(li.m_placed);
        }
        return p.GetArray();
    }

    // ---- What changes in the zones the editor shows (it asks every few seconds: only those zones are
    // read, from the game's own per-zone lists, never the whole world).

    /// <summary>ZDOMan's private per-zone object lists, indexed by ZoneSystem.SectorToIndex.</summary>
    private static readonly FieldInfo ObjectsBySector = typeof(ZDOMan).GetField("m_objectsBySector", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Portals are kept apart from the zone lists, in their own (by zone too).</summary>
    private static readonly FieldInfo PortalObjects = typeof(ZDOMan).GetField("m_portalObjects", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Zones asked for: int count, then int x, int z each (outside the world: left out).</summary>
    private static List<Vector2s> ReadZones(byte[] body)
    {
        ZPackage pkg = new(body);
        int count = pkg.ReadInt();
        List<Vector2s> zones = new(count);
        for (int i = 0; i < count; i++)
        {
            int x = pkg.ReadInt(), z = pkg.ReadInt();
            if (x >= -256 && x < 256 && z >= -256 && z < 256)
            {
                zones.Add(new Vector2s(x, z));
            }
        }
        return zones;
    }

    /// <summary>
    /// The saved objects of a zone (persistent ZDOs), as the game keeps them by zone: its list, and its portals
    /// (kept apart).
    /// </summary>
    private static IEnumerable<ZDO> ZoneZdos(Vector2s zone)
    {
        var bySector = (List<ZDO>[])ObjectsBySector.GetValue(ZDOMan.instance);
        var portals = (Dictionary<ZoneSystem.SectorIndex, List<ZDO>>)PortalObjects.GetValue(ZDOMan.instance);
        ZoneSystem.SectorIndex index = ZoneSystem.SectorToIndex(zone);
        IEnumerable<ZDO> list = index.Sector < bySector.Length && bySector[index.Sector] is { } inZone ? inZone : Enumerable.Empty<ZDO>();
        if (portals != null && portals.TryGetValue(index, out List<ZDO> zonePortals))
        {
            list = list.Concat(zonePortals);
        }
        foreach (ZDO zdo in list)
        {
            if (zdo.Persistent)
            {
                yield return zdo;
            }
        }
    }

    /// <summary>Cache of <c>Moves</c>, by prefab hash.</summary>
    private static readonly Dictionary<int, bool> Movers = [];

    /// <summary>
    /// Kinds whose own changes <c>Watch</c> leaves out. Kinds that move by themselves (creatures, ships, carts, items:
    /// they sync their transform): their moves are not changes to the world worth reading again. And kinds that
    /// rewrite their data on their own every few seconds while a player is near (fires and torches their fuel time,
    /// smelters, cooking stations, beehives, fermenters, sap collectors): that is not a change worth reading their
    /// zone again for (they would be read at every look).
    /// </summary>
    /// <returns>True for those kinds; false for others and for prefabs the game does not know.</returns>
    private static bool Moves(int prefab)
    {
        if (!Movers.TryGetValue(prefab, out bool moves))
        {
#pragma warning disable IDE0031 // Unity's == null also catches destroyed objects; ?. does not
            GameObject go = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
#pragma warning restore IDE0031
            Movers[prefab] = moves = go != null && (go.GetComponent<ZSyncTransform>() != null || go.GetComponent<Fireplace>() != null
                || go.GetComponent<Smelter>() != null || go.GetComponent<CookingStation>() != null || go.GetComponent<Beehive>() != null
                || go.GetComponent<Fermenter>() != null || go.GetComponent<SapCollector>() != null);
        }
        return moves;
    }

    /// <summary>
    /// POST /watch: one number per zone that changes when an object there is made, removed or changed (its data or
    /// place; a mover or a ticking kind only when made or removed): the editor reads a zone again when it changes,
    /// and then sees every object as it is.
    /// </summary>
    /// <returns>JSON <c>{"zones":[[x,z,"number"],...]}</c>, the number as a string (it is a 64-bit unsigned).</returns>
    private static byte[] Watch(byte[] body)
    {
        StringBuilder sb = new("{\"zones\":[");
        bool first = true;
        foreach (Vector2s zone in ReadZones(body))
        {
            ulong digest = 0;
            foreach (ZDO zdo in ZoneZdos(zone))
            {
                int prefab = zdo.GetPrefab();
                ulong h = unchecked((ulong)zdo.m_uid.UserID * 0x9E3779B97F4A7C15UL ^ zdo.m_uid.ID * 0xC2B2AE3D27D4EB4FUL ^ (uint)prefab * 0x165667B19E3779F9UL);
                h ^= Moves(prefab) ? 0 : unchecked(zdo.DataRevision * 0x27D4EB2F165667C5UL + 1);
                // A sum: the same whatever order the game keeps the zone's list in.
                digest = unchecked(digest + (h ^ (h >> 29)) * 0xBF58476D1CE4E5B9UL);
            }
            sb.Append(first ? "" : ",").Append('[').Append(zone.x).Append(',').Append(zone.y).Append(",\"").Append(digest.ToString(CultureInfo.InvariantCulture)).Append("\"]");
            first = false;
        }
        return Encoding.UTF8.GetBytes(sb.Append("]}").ToString());
    }

    /// <summary>
    /// POST /zone: the saved objects of the zones asked for, in the snapshot's format: int count, then for each its
    /// ZDOID (long, uint) and its bytes (int length, bytes). The HTTP thread gzips it.
    /// </summary>
    private static byte[] ZoneObjects(byte[] body)
    {
        var links = SnapshotLinks();
        List<(ZDOID Id, byte[] Bytes)> found = [];
        foreach (Vector2s zone in ReadZones(body))
        {
            foreach (ZDO zdo in ZoneZdos(zone))
            {
                ZPackage one = new();
                WriteZdo(one, zdo, links);
                found.Add((zdo.m_uid, one.GetArray()));
            }
        }
        ZPackage pkg = new();
        pkg.Write(found.Count);
        foreach (var (id, bytes) in found)
        {
            pkg.Write(id.UserID);
            pkg.Write(id.ID);
            pkg.Write(bytes);
        }
        return pkg.GetArray();
    }

    /// <summary>
    /// Links between objects (a spawner and what it spawned, two portals...), as the save writes them: a hash on the
    /// object holding the link and the same hash, marked Target, on its target. The hash is kept with both objects'
    /// ids (main thread only), so an object made again from the snapshot (an undone delete) gets its link back, and
    /// links to it follow it to its new id.
    /// </summary>
    private static readonly Dictionary<int, (ZDOExtraData.ConnectionType Type, ZDOID Source, ZDOID Target)> Links = [];

    /// <summary>The hash of the link <paramref name="source"/> holds, from its id; odd, so never 0.</summary>
    private static int LinkHash(ZDOID source) => unchecked((int)(source.UserID * 486187739L) ^ (int)source.ID * 16777619) | 1;

    /// <summary>
    /// The link each object carries in the save (type and hash), recording every link in <c>Links</c> on the way.
    /// An object both holding a link and the target of one carries the one it holds. Main thread only.
    /// </summary>
    private static Dictionary<ZDOID, (ZDOExtraData.ConnectionType Type, int Hash)> SnapshotLinks()
    {
        var of = new Dictionary<ZDOID, (ZDOExtraData.ConnectionType, int)>();
        var targets = new List<(ZDOID, ZDOExtraData.ConnectionType, int)>();
        foreach (ZDOID source in ZDOExtraData.GetAllConnectionZDOIDs())
        {
            ZDOConnection c = ZDOExtraData.GetConnection(source);
            if (c == null || c.m_type == ZDOExtraData.ConnectionType.None)
            {
                continue;
            }
            int hash = LinkHash(source);
            Links[hash] = (c.m_type, source, c.m_target);
            of[source] = (c.m_type, hash);
            if (c.m_target != ZDOID.None)
            {
                targets.Add((c.m_target, c.m_type | ZDOExtraData.ConnectionType.Target, hash));
            }
        }
        // One link per object in the save: the one it holds before the one it is the target of.
        foreach (var (target, type, hash) in targets)
        {
            if (!of.ContainsKey(target))
            {
                of[target] = (type, hash);
            }
        }
        return of;
    }

    /// <summary>
    /// An object made again from the snapshot (its old link: type and hash) in place of one that is gone (an undone
    /// delete, a move): its link back, and the links to its old id now to it. A copy of an object still there (a
    /// paste) takes no link: it would take the original's.
    /// </summary>
    private static void Relink(ZDO made, ZDOExtraData.ConnectionType type, int hash)
    {
        if (!Links.TryGetValue(hash, out var link))
        {
            return;
        }
        bool holder = (type & ZDOExtraData.ConnectionType.Target) == 0;
        ZDOID old = holder ? link.Source : link.Target;
        if (ZDOMan.instance.GetZDO(old) != null)
        {
            return;
        }
        if (holder)
        {
            if (link.Target == ZDOID.None || ZDOMan.instance.GetZDO(link.Target) != null)
            {
                made.SetConnection(link.Type, link.Target);
            }
            Links[hash] = (link.Type, made.m_uid, link.Target);
        }
        else
        {
            Links[hash] = (link.Type, link.Source, made.m_uid);
        }
        foreach (ZDOID source in ZDOExtraData.GetAllConnectionZDOIDs())
        {
            ZDOConnection c = ZDOExtraData.GetConnection(source);
            if (c != null && c.m_target == old && ZDOMan.instance.GetZDO(source) is { } other)
            {
                other.UpdateConnection(c.m_type, made.m_uid);
            }
        }
    }

    /// <summary>
    /// Mirrors ZDO.Save (world version 41), reading the live data instead of the save-time copy.
    /// </summary>
    /// <param name="pkg">Where the object is written.</param>
    /// <param name="zdo">The object.</param>
    /// <param name="links">Each linked object's link, from <c>SnapshotLinks</c>.</param>
    private static void WriteZdo(ZPackage pkg, ZDO zdo, Dictionary<ZDOID, (ZDOExtraData.ConnectionType Type, int Hash)> links)
    {
        ZDOExtraData.GetData(zdo.m_uid, out var floats, out var vec3s, out var quats, out var ints, out var longs, out var strings, out var bytes, out ZDOConnection _);
        int flags = 0;
        bool hasConn = links.TryGetValue(zdo.m_uid, out var conn);
        if (hasConn)
        {
            flags |= 0x1;
        }
        if (floats.Count > 0)
        {
            flags |= 0x2;
        }
        if (vec3s.Count > 0)
        {
            flags |= 0x4;
        }
        if (quats.Count > 0)
        {
            flags |= 0x8;
        }
        if (ints.Count > 0)
        {
            flags |= 0x10;
        }
        if (longs.Count > 0)
        {
            flags |= 0x20;
        }
        if (strings.Count > 0)
        {
            flags |= 0x40;
        }
        if (bytes.Count > 0)
        {
            flags |= 0x80;
        }
        if (zdo.Persistent)
        {
            flags |= 0x100;
        }
        if (zdo.Distant)
        {
            flags |= 0x200;
        }
        flags |= ((int)zdo.Type & 3) << 10;
        Vector3 euler = ZdoRotation != null ? (Vector3)ZdoRotation.GetValue(zdo) : zdo.GetRotation().eulerAngles;
        // Same test as ZDO.Save (Vector3.CloseToZero).
        bool rotated = Mathf.Abs(euler.x) > 1E-05f || Mathf.Abs(euler.y) > 1E-05f || Mathf.Abs(euler.z) > 1E-05f;
        if (rotated)
        {
            flags |= 0x1000;
        }
        pkg.Write((ushort)flags);
        pkg.Write(zdo.GetPosition());
        pkg.Write(zdo.GetPrefab());
        if (rotated)
        {
            pkg.WriteSmallRotation(euler);
        }
        if ((flags & 0xFF) == 0)
        {
            return;
        }
        if (hasConn)
        {
            pkg.Write((byte)conn.Type);
            pkg.Write(conn.Hash);
        }
        Section(pkg, floats, pkg.Write);
        Section(pkg, vec3s, pkg.Write);
        Section(pkg, quats, pkg.Write);
        Section(pkg, ints, pkg.Write);
        Section(pkg, longs, pkg.Write);
        Section(pkg, strings, pkg.Write);
        Section(pkg, bytes, pkg.Write);
    }

    /// <summary>One typed data section of ZDO.Save: item count, then key and value of each; nothing when empty.</summary>
    private static void Section<T>(ZPackage pkg, List<KeyValuePair<int, T>> items, Action<T> write)
    {
        if (items.Count == 0)
        {
            return;
        }
        pkg.WriteNumItems(items.Count);
        foreach (var kv in items)
        {
            pkg.Write(kv.Key);
            write(kv.Value);
        }
    }
}
