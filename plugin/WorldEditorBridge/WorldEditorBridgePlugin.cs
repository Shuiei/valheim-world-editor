using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

namespace WorldEditorBridge;

// Live bridge for the Valheim world editor. Runs on the dedicated server (or a host) and answers on a
// local HTTP port, protected by a token:
//   GET /snapshot   the whole persistent world: world info, ZoneSystem data (generated zones, locations)
//                   and every persistent object in the save file's chunk format, gzip-compressed
//   GET /players    connected players and their positions (JSON)
//   GET /status     plugin and world info (JSON)
//   POST /terrain   new terrain data for zones: int count, then per zone int x, int z, int length + TCData
//                   (TerrainComp.Save format); every player's game reloads those zones' ground
//   POST /objects   int n + n x (long user, uint id) objects to destroy, then int m + m x (int length +
//                   object in save format) objects to create; answers with the new objects' ids
//   POST /zones/reset  int n + n x (int x, int z, bool keep buildings, bool reset ground): the zones'
//                   objects are removed and the zones are generated again (at once where players are)
// Game objects are only read on Unity's main thread; the HTTP thread waits for the answer.
[BepInPlugin(Guid, "WorldEditorBridge", Version)]
public sealed class WorldEditorBridgePlugin : BaseUnityPlugin
{
	public const string Guid = "Tie.WorldEditorBridge";

	// The id before 0.41.0, which named the settings file.
	private const string OldGuid = "local.worldeditorbridge";

	public const string Version = BuildInfo.Version;

	private const int SnapshotVersion = 1;

	private ConfigEntry<string> _bind;

	private ConfigEntry<int> _port;

	private ConfigEntry<string> _token;

	private HttpListener _listener;

	private Thread _thread;

	private volatile bool _running;

	private readonly ConcurrentQueue<Job> _jobs = new();

	private static readonly FieldInfo ObjectsById = typeof(ZDOMan).GetField("m_objectsByID", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo GeneratedZones = typeof(ZoneSystem).GetField("m_generatedZones", BindingFlags.Instance | BindingFlags.NonPublic);

	// ZDO keeps its rotation as Euler angles; reading them avoids a quaternion round trip.
	private static readonly FieldInfo ZdoRotation = typeof(ZDO).GetField("m_rotation", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly FieldInfo GlobalKeys = typeof(ZoneSystem).GetField("m_globalKeys", BindingFlags.Instance | BindingFlags.NonPublic);

	private sealed class Job
	{
		public Func<byte[]> Work;

		public byte[] Result;

		public Exception Error;

		public readonly ManualResetEventSlim Done = new(false);

		// 0 waiting, 1 taken by the main thread, 2 given up by the caller (never runs).
		public int State;
	}

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
				Logger.LogInfo($"Settings moved from {OldGuid}.cfg to {Guid}.cfg");
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
			Logger.LogInfo($"WorldEditorBridge {Version} listening on http://{_bind.Value}:{_port.Value}/ (token in BepInEx/config/{Guid}.cfg)");
		}
		catch (Exception ex)
		{
			Logger.LogError($"WorldEditorBridge could not listen on {_bind.Value}:{_port.Value}: {ex.Message}");
		}
	}

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

