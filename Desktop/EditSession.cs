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
	// The window's Mask too.
	public Mask Mask { get; set; } = new();
	private Func<int, float>? _strokeMask;

	// The mask over the ground as it is now (null when off).
	public Func<int, float>? MaskNow() => Mask.For(Ground, Scene.Biomes);
	private readonly object _lock = new();

	// A change that can be undone: the ground points it changed, before and after, and the things it
	// took away or brought (index into the scene's things, gone before and after).
	public sealed record Change(string Label, int[] Points, Ground.State Before, Ground.State After, List<(int X, int Z)> Zones)
	{
		public (int Index, bool Before, bool After)[] Things { get; init; } = Array.Empty<(int, bool, bool)>();
		// Zones marked for reset (on) or not.
		public (ZoneReset Reset, bool Before, bool After)[] Resets { get; init; } = Array.Empty<(ZoneReset, bool, bool)>();
		public DateTime Time { get; init; } = DateTime.Now;
		// Taken out (History panel: Remove), and the change a removal takes out.
		public bool Removed { get; set; }
		public Change? RevertOf { get; init; }

		// "3 zones · 2 new · 1 removed · 1 zone reset", like the web editor's history.
		public string Describe()
		{
			var parts = new List<string>();
			if (Zones.Count > 0) parts.Add($"{Zones.Count} zone{(Zones.Count > 1 ? "s" : "")}");
			int added = Things.Count(t => t.Before && !t.After), removed = Things.Count(t => !t.Before && t.After);
			if (added > 0) parts.Add($"{added} new");
			if (removed > 0) parts.Add($"{removed} removed");
			if (Resets.Length > 0) parts.Add($"{Resets.Length} zone reset");
			return string.Join(" · ", parts);
		}
	}

	// The history, oldest first: what can be undone, and what was undone (the next redo last).
	public IReadOnlyList<Change> UndoList { get { lock (_lock) { return _undo.ToList(); } } }
	public IReadOnlyList<Change> RedoList { get { lock (_lock) { return _redo.ToList(); } } }

	// Undoes every change made after this one.
	public void BackTo(Change c)
	{
		while (UndoList.Count > 0 && UndoList[^1] != c && Undo())
		{
		}
	}

	// Redoes up to and including this one.
	public void ForwardTo(Change c)
	{
		while (RedoList.Count > 0 && (UndoList.Count == 0 || UndoList[^1] != c) && Redo())
		{
		}
	}

	// Takes out one change and keeps everything done after it: its height change is taken off the points
	// it touched (later edits there stay), paint goes back where nothing repainted it since, its objects
	// and zone resets go back. The removal is itself a change that can be undone.
	public string RemoveChange(Change c)
	{
		if (c.Removed || c.RevertOf != null)
		{
			return "That change cannot be removed.";
		}
		Change removal;
		lock (_lock)
		{
			if (_stroke != null)
			{
				return "";
			}
			var g = Ground;
			var start = g.Snapshot();
			for (int k = 0; k < c.Points.Length; k++)
			{
				int p = c.Points[k];
				g.Level[p] = Math.Clamp(g.Level[p] + c.Before.Level[k] - c.After.Level[k], -EditStore.MaxLevel, EditStore.MaxLevel);
				g.Smooth[p] = Math.Clamp(g.Smooth[p] + c.Before.Smooth[k] - c.After.Smooth[k], -EditStore.MaxSmooth, EditStore.MaxSmooth);
				g.Mod[p] = (byte)(MathF.Abs(g.Level[p]) + MathF.Abs(g.Smooth[p]) > 1e-4f || c.Before.Mod[k] != 0 && g.Mod[p] != 0 ? 1 : 0);
				bool samePaint = g.PMod[p] == c.After.PMod[k];
				for (int ch = 0; ch < 4 && samePaint; ch++)
				{
					samePaint = MathF.Abs(g.Paint[p * 4 + ch] - c.After.Paint[k * 4 + ch]) < 1e-4f;
				}
				if (samePaint)
				{
					g.PMod[p] = c.Before.PMod[k];
					Array.Copy(c.Before.Paint, k * 4, g.Paint, p * 4, 4);
				}
			}
			int[] pts = c.Points;
			var things = c.Things.Select(t => (t.Index, Scene.Things[t.Index].Gone, t.Before)).Where(t => t.Gone != t.Before).ToArray();
			var marked = Edits.Resets.Select(r => (r.X, r.Z)).ToHashSet();
			var resets = c.Resets.Select(r => (r.Reset, marked.Contains((r.Reset.X, r.Reset.Z)), r.Before)).ToArray();
			removal = new Change($"Removed: {c.Label}", pts, Pick(start, pts), Pick(g.Snapshot(), pts), c.Zones) { Things = things, Resets = resets, RevertOf = c };
			if (pts.Length > 0)
			{
				Touch((0, 0, g.W - 1, g.H - 1));
			}
			Send(c.Zones);
			SetGone(things.Select(t => (t.Index, t.Before)));
			foreach (var (r, _, on) in resets)
			{
				Edits.SetReset(r, on);
			}
			c.Removed = true;
		}
		AddChange(removal);
		if (removal.Things.Length > 0)
		{
			ThingsChanged?.Invoke(removal.Things.Select(t => t.Index).ToList());
		}
		Changed?.Invoke();
		return $"Removed “{c.Label}”; everything else is kept. Ctrl+Z brings it back.";
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
	public (int Zones, int Deleted, int Added, int Resets) Pending => Scene.Owner?.Pending ?? (Edits.ChangedZoneCount, Edits.DeletedCount, Edits.AddedCount, Edits.ResetCount);
	public bool IsLive => Scene.Owner?.IsLive == true;

	public string PendingText
	{
		get
		{
			var (z, d, a, r) = Pending;
			var parts = new[] { z > 0 ? $"{z} zone{(z > 1 ? "s" : "")}" : "", d > 0 ? $"{d} deleted" : "", a > 0 ? $"{a} added" : "", r > 0 ? $"{r} reset" : "" }.Where(p => p != "").ToList();
			return parts.Count > 0 ? (IsLive ? "Not applied: " : "Unsaved: ") + string.Join(", ", parts) : IsLive ? "All applied" : "All saved";
		}
	}

	// The brush goes down at grid point (cx, cz).
	public void BeginStroke(BrushTool tool, float cx, float cz)
	{
		lock (_lock)
		{
			_stroke = new Stroke { Tool = tool, Start = Ground.Snapshot() };
			// Judged as the ground was when the stroke started.
			_strokeMask = Mask.For(Ground, Scene.Biomes, frozen: true);
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
			if (_stroke == null || Sculpt.Apply(Ground, Brush, _stroke, cx, cz, dt, _strokeMask) is not { } rect)
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
			message = s.Clamped ? "Reached the game limit: ground can only move 8 m from its original height (red points)."
				: s.Touched.Count == 0 && _strokeMask != null ? "Nothing changed: the Mask leaves out all the ground under the brush (check its height, slope, biome and paint settings)." : "";
			_strokeMask = null;
		}
		Changed?.Invoke();
		return message;
	}

	// A change of the ground made all at once (a path, an area action): change gets the ground and returns
	// the points it changed and the grid rectangle around them. One undo step.
	public List<int> EditGround(string label, Func<Ground, (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect)> change)
	{
		List<int> touched;
		lock (_lock)
		{
			var start = Ground.Snapshot();
			(touched, var rect) = change(Ground);
			if (touched.Count > 0)
			{
				Touch(rect);
				var zones = Ground.ZonesOf(touched);
				Record(label, start, touched, zones);
				Send(zones);
			}
		}
		Changed?.Invoke();
		return touched;
	}

	// Puts a shape into the ground around grid point (cx, cz) (Shape tool): the formula gives the metres
	// to add at each point within the radius (x, z metres east and north of the middle, d the distance,
	// r the radius, h the height, n(x, z) the brushes' noise). One undo step.
	public (int Touched, bool Clamped, int Bad) Shape(float cx, float cz, Func<Formula.Env, double> f, float r, float h, string label)
	{
		// The web editor gives shapes its fractal noise scaled to about -1..1.
		var env = new Formula.Env { Noise = (x, z) => Brush.Noise.Fbm((float)x, (float)z) / 1.6 };
		var mask = MaskNow();
		int touchedCount, bad = 0;
		bool clamped = false;
		lock (_lock)
		{
			var g = Ground;
			var start = g.Snapshot();
			var touched = new List<int>();
			int x0 = Math.Max(1, (int)MathF.Floor(cx - r)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(cx + r));
			int z0 = Math.Max(1, (int)MathF.Floor(cz - r)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(cz + r));
			for (int gz = z0; gz <= z1; gz++)
			{
				for (int gx = x0; gx <= x1; gx++)
				{
					float x = gx - cx, z = gz - cz, d = MathF.Sqrt(x * x + z * z);
					int p = gz * g.W + gx;
					float w = mask?.Invoke(p) ?? 1;
					if (d > r || g.Locked(gx, gz) || w <= 0)
					{
						continue;
					}
					env.Vars["x"] = x;
					// North is up the grid.
					env.Vars["z"] = z;
					env.Vars["d"] = d;
					env.Vars["r"] = r;
					env.Vars["h"] = h;
					double v;
					try
					{
						v = f(env);
					}
					catch (Exception)
					{
						v = double.NaN;
					}
					if (!double.IsFinite(v))
					{
						bad++;
						continue;
					}
					if (v == 0)
					{
						continue;
					}
					clamped |= g.SetHeight(p, g.HeightOf(p) + (float)v * w);
					touched.Add(p);
				}
			}
			touchedCount = touched.Count;
			if (touched.Count > 0)
			{
				Touch((x0 - 1, z0 - 1, x1 + 1, z1 + 1));
				var zones = g.ZonesOf(touched);
				Record(label, start, touched, zones);
				Send(zones);
			}
		}
		Changed?.Invoke();
		return (touchedCount, clamped, bad);
	}

	// The values of these points only.
	private static Ground.State Pick(Ground.State all, int[] pts) => new(
		pts.Select(p => all.Level[p]).ToArray(), pts.Select(p => all.Smooth[p]).ToArray(), pts.Select(p => all.Mod[p]).ToArray(),
		pts.SelectMany(p => new[] { all.Paint[p * 4], all.Paint[p * 4 + 1], all.Paint[p * 4 + 2], all.Paint[p * 4 + 3] }).ToArray(), pts.Select(p => all.PMod[p]).ToArray());

	private void Record(string label, Ground.State start, IEnumerable<int> touched, List<(int X, int Z)> zones)
	{
		int[] pts = touched.Distinct().Order().ToArray();
		var now = Ground.Snapshot();
		_undo.Add(new Change(label, pts, Pick(start, pts), Pick(now, pts), zones));
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
			foreach (var (r, b, a) in c.Resets)
			{
				Edits.SetReset(r, after ? a : b);
			}
			if (c.RevertOf != null)
			{
				c.RevertOf.Removed = after;
			}
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
		var live = moves.Where(m => !Scene.Things[m.Index].Gone).ToList();
		var adds = live.Select(m =>
		{
			var t = Scene.Things[m.Index];
			// A copy of an object of this session keeps what that one was copied from.
			var from = t.Id < 0 ? Edits.FindAdded(t.Id) : null;
			return (new NewObject(0, t.Prefab, m.Position, m.Rotation, t.Scale, t.Id < 0 ? from?.SourceId : t.Id, t.Id < 0 && (from?.Fresh ?? true), from?.Raw), t.Piece);
		}).ToList();
		return Commit(label ?? $"Moved {live.Count}", null, live.Select(m => m.Index).ToList(), adds);
	}

	// One undo step that may change the ground (ground: as EditGround), take things away (remove:
	// indices) and add new objects (add: their ids are given here; Piece: a player-built piece; read after
	// the ground step, which may fill it). Returns the indices of the things added.
	public List<int> Commit(string label, Func<Ground, (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect)>? ground,
		IReadOnlyCollection<int> remove, IReadOnlyList<(NewObject Object, bool Piece)> add, (ZoneReset Reset, bool Before, bool After)[]? resets = null)
	{
		var list = Scene.Things;
		var added = new List<NewObject>();
		var indices = new List<int>();
		var changes = new List<(int, bool, bool)>();
		int[] points = Array.Empty<int>();
		Ground.State before = NoPoints, after = NoPoints;
		var zones = new List<(int X, int Z)>();
		lock (_lock)
		{
			if (ground != null)
			{
				var start = Ground.Snapshot();
				var (touched, rect) = ground(Ground);
				if (touched.Count > 0)
				{
					Touch(rect);
					zones = Ground.ZonesOf(touched);
					points = touched.Distinct().Order().ToArray();
					(before, after) = (Pick(start, points), Pick(Ground.Snapshot(), points));
					Send(zones);
				}
			}
		}
		lock (list)
		{
			foreach (int i in remove.Distinct())
			{
				if (!list[i].Gone)
				{
					changes.Add((i, false, true));
				}
			}
			foreach (var (o, piece) in add)
			{
				var n = o with { Id = Scene.Owner?.NextId() ?? _nextId-- };
				added.Add(n);
				list.Add(new WorldScene.Thing(n.Id, n.Prefab, n.Position, n.Rotation, n.Scale, piece));
				indices.Add(list.Count - 1);
				changes.Add((list.Count - 1, true, false));
			}
		}
		if (added.Count > 0)
		{
			Edits.AddObjects(added);
		}
		SetGone(changes.Where(c => c.Item3).Select(c => (c.Item1, true)));
		foreach (var (r, _, on) in resets ?? Array.Empty<(ZoneReset, bool, bool)>())
		{
			Edits.SetReset(r, on);
		}
		if (points.Length == 0 && changes.Count == 0 && (resets?.Length ?? 0) == 0)
		{
			return indices;
		}
		AddChange(new Change(label, points, before, after, zones) { Things = changes.ToArray(), Resets = resets ?? Array.Empty<(ZoneReset, bool, bool)>() });
		if (changes.Count > 0)
		{
			ThingsChanged?.Invoke(changes.Select(c => c.Item1).ToList());
		}
		Changed?.Invoke();
		return indices;
	}

	// The zones marked to be given back to the world generator (to draw them).
	public List<ZoneReset> Resets => Edits.Resets;

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
			if (Scene.Owner is { } owner)
			{
				var o = owner.Save();
				result = o.Saved!;
			}
			else
			{
				var changed = Edits.All().Where(e => e.Changed).ToList();
				result = WorldWriter.Save(Scene.World, changed, Edits.Deleted, Edits.Added, Edits.Resets);
				if (result.Saved)
				{
					Edits.ResetFrom(WorldSave.Load(Scene.World.Directory));
				}
			}
			if (result.Saved)
			{
				Reread(Scene.Owner?.World ?? WorldSave.Load(Scene.World.Directory));
			}
		}
		if (result.Saved)
		{
			ThingsReset?.Invoke();
		}
		Changed?.Invoke();
		return result;
	}

	// Live: applies everything to the running game. After zone resets the world is read again from the
	// game (like a save); otherwise the history stays.
	public async Task<WorldSession.Outcome> ApplyLive()
	{
		var owner = Scene.Owner!;
		var o = await owner.ApplyLive();
		lock (_lock)
		{
			if (o.Reloaded)
			{
				Reread(owner.World);
			}
			else
			{
				Ground.TakeEdits(Edits);
			}
		}
		if (o.Reloaded)
		{
			ThingsReset?.Invoke();
		}
		Changed?.Invoke();
		return o;
	}

	// The world was read again (saved, reloaded): the ground and the objects from it, no history.
	private void Reread(WorldSave fresh)
	{
		Scene.World = fresh;
		Ground.TakeEdits(Edits);
		_undo.Clear();
		_redo.Clear();
		// Saving gives objects new ids: read them again.
		var things = WorldScene.ReadThings(fresh, Scene.X0, Scene.Z0, Scene.Size, Edits.Deleted, Edits.Added);
		lock (Scene.Things)
		{
			Scene.Things.Clear();
			Scene.Things.AddRange(things);
		}
		_nextId = -1;
	}
}
