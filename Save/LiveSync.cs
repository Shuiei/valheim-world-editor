using System.Numerics;
using System.Text.Json;
using TerrainEditor.Editing;

namespace TerrainEditor.Save;

// Keeps the running game's objects in step with the editor's deletions and new objects. It remembers
// what has already been applied, so each apply sends only the difference, and undo works after
// applying: a deleted object is created again from its snapshot bytes, a new one is destroyed.
public sealed class LiveSync
{
	private readonly object _lock = new();

	// Snapshot objects (ids >= 0) currently destroyed in the game.
	private readonly HashSet<int> _destroyed = new();

	// Objects whose live ZDOID is not the snapshot's: new objects (ids < 0) and restored ones.
	private readonly Dictionary<int, (long User, uint Id)> _liveIds = new();

	public void Reset()
	{
		lock (_lock)
		{
			_destroyed.Clear();
			_liveIds.Clear();
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
		List<(long, uint)> destroy = new();
		List<byte[]> create = new();
		List<int> createFor = new();
		List<string> skipped = new();
		lock (_lock)
		{
			HashSet<int> deleted = edits.Deleted;
			Dictionary<int, NewObject> added = edits.Added.ToDictionary(a => a.Id);
			(long, uint) LiveId(int id) => _liveIds.TryGetValue(id, out var l) ? l : world.ObjectRefs[id].LiveId;
			foreach (int id in deleted.Where(id => id >= 0 && id < world.ObjectRefs.Count && !_destroyed.Contains(id)))
			{
				destroy.Add(LiveId(id));
				_destroyed.Add(id);
				_liveIds.Remove(id);
			}
			// Undone deletions: the object comes back as a copy of its snapshot bytes.
			foreach (int id in _destroyed.Where(id => !deleted.Contains(id)).ToList())
			{
				ObjectRef o = world.ObjectRefs[id];
				create.Add(world.LiveBytes![(int)o.Start..(int)o.End]);
				createFor.Add(id);
				_destroyed.Remove(id);
			}
			// New objects not in the game yet, and ones that were removed again (undo, delete).
			foreach (NewObject n in added.Values.Where(n => !_liveIds.ContainsKey(n.Id)))
			{
				ObjectRef? model = world.ModelFor(n.Prefab, n.SourceId);
				if (model == null)
				{
					skipped.Add($"no object of that kind to copy at {n.Position.X:F0}, {n.Position.Z:F0}");
					continue;
				}
				create.Add(ZdoBuilder.Build(world.LiveBytes!, model, model.File.WorldVersion, n.Position, n.Rotation, n.Scale, n.Fresh));
				createFor.Add(n.Id);
			}
			foreach (int id in _liveIds.Keys.Where(id => id < 0 && !added.ContainsKey(id)).ToList())
			{
				destroy.Add(_liveIds[id]);
				_liveIds.Remove(id);
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
			for (int i = 0; i < ids.Count && i < createFor.Count; i++)
			{
				_liveIds[createFor[i]] = (long.Parse(ids[i][0]), uint.Parse(ids[i][1]));
			}
		}
		int destroyed = doc.RootElement.GetProperty("destroyed").GetInt32(), missing = doc.RootElement.GetProperty("missing").GetInt32();
		string msg = $"{destroyed} object(s) removed, {ids.Count} created";
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
