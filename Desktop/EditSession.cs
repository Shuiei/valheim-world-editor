using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

// Editing a scene: brush strokes on the ground, deleting and moving objects, undo and redo, what is
// waiting to be saved, and saving. Edits go to the EditStore and are written by WorldWriter, the same code the web editor's
// session uses (/api/zones, /api/save). Strokes run on the drawing thread, the buttons on the window's:
// everything here takes the lock. Objects are changed on the window's thread only.
public sealed class EditSession
{
	public WorldScene Scene { get; }
	public Ground Ground { get; }
	public EditStore Edits { get; }
	// The window shares its brush settings with the session.
	public Brush Brush { get; set; } = new();
	private readonly object _lock = new();

	// A change that can be undone: the ground points it changed, before and after, and the things it
	// took away or brought (index into the scene's things, gone before and after).
	public sealed record Change(string Label, int[] Points, Ground.State Before, Ground.State After, List<(int X, int Z)> Zones)
	{
		public (int Index, bool Before, bool After)[] Things { get; init; } = Array.Empty<(int, bool, bool)>();
	}
	private static readonly Ground.State NoPoints = new(Array.Empty<float>(), Array.Empty<float>(), Array.Empty<byte>(), Array.Empty<float>(), Array.Empty<byte>());
	private readonly List<Change> _undo = new(), _redo = new();
	public const int HistoryLength = 200;

	private Stroke? _stroke;
	// The grid rectangle changed since the view last took it (to send to the graphics card).
	private (int X0, int Z0, int X1, int Z1)? _dirty;
	public event Action? Changed;
	// Things that appeared, went or came back (indices into the scene's things); after a save, every
	// thing is read again (ThingsReset).
	public event Action<IReadOnlyList<int>>? ThingsChanged;
	public event Action? ThingsReset;
	// Ids for objects added in this session: negative, like the web editor's.
	private int _nextId = -1;

	public EditSession(WorldScene scene, Ground ground, EditStore edits)
	{
		Scene = scene;
		Ground = ground;
		Edits = edits;
	}

	public bool CanUndo { get { lock (_lock) { return _undo.Count > 0; } } }
	public bool CanRedo { get { lock (_lock) { return _redo.Count > 0; } } }
	public string? UndoLabel { get { lock (_lock) { return _undo.Count > 0 ? _undo[^1].Label : null; } } }
	public string? RedoLabel { get { lock (_lock) { return _redo.Count > 0 ? _redo[^1].Label : null; } } }
	public bool Stroking { get { lock (_lock) { return _stroke != null; } } }

	// What is waiting to be saved, like the web editor's line under the Save button.
	public (int Zones, int Deleted, int Added, int Resets) Pending => (Edits.ChangedZoneCount, Edits.DeletedCount, Edits.AddedCount, Edits.ResetCount);

	public string PendingText
	{
		get
		{
			var (z, d, a, r) = Pending;
			var parts = new[] { z > 0 ? $"{z} zone{(z > 1 ? "s" : "")}" : "", d > 0 ? $"{d} deleted" : "", a > 0 ? $"{a} added" : "", r > 0 ? $"{r} reset" : "" }.Where(p => p != "").ToList();
			return parts.Count > 0 ? "Unsaved: " + string.Join(", ", parts) : "All saved";
		}
	}

	// The brush goes down at grid point (cx, cz).
	public void BeginStroke(BrushTool tool, float cx, float cz)
	{
		lock (_lock)
		{
			_stroke = new Stroke { Tool = tool, Start = Ground.Snapshot() };
			// Flatten: level to the ground under the first click.
			if (tool == BrushTool.Flatten && Brush.TargetFromClick)
			{
				int gx = Math.Clamp((int)MathF.Round(cx), 0, Ground.W - 1), gz = Math.Clamp((int)MathF.Round(cz), 0, Ground.H - 1);
				_stroke.Target = Ground.HeightOf(gz * Ground.W + gx);
				Brush.Target = _stroke.Target.Value;
			}
		}
	}

	// One frame of the stroke, the brush at (cx, cz), dt seconds after the last.
	public bool StrokeStep(float cx, float cz, float dt)
	{
		lock (_lock)
		{
			if (_stroke == null || Sculpt.Apply(Ground, Brush, _stroke, cx, cz, dt) is not { } rect)
			{
				return false;
			}
			Touch(rect);
			return true;
		}
	}

