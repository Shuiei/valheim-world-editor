using TerrainEditor.Save;

namespace TerrainEditor.Editing;

// The editor's working copy of every zone's terrain modifications. It starts from the world file
// and receives the browser's edits; nothing here is written to disk (saving is a separate step).
public sealed class EditStore
{
	public const int Grid = 65;

	public const int Cells = Grid * Grid;

	// The game clamps edits to these ranges (TerrainComp).
	public const float MaxLevel = 8f;

	public const float MaxSmooth = 1f;

	private readonly object _lock = new();

	private readonly Dictionary<(int, int), ZoneEdit> _zones = new();

	// Bumped on every change so cached map detail can be rebuilt.
	public int Version { get; private set; }

	public EditStore(WorldSave world)
	{
		Load(world);
	}

	// Start over from a (re)loaded world: after saving, or to discard unsaved changes.
	// The page's undo history (its own JSON), kept here so it survives reloads of the page and moves of
	// the work area. It describes the pending changes, so it goes with them (ResetFrom).
	public string? History { get; set; }

	// Changes each time the pending changes are reset: history sent for an earlier generation (a page
	// closing after a save, for example) is not kept.
	public int HistoryGeneration { get; private set; }

	public void ResetFrom(WorldSave world)
	{
		lock (_lock)
		{
			History = null;
			HistoryGeneration++;
			_zones.Clear();
			_baseline.Clear();
			_deleted.Clear();
			_added.Clear();
			_addedTrash.Clear();
			_resets.Clear();
			Load(world);
			Version++;
		}
	}

	private void Load(WorldSave world)
	{
		foreach (TerrainZone z in world.TerrainZones)
		{
			if (z.ModifiedHeight.Length != Cells || z.ModifiedPaint.Length != Cells)
			{
				continue;
			}
			ZoneEdit edit = new(z.ZoneX, z.ZoneZ) { ExistsInWorld = true };
			for (int i = 0; i < Cells; i++)
			{
				edit.Modified[i] = z.ModifiedHeight[i];
				edit.Level[i] = z.LevelDelta[i];
				edit.Smooth[i] = z.SmoothDelta[i];
				edit.PaintModified[i] = z.ModifiedPaint[i];
				edit.Paint[i * 4] = z.Paint[i].X;
				edit.Paint[i * 4 + 1] = z.Paint[i].Y;
				edit.Paint[i * 4 + 2] = z.Paint[i].Z;
				edit.Paint[i * 4 + 3] = z.Paint[i].W;
			}
			_zones[(z.ZoneX, z.ZoneZ)] = edit;
			_baseline[(z.ZoneX, z.ZoneZ)] = edit.Clone();
		}
	}

	// Each zone as saved / applied: a zone is pending only while it differs from this, so undoing back
	// to it clears the change.
	private readonly Dictionary<(int, int), ZoneEdit> _baseline = new();

	public ZoneEdit? Get(int zx, int zz)
	{
		lock (_lock)
		{
			return _zones.TryGetValue((zx, zz), out ZoneEdit? e) ? e.Clone() : null;
		}
	}

	public List<ZoneEdit> All()
	{
		lock (_lock)
		{
			return _zones.Values.Select(e => e.Clone()).ToList();
		}
	}

	// Objects (by WorldSave id) to leave out of the next save.
	private readonly HashSet<int> _deleted = new();

	public HashSet<int> Deleted
	{
		get
		{
			lock (_lock)
			{
				return new HashSet<int>(_deleted);
			}
		}
	}

	public int DeletedCount
	{
		get
		{
			lock (_lock)
			{
				return _deleted.Count;
			}
		}
	}

	// New objects placed in the editor (planted, pasted, replacements), by the negative id the browser gave them.
	private readonly Dictionary<int, NewObject> _added = new();

	// Added objects that were deleted again (kept so undo can bring them back).
	private readonly Dictionary<int, NewObject> _addedTrash = new();

	// Zones to give back to the world generator.
	private readonly Dictionary<(int, int), ZoneReset> _resets = new();

