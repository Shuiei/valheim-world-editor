using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
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
///   GET /status     plugin and world info, the features this plugin has ("move", "once", "files", "update") and this run of it
///                   (a new id each time the game starts), as JSON
///   POST /terrain   new terrain data for zones: int count, then per zone int x, int z, int length + TCData
///                   (TerrainComp.Save format); every player's game reloads those zones' ground
///   POST /objects   int n + n x (long user, uint id) objects to destroy, then int m + m x (int length +
///                   object in save format) objects to create, then each destroyed object's prefab and place,
///                   then int k + k x (long user, uint id, int prefab, place, new place, turn, int length + object
///                   in save format) objects to move, then (1.97.0 on, "update") int k + k x bool: whether each
///                   move takes its object's new data in place; answers with the new and moved objects' ids. A call sent
///                   again under the same X-Call-Id header is answered again, not done twice
///   POST /zones/reset  int n + n x (int x, int z, bool keep buildings, bool reset ground), then (1.93.0 on)
///                   int k + k x int prefab kept with the buildings: the zones' objects are removed and the
///                   zones are generated again (at once where players are)
///   POST /watch     int n + n x (int x, int z): two change numbers per zone (JSON): one for what stays put
///                   (made, removed, changed), one for the kinds that move or tick on their own
///   POST /zone      the same zone list: those zones' saved objects, gzip-compressed
///   POST /zone/movers  the same zone list: only those zones' objects of the kinds that move or tick on their
///                   own (creatures, ships, carts, items, fires, smelters...), gzip-compressed
///   GET /catalogue  every prefab the game knows, mods' too (JSON, see WorldEditorBridgePlugin.Catalogue.cs)
///   POST /models    prefab names, one per line: what the game draws of each (JSON, see
///                   WorldEditorBridgePlugin.Models.cs)
///   GET /mods       the mods' plugins registered with the bridge (WorldEditorBridge.Api; JSON, see
///                   WorldEditorBridgePlugin.Mods.cs)
///   POST /mods/&lt;id&gt;/&lt;name&gt;  a call to one of a mod's own routes, answered by its plugin
///   GET /plugins    every plugin BepInEx loaded: GUID, name, version (JSON, see WorldEditorBridgePlugin.Files.cs)
///   GET /config?path=  a file under BepInEx/config (its bytes) or a folder there (JSON: its entries); read only
///   GET /world/meta the world's meta file as it lies on disk (its starting global keys as written there)
/// </code>
/// Game objects are only read on Unity's main thread; the HTTP thread waits for the answer.
/// On its own, the plugin keeps the editor's ground discs (No limit ground) spawning before everything else
/// (WorldEditorBridgePlugin.Discs.cs).
/// </summary>
[BepInPlugin(PluginGuid, "WorldEditorBridge", Version)]
[BepInDependency(JotunnGuid, BepInDependency.DependencyFlags.SoftDependency)]
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Unity owns its lifetime; OnDestroy stops the listener.")]
#pragma warning disable S2931 // Unity owns its lifetime; OnDestroy stops the listener.
public sealed partial class WorldEditorBridgePlugin : BaseUnityPlugin
#pragma warning restore S2931
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
    /// Secret every request must carry in its X-Bridge-Token header; made on first start when empty.
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
                Caught(ex, $"moving the settings from {OldGuid}.cfg to {PluginGuid}.cfg (the old file is left)");
            }
        }
        _bind = Config.Bind("Http", "BindAddress", "127.0.0.1", "Address to listen on. Keep 127.0.0.1 and reach it through an SSH tunnel (ssh -L 5182:127.0.0.1:5182 user@server). Another address lets this machine's local networks in (never the internet), and the token then travels unencrypted.");
        _port = Config.Bind("Http", "Port", 5182, "TCP port of the bridge.");
        _token = Config.Bind("Http", "Token", "", "Secret the editor must send (X-Bridge-Token header). Generated on first start when empty.");
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
#pragma warning disable S5332 // HttpListener serves HTTPS only on a certificate the OS binds to the port; every call needs the token.
            _listener.Prefixes.Add($"http://{_bind.Value}:{_port.Value}/");
#pragma warning restore S5332
            if (_bind.Value == "127.0.0.1")
            {
                _listener.Prefixes.Add($"http://localhost:{_port.Value}/");
            }
            _listener.Start();
            _running = true;
            _thread = new Thread(Serve) { IsBackground = true, Name = "WorldEditorBridge" };
            _thread.Start();
            Logger.LogInfo($"WorldEditorBridge {Version} listening on http://{_bind.Value}:{_port.Value}/ (token in BepInEx/config/{PluginGuid}.cfg)");
            if (!IsLoopbackBind(_bind.Value))
            {
                Logger.LogWarning($"WorldEditorBridge: BindAddress {_bind.Value} lets other machines of this one's local networks connect (never the internet's), "
                    + "and the token travels unencrypted (plain HTTP): anyone on those networks can read it and change the world. "
                    + "Keep 127.0.0.1 and reach it through an SSH tunnel (the editor makes one).");
            }
        }
        catch (Exception ex)
        {
            Caught(ex, $"listening on {_bind.Value}:{_port.Value} (the bridge does not answer)", level: LogLevel.Error);
        }
    }

    /// <summary>Whether BindAddress is this machine only (127.0.0.1, localhost, ::1, another loopback address).</summary>
    /// <param name="bind">The setting's value.</param>
    private static bool IsLoopbackBind(string bind) =>
        string.Equals(bind, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(bind.Trim('[', ']'), out var ip) && IPAddress.IsLoopback(ip));

    /// <summary>Stops the listener, which ends <c>Serve</c>. Unity calls it by name.</summary>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Unity calls it by name.")]
    private void OnDestroy()
    {
        _running = false;
        try
        {
            _listener?.Stop();
        }
        catch (Exception ex)
        {
            Caught(ex, "stopping the listener (the game is closing anyway)", brief: true);
        }
    }

    /// <summary>
    /// Runs at most four queued jobs per frame on Unity's main thread, skipping those their caller gave up on.
    /// Unity calls it by name.
    /// </summary>
    [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Unity calls it by name.")]
    private void Update()
    {
        KeepDiscsFirst();
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
                // Handed to the request waiting for the job, which throws it again: its catch logs it.
                job.Error = ex;
            }
            job.Done.Set();
        }
    }
}