	// The brush comes up: the zones it touched go to the edit store and the stroke to the history.
	// Returns a message for the status line ("" when there is nothing to say).
	public string EndStroke()
	{
		string message;
		lock (_lock)
		{
			var s = _stroke;
			_stroke = null;
			if (s == null)
			{
				return "";
			}
			if (s.Touched.Count > 0)
			{
				var zones = Ground.ZonesOf(s.Touched);
				Record(Brush.Label(s.Tool), s.Start, s.Touched, zones);
				Send(zones);
			}
			message = s.Clamped ? "Reached the game limit: ground can only move 8 m from its original height (red points)." : "";
		}
		Changed?.Invoke();
		return message;
	}

	private void Record(string label, Ground.State start, IEnumerable<int> touched, List<(int X, int Z)> zones)
	{
		int[] pts = touched.Order().ToArray();
		var now = Ground.Snapshot();
		Ground.State Pick(Ground.State all) => new(
			pts.Select(p => all.Level[p]).ToArray(), pts.Select(p => all.Smooth[p]).ToArray(), pts.Select(p => all.Mod[p]).ToArray(),
			pts.SelectMany(p => new[] { all.Paint[p * 4], all.Paint[p * 4 + 1], all.Paint[p * 4 + 2], all.Paint[p * 4 + 3] }).ToArray(), pts.Select(p => all.PMod[p]).ToArray());
		_undo.Add(new Change(label, pts, Pick(start), Pick(now), zones));
		if (_undo.Count > HistoryLength)
		{
			_undo.RemoveAt(0);
		}
		_redo.Clear();
	}

	public bool Undo() => Step(_undo, _redo, after: false);

	public bool Redo() => Step(_redo, _undo, after: true);

	private bool Step(List<Change> from, List<Change> to, bool after)
	{
		Change c;
		lock (_lock)
		{
			if (_stroke != null || from.Count == 0)
			{
				return false;
			}
			c = from[^1];
			from.RemoveAt(from.Count - 1);
			var v = after ? c.After : c.Before;
			int x0 = int.MaxValue, z0 = int.MaxValue, x1 = 0, z1 = 0;
			for (int k = 0; k < c.Points.Length; k++)
			{
				int p = c.Points[k];
				Ground.Level[p] = v.Level[k];
				Ground.Smooth[p] = v.Smooth[k];
				Ground.Mod[p] = v.Mod[k];
				Array.Copy(v.Paint, k * 4, Ground.Paint, p * 4, 4);
				Ground.PMod[p] = v.PMod[k];
				int gx = p % Ground.W, gz = p / Ground.W;
				x0 = Math.Min(x0, gx); z0 = Math.Min(z0, gz); x1 = Math.Max(x1, gx); z1 = Math.Max(z1, gz);
			}
			to.Add(c);
			if (c.Points.Length > 0)
			{
				Touch((x0 - 1, z0 - 1, x1 + 1, z1 + 1));
			}
			Send(c.Zones);
			SetGone(c.Things.Select(t => (t.Index, after ? t.After : t.Before)));
		}
		if (c.Things.Length > 0)
		{
			ThingsChanged?.Invoke(c.Things.Select(t => t.Index).ToList());
		}
		Changed?.Invoke();
		return true;
	}

	// Takes things away or brings them back, in the scene and in the edit store (an object added in this
	// session goes to its trash, so it can come back).
	private void SetGone(IEnumerable<(int Index, bool Gone)> things)
	{
		var list = Scene.Things;
		lock (list)
		{
			foreach (var (i, gone) in things)
			{
				list[i] = list[i] with { Gone = gone };
				Edits.SetDeleted(new[] { list[i].Id }, gone);
			}
		}
	}

	private void AddChange(Change c)
	{
		lock (_lock)
		{
			_undo.Add(c);
			if (_undo.Count > HistoryLength)
			{
				_undo.RemoveAt(0);
			}
			_redo.Clear();
		}
	}

	// Removes things from the world (on the next save).
	public void Delete(IReadOnlyCollection<int> indices)
	{
		var live = indices.Where(i => !Scene.Things[i].Gone).Distinct().ToArray();
		if (live.Length == 0)
		{
			return;
		}
		SetGone(live.Select(i => (i, true)));
		AddChange(new Change($"Deleted {live.Length}", Array.Empty<int>(), NoPoints, NoPoints, new()) { Things = live.Select(i => (i, false, true)).ToArray() });
		ThingsChanged?.Invoke(live);
		Changed?.Invoke();
	}