	public List<NewObject> Added
	{
		get
		{
			lock (_lock)
			{
				return _added.Values.ToList();
			}
		}
	}

	public int AddedCount
	{
		get
		{
			lock (_lock)
			{
				return _added.Count;
			}
		}
	}

	// An object added in this session (also one deleted again, which undo can bring back).
	public NewObject? FindAdded(int id)
	{
		lock (_lock)
		{
			return _added.TryGetValue(id, out NewObject? o) ? o : _addedTrash.TryGetValue(id, out NewObject? t) ? t : null;
		}
	}

	public void AddObjects(IEnumerable<NewObject> objects)
	{
		lock (_lock)
		{
			foreach (NewObject o in objects.Where(o => o.Id < 0))
			{
				_addedTrash.Remove(o.Id);
				_added[o.Id] = o;
			}
			Version++;
		}
	}

	public List<ZoneReset> Resets
	{
		get
		{
			lock (_lock)
			{
				return _resets.Values.ToList();
			}
		}
	}

	public int ResetCount
	{
		get
		{
			lock (_lock)
			{
				return _resets.Count;
			}
		}
	}

	public void SetReset(ZoneReset reset, bool on)
	{
		lock (_lock)
		{
			if (on)
			{
				_resets[(reset.X, reset.Z)] = reset;
			}
			else
			{
				_resets.Remove((reset.X, reset.Z));
			}
			Version++;
		}
	}

	public void SetDeleted(IEnumerable<int> ids, bool deleted)
	{
		lock (_lock)
		{
			foreach (int id in ids)
			{
				if (id < 0)
				{
					// An object added in this session: deleting it just drops it.
					if (deleted && _added.Remove(id, out NewObject? o))
					{
						_addedTrash[id] = o;
					}
					else if (!deleted && _addedTrash.Remove(id, out NewObject? t))
					{
						_added[id] = t;
					}
					continue;
				}
				if (deleted)
				{
					_deleted.Add(id);
				}
				else
				{
					_deleted.Remove(id);
				}
			}
			Version++;
		}
	}

	public int ChangedZoneCount
	{
		get
		{
			lock (_lock)
			{
				return _zones.Values.Count(e => e.Changed);
			}
		}
	}

	// Zones that were just applied to the running game: no longer pending.
	public void MarkApplied(IEnumerable<(int X, int Z)> zones)
	{
		lock (_lock)
		{
			foreach (var key in zones)
			{
				if (_zones.TryGetValue(key, out ZoneEdit? e))
				{
					e.Changed = false;
					e.ExistsInWorld = true;
					_baseline[key] = e.Clone();
				}
			}
			Version++;
		}
	}

	// Zones the editor has put back to their saved / applied state (discarding pending changes).
	public void MarkUnchanged(IEnumerable<(int X, int Z)> zones)
	{
		lock (_lock)
		{
			foreach (var key in zones)
			{
				if (_zones.TryGetValue(key, out ZoneEdit? e))
				{
					e.Changed = false;
				}
			}
			Version++;
		}
	}

	public void Put(ZoneEdit incoming)
	{
		incoming.Sanitize();
		lock (_lock)
		{
			bool exists = _zones.TryGetValue((incoming.ZoneX, incoming.ZoneZ), out ZoneEdit? old);
			incoming.ExistsInWorld = exists && old!.ExistsInWorld;
			incoming.Changed = !_baseline.TryGetValue((incoming.ZoneX, incoming.ZoneZ), out ZoneEdit? baseline) ? !incoming.IsEmpty : !incoming.SameGround(baseline);
			_zones[(incoming.ZoneX, incoming.ZoneZ)] = incoming;
			Version++;
		}
	}
}

public sealed class ZoneEdit(int zoneX, int zoneZ)
{
	public int ZoneX { get; } = zoneX;

	public int ZoneZ { get; } = zoneZ;

	// True when the world file already has terrain data for this zone.
	public bool ExistsInWorld { get; set; }

