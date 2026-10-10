using System.Numerics;
using System.Text.Json;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

// An object in the running game: its ZDOID, and what it is and where, to find it again when another
// mod has made it again under a new ZDOID.
public readonly record struct LiveTarget(long User, uint Id, int Prefab, Vector3 Position);

// Keeps the running game's objects in step with the editor's deletions and new objects. It remembers
// what has already been applied, so each apply sends only the difference, and undo works after
// applying: a deleted object is created again from its snapshot bytes, a new one is destroyed.
public sealed class LiveSync
{
	private readonly object _lock = new();

	// Snapshot objects (ids >= 0) currently destroyed in the game.
	private readonly HashSet<int> _destroyed = new();

	// Objects whose live ZDOID is not the snapshot's: new objects (ids < 0) and restored ones.
	private readonly Dictionary<int, LiveTarget> _liveIds = new();

	public void Reset()
	{
		lock (_lock)
		{
			_destroyed.Clear();
			_liveIds.Clear();
		}
	}

	// Whether the game has this new object already (it was sent and accepted).
	public bool IsLive(int id)
	{
		lock (_lock)
		{
			return _liveIds.ContainsKey(id);
		}
	}

	// Whether the game no longer has this object because the editor removed it (applied).
	public bool IsDestroyed(int id)
	{
		lock (_lock)
		{
			return _destroyed.Contains(id);
		}
	}

	// The game removed an object the editor made (a player picked or destroyed it): no longer the
	// editor's to remove or to count.
	public void Forget(int id)
	{
		lock (_lock)
		{
			_liveIds.Remove(id);
		}
	}

	// The editor's new objects the game has, by id (ids < 0).
	public List<(int Id, LiveTarget Target)> NewObjects()
	{
		lock (_lock)
		{
			return _liveIds.Where(kv => kv.Key < 0).Select(kv => (kv.Key, kv.Value)).ToList();
		}
	}

	// Whether this ZDOID is one the editor made (a new object, or a removed one brought back).
	public bool IsOurs((long User, uint Id) zdo)
	{
		lock (_lock)
		{
			return _liveIds.Values.Any(t => t.User == zdo.User && t.Id == zdo.Id);
		}
	}

	public (int Deleted, int Added) Pending(EditStore edits)
	{
		lock (_lock)
		{
			HashSet<int> deleted = edits.Deleted;
			HashSet<int> added = edits.Added.Select(a => a.Id).ToHashSet();
			int d = deleted.Count(id => id >= 0 && !_destroyed.Contains(id)) + _destroyed.Count(id => !deleted.Contains(id));
			int a = added.Count(id => !_liveIds.ContainsKey(id)) + _liveIds.Keys.Count(id => id < 0 && !added.Contains(id));
			return (d, a);
		}
	}

	public async Task<string> Apply(WorldSave world, EditStore edits, LiveBridge live)
	{
		List<LiveTarget> destroy = new();
		List<byte[]> create = new();
		List<int> createFor = new();
		// What each creation is and where (for its LiveTarget once the game gives its ZDOID).
		List<(int Prefab, Vector3 Position)> createWhat = new();
		List<string> skipped = new();
		// What the game has once it accepts the call (nothing is recorded before: a refused call is sent
		// again next time).
		List<int> destroyIds = new(), restoreIds = new(), dropNew = new();
		lock (_lock)
		{
			HashSet<int> deleted = edits.Deleted;
			Dictionary<int, NewObject> added = edits.Added.ToDictionary(a => a.Id);
			LiveTarget LiveId(int id)
			{
				if (_liveIds.TryGetValue(id, out var l))
				{
					return l;
				}
				ObjectRef o = world.ObjectRefs[id];
				return new LiveTarget(o.LiveId.User, o.LiveId.Id, o.Prefab, o.Position);
			}
			foreach (int id in deleted.Where(id => id >= 0 && id < world.ObjectRefs.Count && !_destroyed.Contains(id)))
			{
				destroy.Add(LiveId(id));
				destroyIds.Add(id);
			}
			// Undone deletions: the object comes back as a copy of its snapshot bytes.
			foreach (int id in _destroyed.Where(id => !deleted.Contains(id)).ToList())
			{
				ObjectRef o = world.ObjectRefs[id];
				create.Add(world.ObjectBytes(id));
				createFor.Add(id);
				createWhat.Add((o.Prefab, o.Position));
				restoreIds.Add(id);
			}
			// New objects not in the game yet, and ones that were removed again (undo, delete).
			foreach (NewObject n in added.Values.Where(n => !_liveIds.ContainsKey(n.Id)))
			{
				byte[]? bytes = world.NewObjectBytes(n, m => world.LiveSource(m.File));
				if (bytes == null)
				{
					skipped.Add($"unknown kind of object at {n.Position.X:F0}, {n.Position.Z:F0}");
					continue;
				}
				create.Add(bytes);
				createFor.Add(n.Id);
				createWhat.Add((n.Prefab, n.Position));
			}
			foreach (int id in _liveIds.Keys.Where(id => id < 0 && !added.ContainsKey(id)).ToList())
			{
				destroy.Add(_liveIds[id]);
				dropNew.Add(id);
			}
		}
		if (destroy.Count + create.Count == 0)
		{
			return "";
		}
		string reply = await live.ApplyObjects(destroy, create);
		using JsonDocument doc = JsonDocument.Parse(reply);
		var ids = doc.RootElement.GetProperty("created").EnumerateArray().Select(e => e.GetString()!.Split(':')).ToList();
		lock (_lock)
		{
			foreach (int id in destroyIds)
			{
				_destroyed.Add(id);
				_liveIds.Remove(id);
			}
			foreach (int id in restoreIds)
			{
				_destroyed.Remove(id);
			}
			foreach (int id in dropNew)
			{
				_liveIds.Remove(id);
			}
			for (int i = 0; i < ids.Count && i < createFor.Count; i++)
			{
				_liveIds[createFor[i]] = new LiveTarget(long.Parse(ids[i][0]), uint.Parse(ids[i][1]), createWhat[i].Prefab, createWhat[i].Position);
			}
		}
		// Objects of the world made again in the game (an applied delete undone): followed under their new
		// ZDOID from now on (WorldSave.MergeLive), as the world's own.
		for (int i = 0; i < ids.Count && i < createFor.Count; i++)
		{
			if (createFor[i] >= 0)
			{
				world.SetLiveId(createFor[i], (long.Parse(ids[i][0]), uint.Parse(ids[i][1])));
			}
		}
		int destroyed = doc.RootElement.GetProperty("destroyed").GetInt32(), missing = doc.RootElement.GetProperty("missing").GetInt32();
		string msg = $"{destroyed} object(s) removed, {ids.Count} created";
		if (doc.RootElement.TryGetProperty("refound", out var rf) && rf.GetInt32() is > 0 and int refound)
		{
			msg += $" ({refound} made again by another mod, found at their place)";
		}
		if (missing > 0)
		{
			msg += $", {missing} were already gone";
		}
		if (skipped.Count > 0)
		{
			msg += $", {skipped.Count} skipped ({skipped[0]})";
		}
		return msg;
	}
}