	// Moves (and turns) things: each is replaced by a copy at its new place that keeps its own data
	// (chest contents, builder...), like the web editor's moves. Returns the copies' indices.
	public List<int> Move(IReadOnlyList<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation)> moves, string? label = null)
	{
		var list = Scene.Things;
		var added = new List<NewObject>();
		var copies = new List<int>();
		var changes = new List<(int, bool, bool)>();
		lock (list)
		{
			foreach (var (i, pos, rot) in moves)
			{
				var t = list[i];
				if (t.Gone)
				{
					continue;
				}
				// A copy of an object of this session keeps what that one was copied from.
				var from = t.Id < 0 ? Edits.FindAdded(t.Id) : null;
				var n = new NewObject(_nextId--, t.Prefab, pos, rot, t.Scale, t.Id < 0 ? from?.SourceId : t.Id, t.Id < 0 && (from?.Fresh ?? true), from?.Raw);
				added.Add(n);
				list.Add(new WorldScene.Thing(n.Id, t.Prefab, pos, rot, t.Scale, t.Piece));
				copies.Add(list.Count - 1);
				changes.Add((i, false, true));
				changes.Add((list.Count - 1, true, false));
			}
		}
		if (added.Count == 0)
		{
			return copies;
		}
		Edits.AddObjects(added);
		SetGone(changes.Where(c => c.Item3).Select(c => (c.Item1, true)));
		AddChange(new Change(label ?? $"Moved {added.Count}", Array.Empty<int>(), NoPoints, NoPoints, new()) { Things = changes.ToArray() });
		ThingsChanged?.Invoke(changes.Select(c => c.Item1).ToList());
		Changed?.Invoke();
		return copies;
	}

	// The zones to the edit store, as the web page uploads them.
	private void Send(List<(int X, int Z)> zones)
	{
		foreach (var (zx, zz) in zones)
		{
			Edits.Put(Ground.ZoneEdit(zx, zz));
		}
	}

	// The scene's heights, paint mask and limit marks follow the ground over a rectangle.
	private void Touch((int X0, int Z0, int X1, int Z1) r)
	{
		int x0 = Math.Max(0, r.X0), z0 = Math.Max(0, r.Z0), x1 = Math.Min(Ground.W - 1, r.X1), z1 = Math.Min(Ground.H - 1, r.Z1);
		Scene.Refresh(Ground, x0, z0, x1, z1);
		_dirty = _dirty is { } d ? (Math.Min(d.X0, x0), Math.Min(d.Z0, z0), Math.Max(d.X1, x1), Math.Max(d.Z1, z1)) : (x0, z0, x1, z1);
	}

	// The rectangle changed since the last call (null: nothing).
	public (int X0, int Z0, int X1, int Z1)? TakeDirty()
	{
		lock (_lock)
		{
			var d = _dirty;
			_dirty = null;
			return d;
		}
	}

	// Writes the changes into the world's files (with a backup first, see WorldWriter), then starts over
	// from what was written: nothing pending, an empty history (like the web editor reloading its page).
	public WorldWriter.Result Save()
	{
		WorldWriter.Result result;
		lock (_lock)
		{
			if (_stroke != null)
			{
				throw new InvalidOperationException("A stroke is still going on.");
			}
			var changed = Edits.All().Where(e => e.Changed).ToList();
			result = WorldWriter.Save(Scene.World, changed, Edits.Deleted, Edits.Added, Edits.Resets);
			if (result.Saved)
			{
				var fresh = WorldSave.Load(Scene.World.Directory);
				Scene.World = fresh;
				Edits.ResetFrom(fresh);
				Ground.TakeEdits(Edits);
				_undo.Clear();
				_redo.Clear();
				// Saving gives objects new ids: read them again.
				var things = WorldScene.ReadThings(fresh, Scene.X0, Scene.Z0, Scene.Size, Edits.Deleted);
				lock (Scene.Things)
				{
					Scene.Things.Clear();
					Scene.Things.AddRange(things);
				}
				_nextId = -1;
			}
		}
		if (result.Saved)
		{
			ThingsReset?.Invoke();
		}
		Changed?.Invoke();
		return result;
	}
}