	// True when the editor changed this zone since the world was loaded.
	public bool Changed { get; set; }

	public bool[] Modified { get; init; } = new bool[EditStore.Cells];

	public float[] Level { get; init; } = new float[EditStore.Cells];

	public float[] Smooth { get; init; } = new float[EditStore.Cells];

	public bool[] PaintModified { get; init; } = new bool[EditStore.Cells];

	// RGBA per vertex: r dirt, g cultivated, b paved, a vegetation allowed.
	public float[] Paint { get; init; } = new float[EditStore.Cells * 4];

	public int HeightCount => Modified.Count(m => m);

	public int PaintCount => PaintModified.Count(m => m);

	public bool IsEmpty => !Modified.Any(m => m) && !PaintModified.Any(m => m);

	// Same resulting ground and paint (how the height is split between level and smoothing aside).
	public bool SameGround(ZoneEdit other)
	{
		for (int i = 0; i < EditStore.Cells; i++)
		{
			float a = Modified[i] ? Level[i] + Smooth[i] : 0f, b = other.Modified[i] ? other.Level[i] + other.Smooth[i] : 0f;
			if (Math.Abs(a - b) > 1e-4f || PaintModified[i] != other.PaintModified[i])
			{
				return false;
			}
			if (PaintModified[i])
			{
				for (int c = 0; c < 4; c++)
				{
					if (Math.Abs(Paint[i * 4 + c] - other.Paint[i * 4 + c]) > 1e-4f)
					{
						return false;
					}
				}
			}
		}
		return true;
	}

	public ZoneEdit Clone() => new(ZoneX, ZoneZ)
	{
		ExistsInWorld = ExistsInWorld,
		Changed = Changed,
		Modified = (bool[])Modified.Clone(),
		Level = (float[])Level.Clone(),
		Smooth = (float[])Smooth.Clone(),
		PaintModified = (bool[])PaintModified.Clone(),
		Paint = (float[])Paint.Clone()
	};

	// Enforce the game's limits and drop no-op modifications, whatever the browser sent.
	public void Sanitize()
	{
		if (Modified.Length != EditStore.Cells || Level.Length != EditStore.Cells || Smooth.Length != EditStore.Cells || PaintModified.Length != EditStore.Cells || Paint.Length != EditStore.Cells * 4)
		{
			throw new ArgumentException("Zone data must have 65x65 entries.");
		}
		for (int i = 0; i < EditStore.Cells; i++)
		{
			Level[i] = float.IsFinite(Level[i]) ? Math.Clamp(Level[i], -EditStore.MaxLevel, EditStore.MaxLevel) : 0f;
			Smooth[i] = float.IsFinite(Smooth[i]) ? Math.Clamp(Smooth[i], -EditStore.MaxSmooth, EditStore.MaxSmooth) : 0f;
			if (!Modified[i])
			{
				Level[i] = 0f;
				Smooth[i] = 0f;
			}
			for (int c = 0; c < 4; c++)
			{
				float v = Paint[i * 4 + c];
				Paint[i * 4 + c] = float.IsFinite(v) ? Math.Clamp(v, 0f, 1f) : 0f;
			}
		}
	}
}

// A new object: prefab hash, position, Unity Euler rotation (degrees) and uniform scale (0 = as the
// template object). SourceId: the object it comes from (else the first object of the prefab).
// Fresh: a new independent object (only the builder is taken from the source); otherwise, for a
// moved object, all of the source's data (chest contents, health, builder...) is kept.
// Raw: the object's complete data in the save format (an object edited in the inspector, or restored
// from a backup); it is written with this position, rotation and scale instead of copying a source.
public sealed record NewObject(int Id, int Prefab, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation, float Scale, int? SourceId = null, bool Fresh = true, byte[]? Raw = null);

// Give a zone back to the world generator. KeepBuildings keeps player-built pieces; Ground also
// removes the terrain edits.
public sealed record ZoneReset(int X, int Z, bool KeepBuildings, bool Ground);