	private void Update()
	{
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
			switch (ctx.Request.Url.AbsolutePath)
			{
			case "/status":
				Reply(res, 200, "application/json", OnMainThread(Status));
				break;
			case "/players":
				Reply(res, 200, "application/json", OnMainThread(Players));
				break;
			case "/terrain":
			{
				if (ctx.Request.HttpMethod != "POST")
				{
					Reply(res, 405, "text/plain", Encoding.UTF8.GetBytes("POST only"));
					break;
				}
				byte[] body = ReadAll(ctx.Request.InputStream);
				Reply(res, 200, "application/json", OnMainThread(() => ApplyTerrain(body)));
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
				Reply(res, 200, "application/json", OnMainThread(() => ApplyObjects(body)));
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
				Reply(res, 200, "application/json", OnMainThread(() => ResetZones(body)));
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
			Logger.LogWarning($"WorldEditorBridge request {ctx.Request.Url.AbsolutePath} failed: {ex}");
			try
			{
				Reply(res, 500, "text/plain", Encoding.UTF8.GetBytes(ex.Message));
			}
			catch
			{
			}
		}
	}

	private static byte[] ReadAll(Stream s)
	{
		using MemoryStream ms = new();
		s.CopyTo(ms);
		return ms.ToArray();
	}

	private static readonly int TerrainCompilerPrefab = "_TerrainCompiler".GetStableHashCode();

	// Sets the terrain data of each zone's terrain compiler object (creating it when the zone has
	// none). The game syncs the object to everyone, and TerrainComp reloads when its data changes.
	private byte[] ApplyTerrain(byte[] body)
	{
		ZPackage pkg = new(body);
		int count = pkg.ReadInt();
		var dict = (Dictionary<ZDOID, ZDO>)ObjectsById.GetValue(ZDOMan.instance);
		Dictionary<Vector2s, ZDO> compilers = new();
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
		for (int i = 0; i < count; i++)
		{
			int zx = pkg.ReadInt(), zz = pkg.ReadInt();
			byte[] data = pkg.ReadByteArray();
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
		return Encoding.UTF8.GetBytes($"{{\"applied\":{applied},\"created\":{created}}}");
	}

	// Destroys objects (as their owner, so every peer drops them) and creates new ones from objects in
	// the save file format (flags, position, prefab, rotation, data), like ZDO.Load but for a new object.
	// Editors from 1.15.4 add each destroyed object's prefab and position: another mod may have made it
	// again under a new ZDOID (ServersideQoL does, for build pieces, plants, fires and more), and then
	// the object of that prefab at that place goes instead. Without this, undoing a placed piece found
	// it "already gone" while every player kept seeing the new one.
	private byte[] ApplyObjects(byte[] body)
	{
		ZPackage pkg = new(body);
		long session = ZDOMan.GetSessionID();
		int destroyCount = pkg.ReadInt();
		ZDOID[] destroyIds = new ZDOID[destroyCount];
		for (int i = 0; i < destroyCount; i++)
		{
			destroyIds[i] = new ZDOID(pkg.ReadLong(), pkg.ReadUInt());
		}
		int createCount = pkg.ReadInt();
		// Every object to make is read first: a malformed one fails the call before anything changed,
		// so the editor, which records nothing then, can send it again without doubles.
		Func<ZDO>[] creates = new Func<ZDO>[createCount];
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
		List<int> lost = new();
		for (int i = 0; i < destroyCount; i++)
		{
			ZDO zdo = ZDOMan.instance.GetZDO(destroyIds[i]);
			if (zdo == null)
			{
				lost.Add(i);
				continue;
			}
			zdo.SetOwner(session);
			ZDOMan.instance.DestroyZDO(zdo);
			destroyed++;
		}
		if (lost.Count > 0 && where != null)
		{
			refound = DestroyAtPlace(lost.Select(i => where[i]).ToList(), session);
		}
		int missing = lost.Count - refound;
		StringBuilder ids = new();
		for (int i = 0; i < createCount; i++)
		{
			ZDO zdo = creates[i]();
			if (i > 0)
			{
				ids.Append(',');
			}
			ids.Append('"').Append(zdo.m_uid.UserID.ToString(CultureInfo.InvariantCulture)).Append(':').Append(zdo.m_uid.ID.ToString(CultureInfo.InvariantCulture)).Append('"');
		}
		Logger.LogInfo($"WorldEditorBridge: destroyed {destroyed + refound} object(s) ({refound} found again at their place under a new id, {missing} already gone), created {createCount}");
		return Encoding.UTF8.GetBytes($"{{\"destroyed\":{destroyed + refound},\"refound\":{refound},\"missing\":{missing},\"created\":[{ids}]}}");
	}

	// The objects of these prefabs at these places (within 10 cm: a re-made object keeps its position),
	// each at most once; returns how many were found and destroyed.
	private static int DestroyAtPlace(List<(int Prefab, Vector3 Position)> wanted, long session)
	{
		Dictionary<int, List<Vector3>> byPrefab = new();
		foreach (var (prefab, pos) in wanted)
		{
			if (!byPrefab.TryGetValue(prefab, out var list))
			{
				byPrefab[prefab] = list = new List<Vector3>();
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
			zdo.SetOwner(session);
			ZDOMan.instance.DestroyZDO(zdo);
			found++;
		}
		return found;
	}

	private static readonly int TombstonePrefab = "Player_tombstone".GetStableHashCode();

	// Like the editor's offline reset: every saved object of the zones goes (player-built pieces stay when
	// asked, the ground edits only when asked, players' tombstones always stay), the zones are no longer
	// marked as generated and their locations as not placed, so the game builds them again.
	private byte[] ResetZones(byte[] body)
	{
		ZPackage pkg = new(body);
		int count = pkg.ReadInt();
		Dictionary<Vector2s, (bool Keep, bool Ground)> zones = new();
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
		return Encoding.UTF8.GetBytes($"{{\"zones\":{zones.Count},\"destroyed\":{destroyed},\"locations\":{locations}}}");
	}

	// An object as the save writes it (ZDO.Save), read whole into what makes it: nothing is made in the
	// game until that is called.
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
		List<Action<ZDO>> values = new();
		if ((flags & 0xFF) != 0)
		{
			if ((flags & 0x1) != 0)
			{
				p.ReadByte();
				p.ReadInt();
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
			return zdo;
		};
	}

	// Like ZNetView.Awake for the _TerrainCompiler prefab, at the zone centre.
	private static ZDO CreateTerrainCompiler(Vector2s zone)
	{
		GameObject prefab = ZNetScene.instance.GetPrefab(TerrainCompilerPrefab);
		ZNetView view = prefab != null ? prefab.GetComponent<ZNetView>() : null;
		ZDO zdo = ZDOMan.instance.CreateNewZDO(ZoneSystem.GetZonePos(zone), TerrainCompilerPrefab);
		zdo.Persistent = view == null || view.m_persistent;
		zdo.Type = view != null ? view.m_type : ZDO.ObjectType.Terrain;
		zdo.Distant = view != null && view.m_distant;
		zdo.SetPrefab(TerrainCompilerPrefab);
		return zdo;
	}

	private static void Reply(HttpListenerResponse res, int code, string type, byte[] body)
	{
		res.StatusCode = code;
		res.ContentType = type;
		res.ContentLength64 = body.Length;
		res.OutputStream.Write(body, 0, body.Length);
		res.OutputStream.Close();
	}

	private static string Json(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

	private static string F(float v) => v.ToString("R", CultureInfo.InvariantCulture);

	private byte[] Status()
	{
		World w = ZNet.World;
		var dict = (Dictionary<ZDOID, ZDO>)ObjectsById.GetValue(ZDOMan.instance);
		string json = $"{{\"plugin\":{Json(Version)},\"world\":{Json(w?.m_name ?? "")},\"seedName\":{Json(w?.m_seedName ?? "")},\"objects\":{dict.Count},\"players\":{ZNet.instance.GetPlayerList().Count},\"netTime\":{ZNet.instance.GetTimeSeconds().ToString("R", CultureInfo.InvariantCulture)}}}";
		return Encoding.UTF8.GetBytes(json);
	}

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

	// Snapshot layout (all little endian, strings as .NET BinaryWriter strings):
	//   "VWEB" int version, string world, string seedName, int seed, int worldGenVersion, double netTime
	//   int length + ZoneSystem package (as ZoneSystem.Save writes it, uncompressed)
	//   short 41, int count, count objects (ZDO.Save chunk format), then count x (long user, uint id)
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
		pkg.Write((short)41);
		pkg.Write(zdos.Count);
		foreach (ZDO zdo in zdos)
		{
			WriteZdo(pkg, zdo);
		}
		foreach (ZDO zdo in zdos)
		{
			pkg.Write(zdo.m_uid.UserID);
			pkg.Write(zdo.m_uid.ID);
		}
		return pkg.GetArray();
	}

	// Same layout as ZoneSystem.Save: generated zones, location version, global keys, locations.
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

	// Mirrors ZDO.Save (world version 41), reading the live data instead of the save-time copy.
	private static void WriteZdo(ZPackage pkg, ZDO zdo)
	{
		ZDOExtraData.GetData(zdo.m_uid, out var floats, out var vec3s, out var quats, out var ints, out var longs, out var strings, out var bytes, out ZDOConnection conn);
		int flags = 0;
		bool hasConn = conn != null && conn.m_type != ZDOExtraData.ConnectionType.None;
		if (hasConn) flags |= 0x1;
		if (floats.Count > 0) flags |= 0x2;
		if (vec3s.Count > 0) flags |= 0x4;
		if (quats.Count > 0) flags |= 0x8;
		if (ints.Count > 0) flags |= 0x10;
		if (longs.Count > 0) flags |= 0x20;
		if (strings.Count > 0) flags |= 0x40;
		if (bytes.Count > 0) flags |= 0x80;
		if (zdo.Persistent) flags |= 0x100;
		if (zdo.Distant) flags |= 0x200;
		flags |= ((int)zdo.Type & 3) << 10;
		Vector3 euler = ZdoRotation != null ? (Vector3)ZdoRotation.GetValue(zdo) : zdo.GetRotation().eulerAngles;
		// Same test as ZDO.Save (Vector3.CloseToZero).
		bool rotated = Mathf.Abs(euler.x) > 1E-05f || Mathf.Abs(euler.y) > 1E-05f || Mathf.Abs(euler.z) > 1E-05f;
		if (rotated) flags |= 0x1000;
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
			pkg.Write((byte)conn.m_type);
			pkg.Write(0);
		}
		Section(pkg, floats, v => pkg.Write(v));
		Section(pkg, vec3s, v => pkg.Write(v));
		Section(pkg, quats, v => pkg.Write(v));
		Section(pkg, ints, v => pkg.Write(v));
		Section(pkg, longs, v => pkg.Write(v));
		Section(pkg, strings, v => pkg.Write(v));
		Section(pkg, bytes, v => pkg.Write(v));
	}

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
