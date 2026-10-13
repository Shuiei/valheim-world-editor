using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;

namespace TerrainEditor.Desktop;

/// <summary>
/// Editing a scene: brush strokes on the ground, deleting and moving objects, undo and redo, what is waiting to be
/// saved, and saving. Edits go to the EditStore and are written by WorldWriter, the same code the web editor's session
/// uses (/api/zones, /api/save). Strokes run on the drawing thread, the buttons on the window's: everything here takes
/// the lock. Objects are changed on the window's thread only.
/// </summary>
public sealed class EditSession
{
    /// <summary>
    /// The open area being edited.
    /// </summary>
    public WorldScene Scene { get; }
    /// <summary>
    /// The area's ground grid the brushes and tools change.
    /// </summary>
    public Ground Ground { get; }
    /// <summary>
    /// Where the edits wait to be saved or applied live (zones, deleted and added objects, zone resets).
    /// </summary>
    public EditStore Edits { get; }
    /// <summary>
    /// The window shares its brush settings with the session.
    /// </summary>
    public Brush Brush { get; set; } = new();
    /// <summary>
    /// The window's Mask too: which ground the brushes and tools may change.
    /// </summary>
    public Mask Mask { get; set; } = new();
    /// <summary>
    /// The mask of the stroke going on, judged as the ground was when it started (null when off or between strokes).
    /// </summary>
    private Func<int, float>? _strokeMask;

    /// <summary>
    /// The mask over the ground as it is now (null when off).
    /// </summary>
    /// <returns>How much each grid point (by index) may change, 0 to 1; null when the mask is off.</returns>
    public Func<int, float>? MaskNow() => Mask.For(Ground, Scene.Biomes);
    /// <summary>
    /// Guards the ground, the history and the stroke: strokes run on the drawing thread, the buttons on the window's.
    /// </summary>
    private readonly Lock _lock = new();

    /// <summary>
    /// A change that can be undone: the ground points it changed, before and after, and the things it took away or
    /// brought.
    /// </summary>
    /// <param name="Label">The name shown in the history and on Undo.</param>
    /// <param name="Points">The ground grid points it changed (indices, sorted).</param>
    /// <param name="Before">Those points' values before it, in the same order.</param>
    /// <param name="After">Those points' values after it.</param>
    /// <param name="Zones">The zones of the area it touched (sent to the edit store on undo and redo).</param>
    public sealed record Change(string Label, int[] Points, Ground.State Before, Ground.State After, List<(int X, int Z)> Zones)
    {
        /// <summary>
        /// The things it took away or brought: index into the scene's things, gone before and after.
        /// </summary>
        public (int Index, bool Before, bool After)[] Things { get; init; } = [];
        /// <summary>
        /// Zones marked for reset (on) or not.
        /// </summary>
        public (ZoneReset Reset, bool Before, bool After)[] Resets { get; init; } = [];
        /// <summary>
        /// When it was made (local time).
        /// </summary>
        public DateTime Time { get; init; } = DateTime.Now;
        /// <summary>
        /// Taken out (History panel: Remove).
        /// </summary>
        public bool Removed { get; set; }
        /// <summary>
        /// For a removal, the change it takes out; null otherwise.
        /// </summary>
        public Change? RevertOf { get; init; }
        /// <summary>
        /// Live: already sent to the running game (Apply live); still undoable, which makes a new change.
        /// </summary>
        public bool Applied { get; set; }
        /// <summary>
        /// From an earlier session of the editor (HistoryFile): the world may have changed since.
        /// </summary>
        public bool Earlier { get; init; }

        /// <summary>
        /// A short line of what it did, like the web editor's history: "3 zones · 2 new · 1 removed · 1 zone reset".
        /// </summary>
        public string Describe()
        {
            var parts = new List<string>();
            if (Zones.Count > 0)
            {
                parts.Add($"{Zones.Count} zone{(Zones.Count > 1 ? "s" : "")}");
            }

            int added = Things.Count(t => t.Before && !t.After), removed = Things.Count(t => !t.Before && t.After);
            if (added > 0)
            {
                parts.Add($"{added} new");
            }

            if (removed > 0)
            {
                parts.Add($"{removed} removed");
            }

            if (Resets.Length > 0)
            {
                parts.Add($"{Resets.Length} zone reset");
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// The undo list, oldest first (the next undo last).
    /// </summary>
    public IReadOnlyList<Change> UndoList { get { lock (_lock) { return [.. _undo]; } } }
    /// <summary>
    /// What was undone, the next redo last.
    /// </summary>
    public IReadOnlyList<Change> RedoList { get { lock (_lock) { return [.. _redo]; } } }

    /// <summary>
    /// Undoes every change made after this one.
    /// </summary>
    public void BackTo(Change c)
    {
        while (UndoList.Count > 0 && UndoList[^1] != c && Undo())
        {
        }
    }

    /// <summary>
    /// Redoes up to and including this one.
    /// </summary>
    public void ForwardTo(Change c)
    {
        while (RedoList.Count > 0 && (UndoList.Count == 0 || UndoList[^1] != c) && Redo())
        {
        }
    }

    /// <summary>
    /// Takes out one change and keeps everything done after it: its height change is taken off the points it touched
    /// (later edits there stay), paint goes back where nothing repainted it since, its objects and zone resets go back.
    /// The removal is itself a change that can be undone.
    /// </summary>
    /// <returns>A message for the status line ("" during a stroke, when nothing was done).</returns>
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
                g.Lift[p] = Math.Clamp(g.Lift[p] + c.Before.Lift[k] - c.After.Lift[k], -EditStore.MaxLift, EditStore.MaxLift);
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
            Notify(removal.Things.Select(t => t.Index));
        }
        Edited();
        return $"Removed “{c.Label}”; everything else is kept. Ctrl+Z brings it back.";
    }
    /// <summary>
    /// The ground state of a change that touched no ground points.
    /// </summary>
    private static readonly Ground.State NoPoints = new([], [], [], [], [], []);
    /// <summary>
    /// The history: what can be undone (the next undo last) and what can be redone (the next redo last).
    /// </summary>
    private readonly List<Change> _undo = [], _redo = [];
    /// <summary>
    /// How many steps the undo list keeps; the oldest go first.
    /// </summary>
    public const int HistoryLength = 200;

    /// <summary>
    /// The brush stroke going on; null between strokes (during one, undo and redo do nothing and saving throws).
    /// </summary>
    private Stroke? _stroke;
    /// <summary>
    /// The grid rectangle changed since the view last took it (to send to the graphics card).
    /// </summary>
    private (int X0, int Z0, int X1, int Z1)? _dirty;
    /// <summary>
    /// Raised after anything here changed (an edit, a save, an apply): what is pending and the undo buttons may differ.
    /// </summary>
    public event Action? Changed;
    /// <summary>
    /// A change made by the user (a stroke, a tool, undo, redo, a change taken out): not a save or an apply. Live
    /// auto-apply sends these to the game.
    /// </summary>
    public event Action? EditMade;

    /// <summary>
    /// Raises Changed and EditMade after an edit by the user.
    /// </summary>
    private void Edited()
    {
        Changed?.Invoke();
        EditMade?.Invoke();
    }
    /// <summary>
    /// Things that appeared, went or came back (indices into the scene's things).
    /// </summary>
    public event Action<IReadOnlyList<int>>? ThingsChanged;
    /// <summary>
    /// After a save or an apply that read the world again, every thing is read again: the scene's things are a new
    /// list.
    /// </summary>
    public event Action? ThingsReset;
    /// <summary>
    /// New objects were added (placed, pasted, copied, restored...): their indices in Scene.Things.
    /// </summary>
    public event Action<IReadOnlyList<int>>? ThingsAdded;
    /// <summary>
    /// Ids for objects added in this session: negative, like the web editor's.
    /// </summary>
    private int _nextId = -1;

    /// <summary>
    /// A session over an open area, its ground and the edit store its edits go to.
    /// </summary>
    public EditSession(WorldScene scene, Ground ground, EditStore edits)
    {
        Scene = scene;
        Ground = ground;
        Edits = edits;
    }

    /// <summary>
    /// Whether there is a step to undo.
    /// </summary>
    public bool CanUndo { get { lock (_lock) { return _undo.Count > 0; } } }
    /// <summary>
    /// Whether there is a step to redo.
    /// </summary>
    public bool CanRedo { get { lock (_lock) { return _redo.Count > 0; } } }
    /// <summary>
    /// The label of the next undo; null when there is none.
    /// </summary>
    public string? UndoLabel { get { lock (_lock) { return _undo.Count > 0 ? _undo[^1].Label : null; } } }
    /// <summary>
    /// The label of the next redo; null when there is none.
    /// </summary>
    public string? RedoLabel { get { lock (_lock) { return _redo.Count > 0 ? _redo[^1].Label : null; } } }
    /// <summary>
    /// Whether a brush stroke is going on.
    /// </summary>
    public bool Stroking { get { lock (_lock) { return _stroke != null; } } }

    /// <summary>
    /// What is waiting to be saved (or applied live), like the web editor's line under the Save button: the open
    /// world's counts when there is one, else the edit store's.
    /// </summary>
    public (int Zones, int Deleted, int Added, int Resets) Pending => Scene.Owner?.Pending ?? (Edits.ChangedZoneCount, Edits.DeletedCount, Edits.AddedCount, Edits.ResetCount);
    /// <summary>
    /// Whether the area belongs to a world followed live in the running game (Apply live rather than Save).
    /// </summary>
    public bool IsLive => Scene.Owner?.IsLive == true;

    /// <summary>
    /// Pending as a line for the window, e.g. "Unsaved: 3 zones, 1 deleted" or "All saved" ("Not applied" and "All
    /// applied" when live).
    /// </summary>
    public string PendingText
    {
        get
        {
            var (z, d, a, r) = Pending;
            var parts = new[] { z > 0 ? $"{z} zone{(z > 1 ? "s" : "")}" : "", d > 0 ? $"{d} deleted" : "", a > 0 ? $"{a} added" : "", r > 0 ? $"{r} reset" : "" }.Where(p => p != "").ToList();
            return parts.Count > 0 ? (IsLive ? "Not applied: " : "Unsaved: ") + string.Join(", ", parts) : IsLive ? "All applied" : "All saved";
        }
    }

    /// <summary>
    /// The brush goes down at grid point (cx, cz). Flatten with TargetFromClick takes its target height from the ground
    /// there.
    /// </summary>
    public void BeginStroke(BrushTool tool, float cx, float cz)
    {
        Interlocked.Increment(ref _generation);
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

    /// <summary>
    /// One frame of the stroke, the brush at (cx, cz), dt seconds after the last.
    /// </summary>
    /// <returns>False when no stroke is going on or the step changed nothing.</returns>
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

    /// <summary>
    /// The brush comes up: the zones it touched go to the edit store and the stroke to the history.
    /// </summary>
    /// <returns>A message for the status line ("" when there is nothing to say): the game's ±8 m limit reached, or the
    /// Mask leaving out all the ground.</returns>
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
        Edited();
        return message;
    }

    /// <summary>
    /// A change of the ground made all at once (a path, an area action): change gets the ground and returns the points
    /// it changed and the grid rectangle around them. One undo step.
    /// </summary>
    /// <returns>The ground points changed (none: no undo step).</returns>
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
        Edited();
        return touched;
    }

    /// <summary>
    /// Natural objects (trees, rocks, ore, bushes, pickables): what new ground buries or leaves floating.
    /// </summary>
    internal static bool Natural(WorldScene.Thing t) => !t.Piece && !t.Tamed
        && ObjectKinds.Of(Terrain.PrefabCatalog.NameOf(t.Prefab), false) is ObjectKind.Trees or ObjectKind.Rocks or ObjectKind.Ore or ObjectKind.Bushes or ObjectKind.Pickables;

    /// <summary>
    /// Puts a mountain into the ground around grid point (cx, cz) (Mountain tool), past the game's ±8 m (saving turns
    /// it into ground discs). One undo step.
    /// </summary>
    /// <param name="cx">The middle, a grid x.</param>
    /// <param name="cz">The middle, a grid z.</param>
    /// <param name="m">The mountain's shape.</param>
    /// <param name="clear">Whether the natural objects where the ground rises more than Uplift.ClearHeight go (saving
    /// would take them away anyway).</param>
    /// <param name="label">The history's name for the step.</param>
    /// <returns>How many ground points changed and how many objects were cleared; null when the mountain does not fit
    /// in the open area.</returns>
    public (int Touched, int Cleared)? Mountain(float cx, float cz, MountainSpec m, bool clear, string label)
    {
        var g = Ground;
        float reach = m.Reach;
        if (cx - reach < 1 || cz - reach < 1 || cx + reach > g.W - 2 || cz + reach > g.H - 2)
        {
            return null;
        }
        var shape = Desktop.Mountain.Shape(m);
        var mask = MaskNow();
        float ox = Scene.X0 * 64 - 32, oz = Scene.Z0 * 64 - 32;
        var remove = new List<int>();
        if (clear)
        {
            lock (Scene.Things)
            {
                for (int i = 0; i < Scene.Things.Count; i++)
                {
                    var t = Scene.Things[i];
                    float gx = t.Position.X - ox, gz = t.Position.Z - oz;
                    if (!t.Gone && Natural(t) && MathF.Abs(gx - cx) <= reach && MathF.Abs(gz - cz) <= reach
                        && shape(gx - cx, gz - cz) * (mask?.Invoke((int)MathF.Round(gz) * g.W + (int)MathF.Round(gx)) ?? 1) > Uplift.ClearHeight)
                    {
                        remove.Add(i);
                    }
                }
            }
        }
        int touchedCount = 0;
        Commit(label, gr =>
        {
            var touched = new List<int>();
            bool was = gr.NoLimit;
            gr.NoLimit = true;
            int x0 = (int)MathF.Floor(cx - reach), x1 = (int)MathF.Ceiling(cx + reach), z0 = (int)MathF.Floor(cz - reach), z1 = (int)MathF.Ceiling(cz + reach);
            for (int gz = z0; gz <= z1; gz++)
            {
                for (int gx = x0; gx <= x1; gx++)
                {
                    int p = gz * gr.W + gx;
                    float w = mask?.Invoke(p) ?? 1;
                    if (gr.Locked(gx, gz) || w <= 0)
                    {
                        continue;
                    }
                    float v = shape(gx - cx, gz - cz) * w;
                    if (v == 0)
                    {
                        continue;
                    }
                    gr.SetHeight(p, gr.HeightOf(p) + v);
                    touched.Add(p);
                }
            }
            gr.NoLimit = was;
            touchedCount = touched.Count;
            return (touched, (x0 - 1, z0 - 1, x1 + 1, z1 + 1));
        }, remove, []);
        return (touchedCount, remove.Count);
    }

    /// <summary>
    /// Puts a shape into the ground around grid point (cx, cz) (Shape tool). One undo step.
    /// </summary>
    /// <param name="cx">The middle, a grid x.</param>
    /// <param name="cz">The middle, a grid z.</param>
    /// <param name="f">The formula: the metres to add at each point within the radius (x, z metres east and north of
    /// the middle, d the distance, r the radius, h the height, n(x, z) the brushes' noise).</param>
    /// <param name="r">The radius in metres.</param>
    /// <param name="h">The height, h in the formula.</param>
    /// <param name="label">The history's name for the step.</param>
    /// <returns>How many points changed, whether any reached the game's ±8 m limit, and how many points the formula
    /// gave no number for (they are left as they were).</returns>
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
        Edited();
        return (touchedCount, clamped, bad);
    }

    /// <summary>
    /// The values of these points only, in their order.
    /// </summary>
    private static Ground.State Pick(Ground.State all, int[] pts) => new(
        [.. pts.Select(p => all.Level[p])], [.. pts.Select(p => all.Smooth[p])], [.. pts.Select(p => all.Mod[p])],
        [.. pts.SelectMany(p => new[] { all.Paint[p * 4], all.Paint[p * 4 + 1], all.Paint[p * 4 + 2], all.Paint[p * 4 + 3] })], [.. pts.Select(p => all.PMod[p])],
        [.. pts.Select(p => all.Lift[p])]);

    /// <summary>
    /// Puts a ground change on the undo list (its points' values from start to now) and clears the redo list. Called
    /// inside the lock.
    /// </summary>
    private void Record(string label, Ground.State start, IEnumerable<int> touched, List<(int X, int Z)> zones)
    {
        Interlocked.Increment(ref _generation);
        int[] pts = [.. touched.Distinct().Order()];
        var now = Ground.Snapshot();
        _undo.Add(new Change(label, pts, Pick(start, pts), Pick(now, pts), zones));
        if (_undo.Count > HistoryLength)
        {
            _undo.RemoveAt(0);
        }
        _redo.Clear();
    }

    /// <summary>
    /// Undoes the last step.
    /// </summary>
    /// <returns>False during a stroke or when there is nothing to undo.</returns>
    public bool Undo() => Step(_undo, _redo, after: false);

    /// <summary>
    /// The steps at the end of the history not saved (offline) or applied (live) yet.
    /// </summary>
    public int UnappliedSteps
    {
        get
        {
            lock (_lock)
            {
                int n = 0;
                for (int i = _undo.Count - 1; i >= 0 && !_undo[i].Applied; i--)
                {
                    n++;
                }
                return n;
            }
        }
    }

    /// <summary>
    /// The editor's Discard: undoes those steps, newest first, and drops what could be redone; the rest of the history
    /// stays.
    /// </summary>
    /// <returns>How many were undone (none during a stroke).</returns>
    public int UndoUnapplied()
    {
        int n = 0;
        while (true)
        {
            lock (_lock)
            {
                if (_stroke != null || _undo.Count == 0 || _undo[^1].Applied)
                {
                    break;
                }
            }
            if (!Undo())
            {
                break;
            }
            n++;
        }
        lock (_lock)
        {
            if (_stroke == null)
            {
                _redo.Clear();
            }
        }
        Changed?.Invoke();
        return n;
    }

    /// <summary>
    /// Redoes the last undone step.
    /// </summary>
    /// <returns>False during a stroke or when there is nothing to redo.</returns>
    public bool Redo() => Step(_redo, _undo, after: true);

    /// <summary>
    /// Counts every change to the area (a step made, undone or redone, the area read again): what was worked out from
    /// it earlier (a script's run) is out of date once it moved.
    /// </summary>
    public int Generation => Volatile.Read(ref _generation);
    /// <summary>
    /// Backs Generation; changed with Interlocked.
    /// </summary>
    private int _generation;

    /// <summary>
    /// Undo or redo: moves the last change of one list to the other and puts its ground points, things and zone resets
    /// as they were before it (after: false) or after it (after: true).
    /// </summary>
    /// <returns>False during a stroke or when the list is empty.</returns>
    private bool Step(List<Change> from, List<Change> to, bool after)
    {
        Interlocked.Increment(ref _generation);
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
                Ground.Lift[p] = v.Lift[k];
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
            c.RevertOf?.Removed = after;
        }
        if (c.Things.Length > 0)
        {
            Notify(c.Things.Select(t => t.Index));
        }
        Edited();
        return true;
    }

    /// <summary>
    /// Takes things away or brings them back, in the scene and in the edit store (an object added in this session goes
    /// to its trash, so it can come back).
    /// </summary>
    private void SetGone(IEnumerable<(int Index, bool Gone)> things)
    {
        var list = Scene.Things;
        lock (list)
        {
            foreach (var (i, gone) in things)
            {
                // Removed in the game since: neither undo nor redo touches it (it is not there to bring back
                // or to delete; a deletion of it would remove another object under its ZDOID).
                if (list[i].Id >= 0 && Scene.World.Vanished.Contains(list[i].Id))
                {
                    continue;
                }
                list[i] = list[i] with { Gone = gone };
                Edits.SetDeleted([list[i].Id], gone);
            }
        }
    }

    /// <summary>
    /// Puts a change on the undo list (dropping the oldest past HistoryLength) and clears the redo list.
    /// </summary>
    private void AddChange(Change c)
    {
        Interlocked.Increment(ref _generation);
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

    /// <summary>
    /// Removes things from the world (on the next save) as one undo step; those already gone are skipped. A
    /// ValheimRAFT vehicle's root takes its parts with it.
    /// </summary>
    /// <param name="indices">Indices into the scene's things.</param>
    public void Delete(IReadOnlyCollection<int> indices)
    {
        int[] live = [.. WithVehicles(indices, anyPart: false).Indices.Where(i => !Scene.Things[i].Gone).Distinct()];
        if (live.Length == 0)
        {
            return;
        }
        SetGone(live.Select(i => (i, true)));
        AddChange(new Change($"Deleted {live.Length}", [], NoPoints, NoPoints, []) { Things = [.. live.Select(i => (i, false, true))] });
        Notify(live);
        Edited();
    }

    /// <summary>
    /// Moves (and turns) things as one undo step: each is replaced by a copy at its new place that keeps its own data
    /// (chest contents, builder...), like the web editor's moves. Rotations are Euler angles in degrees.
    /// </summary>
    /// <returns>The copies' indices.</returns>
    public List<int> Move(IReadOnlyList<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation)> moves, string? label = null) =>
        Transform([.. moves.Select(m => (m.Index, m.Position, m.Rotation, Scene.Things[m.Index].Scale))], label);

    /// <summary>
    /// Moves, turns and sizes things as one undo step, as Move does, each with its own scale (zero: the prefab's own).
    /// A part of a ValheimRAFT vehicle never moves alone: its whole vehicle moves (see WholeVehicleMoves).
    /// </summary>
    /// <returns>
    /// The copies' indices, in the order of the things not gone; then those of the vehicles' other parts moved with them.
    /// </returns>
    public List<int> Transform(IReadOnlyList<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation, float Scale)> moves, string? label = null)
    {
        var live = WholeVehicleMoves([.. moves.Where(m => !Scene.Things[m.Index].Gone)]);
        var adds = live.Select(m => MovedCopy(m.Index, m.Position, m.Rotation, m.Scale)).ToList();
        return Commit(label ?? $"Moved {live.Count}", null, [.. live.Select(m => m.Index)], adds);
    }

    /// <summary>
    /// The copy that stands for a thing moved, turned or sized (to Commit in place of it): it keeps the thing's own
    /// data (chest contents, builder…) and, for an object added in this session, what that was copied from and its
    /// values (the Workshop's objects and generated dungeons carry theirs there).
    /// </summary>
    /// <param name="index">The thing, an index into the scene's things.</param>
    /// <param name="position">Its new place.</param>
    /// <param name="rotation">Its new turn, Euler angles in degrees.</param>
    /// <param name="scale">Its new size (zero: the prefab's own).</param>
    internal (NewObject Object, bool Piece) MovedCopy(int index, System.Numerics.Vector3 position, System.Numerics.Vector3 rotation, float scale)
    {
        var t = Scene.Things[index];
        var from = t.Id < 0 ? Edits.FindAdded(t.Id) : null;
        return (new NewObject(0, t.Prefab, position, rotation, scale, t.Id < 0 ? from?.SourceId : t.Id, t.Id < 0 && (from?.Fresh ?? true), from?.Raw, from?.Data), t.Piece);
    }

    // ---- ValheimRAFT's vehicles: a root and its parts, stored at the root and built by the mod from their offsets.

    /// <summary>The vehicles (ValheimRAFT) as they are now: the world's objects and this session's.</summary>
    public VehicleIndex ReadVehicles()
    {
        var world = Scene.World;
        lock (world.Sync)
        {
            return VehicleIndex.Of(world, Edits.Deleted, Edits.Added);
        }
    }

    /// <summary>The scene's things not gone, by object id (the first one when two have it).</summary>
    private Dictionary<int, int> ShownById()
    {
        var at = new Dictionary<int, int>();
        var list = Scene.Things;
        lock (list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].Gone)
                {
                    at.TryAdd(list[i].Id, i);
                }
            }
        }
        return at;
    }

    /// <summary>
    /// Things with the rest of the ValheimRAFT vehicles they belong to: each vehicle's parts in the area come after the
    /// things given (which keep their order).
    /// </summary>
    /// <param name="indices">Indices into the scene's things.</param>
    /// <param name="anyPart">Any part brings its whole vehicle (the Select tool); false: only a root brings its parts.</param>
    /// <returns>The things, which of them are vehicles' parts (roots too), and how many vehicles they hold.</returns>
    public (List<int> Indices, HashSet<int> Parts, int Vehicles) WithVehicles(IEnumerable<int> indices, bool anyPart = true)
    {
        var list = indices.ToList();
        var parts = new HashSet<int>();
        var vehicles = ReadVehicles();
        if (vehicles.IsEmpty)
        {
            return (list, parts, 0);
        }
        var things = Scene.Things;
        var at = ShownById();
        var have = list.ToHashSet();
        var roots = new HashSet<int>();
        foreach (int i in list.ToList())
        {
            if (i < 0 || i >= things.Count || vehicles.Get(things[i].Id) == null)
            {
                continue;
            }
            int id = things[i].Id;
            var group = vehicles.Group(id);
            if (group.Count < 2 || !anyPart && group[0] != id || !roots.Add(group[0]))
            {
                continue;
            }
            parts.Add(i);
            foreach (int part in group)
            {
                if (at.TryGetValue(part, out int k))
                {
                    parts.Add(k);
                    if (have.Add(k))
                    {
                        list.Add(k);
                    }
                }
            }
        }
        return (list, parts, roots.Count);
    }

    /// <summary>
    /// Moves of things made moves of whole ValheimRAFT vehicles: a vehicle's root goes where its move puts it (or, when
    /// only parts of it were moved, where the first of them takes it along), and each of its parts is stored at the
    /// root's new place with its own turn as it was, its offset unchanged, as the game saves them: the mod builds the
    /// vehicle there again. Other moves are left as they are; the ones given come first, in their order.
    /// </summary>
    /// <param name="moves">The moves (index into the scene's things, new place, turn and scale).</param>
    private List<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation, float Scale)> WholeVehicleMoves(
        List<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation, float Scale)> moves)
    {
        var vehicles = ReadVehicles();
        if (vehicles.IsEmpty)
        {
            return moves;
        }
        var things = Scene.Things;
        var at = ShownById();
        var result = new List<(int Index, System.Numerics.Vector3 Position, System.Numerics.Vector3 Rotation, float Scale)>(moves);
        var slot = new Dictionary<int, int>();
        for (int k = 0; k < result.Count; k++)
        {
            slot.TryAdd(result[k].Index, k);
        }
        void Set(int index, System.Numerics.Vector3 position, System.Numerics.Vector3 rotation, float scale)
        {
            if (slot.TryGetValue(index, out int k))
            {
                result[k] = (index, position, rotation, scale);
            }
            else
            {
                slot[index] = result.Count;
                result.Add((index, position, rotation, scale));
            }
        }
        var done = new HashSet<int>();
        foreach (var m in moves)
        {
            var t = things[m.Index];
            if (vehicles.Get(t.Id) == null || vehicles.Group(t.Id) is not { Count: > 1 } group || !done.Add(group[0])
                || !at.TryGetValue(group[0], out int rootIndex))
            {
                continue;
            }
            var root = things[rootIndex];
            System.Numerics.Vector3 position, rotation;
            float scale;
            if (slot.TryGetValue(rootIndex, out int k))
            {
                (_, position, rotation, scale) = result[k];
            }
            else
            {
                // The part's own move, from where it is drawn to where it goes, taken by the whole vehicle.
                var turn = BlueprintFormats.FromEuler(m.Rotation) * System.Numerics.Quaternion.Inverse(BlueprintFormats.FromEuler(t.Rotation));
                position = m.Position + System.Numerics.Vector3.Transform(root.Position - t.Position, turn);
                rotation = BlueprintFormats.ToEuler(turn * BlueprintFormats.FromEuler(root.Rotation));
                scale = root.Scale;
            }
            Set(rootIndex, position, rotation, scale);
            foreach (int id in group.Skip(1))
            {
                if (at.TryGetValue(id, out int i))
                {
                    Set(i, position, vehicles.Get(id)!.Rotation, slot.TryGetValue(i, out int j) ? result[j].Scale : things[i].Scale);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// After objects came, went or changed: the parts of ValheimRAFT vehicles drawn where the mod puts them now (their
    /// root or swivel may have moved, gone or come back).
    /// </summary>
    /// <returns>The things placed again, indices into the scene's things.</returns>
    private List<int> PlaceVehicles()
    {
        var moved = new List<int>();
        var vehicles = ReadVehicles();
        if (vehicles.IsEmpty)
        {
            return moved;
        }
        var list = Scene.Things;
        lock (list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var t = list[i];
                var placed = t.Gone ? t : WorldScene.Placed(t, vehicles);
                if (placed != t)
                {
                    list[i] = placed;
                    moved.Add(i);
                }
            }
        }
        return moved;
    }

    /// <summary>Raises ThingsChanged for these things and the vehicles' parts placed again after them.</summary>
    private void Notify(IEnumerable<int> indices)
    {
        int[] all = [.. indices.Concat(PlaceVehicles()).Distinct()];
        if (all.Length > 0)
        {
            ThingsChanged?.Invoke(all);
        }
    }

    /// <summary>
    /// One undo step that may change the ground, take things away, add new objects and mark zones for reset. Nothing
    /// goes on the history when it changes nothing.
    /// </summary>
    /// <param name="label">The history's name for the step.</param>
    /// <param name="ground">As EditGround's change; null leaves the ground alone.</param>
    /// <param name="remove">Things to take away, indices into the scene's things (bad or already gone ones are
    /// skipped).</param>
    /// <param name="add">New objects (their ids are given here; Piece: a player-built piece; read after the ground
    /// step, which may fill it).</param>
    /// <param name="resets">Zones marked for reset or unmarked, with their state before and after.</param>
    /// <returns>The indices of the things added.</returns>
    public List<int> Commit(string label, Func<Ground, (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect)>? ground,
        IReadOnlyCollection<int> remove, IReadOnlyList<(NewObject Object, bool Piece)> add, (ZoneReset Reset, bool Before, bool After)[]? resets = null)
    {
        var list = Scene.Things;
        var added = new List<NewObject>();
        var indices = new List<int>();
        var changes = new List<(int, bool, bool)>();
        int[] points = [];
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
                    points = [.. touched.Distinct().Order()];
                    (before, after) = (Pick(start, points), Pick(Ground.Snapshot(), points));
                    Send(zones);
                }
            }
        }
        lock (list)
        {
            // (Indices from elsewhere, a script's, are checked: a bad one changed the ground with no undo step.)
            foreach (int i in remove.Distinct())
            {
                if (i >= 0 && i < list.Count && !list[i].Gone)
                {
                    changes.Add((i, false, true));
                }
            }
            foreach (var (o, piece) in add)
            {
                var n = o with { Id = Scene.Owner?.NextId() ?? _nextId-- };
                added.Add(n);
                list.Add(new WorldScene.Thing(n.Id, n.Prefab, n.Position, n.Rotation, n.Scale, piece) { Tamed = WorldScene.TamedOf(Scene.World, n) });
                indices.Add(list.Count - 1);
                changes.Add((list.Count - 1, true, false));
            }
        }
        if (added.Count > 0)
        {
            Edits.AddObjects(added);
        }
        SetGone(changes.Where(c => c.Item3).Select(c => (c.Item1, true)));
        foreach (var (r, _, on) in resets ?? [])
        {
            Edits.SetReset(r, on);
        }
        if (points.Length == 0 && changes.Count == 0 && (resets?.Length ?? 0) == 0)
        {
            return indices;
        }
        AddChange(new Change(label, points, before, after, zones) { Things = [.. changes], Resets = resets ?? [] });
        if (changes.Count > 0)
        {
            Notify(changes.Select(c => c.Item1));
        }
        if (indices.Count > 0)
        {
            ThingsAdded?.Invoke(indices);
        }
        Edited();
        return indices;
    }

    /// <summary>
    /// The zones marked to be given back to the world generator (to draw them).
    /// </summary>
    public List<ZoneReset> Resets => Edits.Resets;

    /// <summary>
    /// Marks zones for reset when saving (Reset zones), as one undo step: the generator makes them again, their trees,
    /// rocks, ruins and dungeon entrances gone (and the players' buildings unless kept); with ground, their ground
    /// edits in the area are undone now too.
    /// </summary>
    /// <param name="zones">The zones (world zone column and row).</param>
    /// <param name="keepBuildings">Keep the pieces players built.</param>
    /// <param name="ground">Also undo their ground edits.</param>
    /// <param name="label">The history's name for the step.</param>
    public void MarkResets(IReadOnlyCollection<(int X, int Z)> zones, bool keepBuildings, bool ground, string label)
    {
        var marked = Resets.Select(r => (r.X, r.Z)).ToHashSet();
        var resets = zones.Select(z => (new ZoneReset(z.X, z.Z, keepBuildings, ground), marked.Contains(z), true)).ToArray();
        Func<Ground, (List<int>, (int, int, int, int))>? clearGround = !ground ? null : g =>
        {
            var touched = new List<int>();
            foreach (var (zx, zz) in zones)
            {
                int gx0 = (zx - g.X0) * 64, gz0 = (zz - g.Z0) * 64;
                for (int k = 0; k <= 64; k++)
                {
                    for (int l = 0; l <= 64; l++)
                    {
                        int gx = gx0 + l, gz = gz0 + k, p = gz * g.W + gx;
                        if (gx >= 0 && gz >= 0 && gx < g.W && gz < g.H && (g.Mod[p] != 0 || g.PMod[p] != 0 || g.Lift[p] != 0))
                        {
                            g.Mod[p] = g.PMod[p] = 0;
                            g.Level[p] = g.Smooth[p] = g.Lift[p] = 0;
                            touched.Add(p);
                        }
                    }
                }
            }
            return (touched, (0, 0, g.W - 1, g.H - 1));
        };
        Commit(label, clearGround, [], [], resets);
    }

    /// <summary>Unmarks those of these zones that are marked for reset, as one undo step.</summary>
    /// <returns>How many were unmarked (none: no step).</returns>
    public int UnmarkResets(IEnumerable<(int X, int Z)> zones, string label)
    {
        var marked = Resets.ToDictionary(r => (r.X, r.Z));
        var found = zones.Where(marked.ContainsKey).Distinct().ToList();
        if (found.Count > 0)
        {
            Commit(label, null, [], [], [.. found.Select(z => (marked[z], true, false))]);
        }
        return found.Count;
    }

    /// <summary>
    /// The zones to the edit store, as the web page uploads them.
    /// </summary>
    private void Send(List<(int X, int Z)> zones)
    {
        foreach (var (zx, zz) in zones)
        {
            Edits.Put(Ground.ZoneEdit(zx, zz));
        }
    }

    /// <summary>
    /// The scene's heights, paint mask and limit marks follow the ground over a grid rectangle (clamped to the grid),
    /// which joins the dirty rectangle.
    /// </summary>
    private void Touch((int X0, int Z0, int X1, int Z1) r)
    {
        int x0 = Math.Max(0, r.X0), z0 = Math.Max(0, r.Z0), x1 = Math.Min(Ground.W - 1, r.X1), z1 = Math.Min(Ground.H - 1, r.Z1);
        Scene.Refresh(Ground, x0, z0, x1, z1);
        _dirty = _dirty is { } d ? (Math.Min(d.X0, x0), Math.Min(d.Z0, z0), Math.Max(d.X1, x1), Math.Max(d.Z1, z1)) : (x0, z0, x1, z1);
    }

    /// <summary>
    /// The grid rectangle changed since the last call (null: nothing), for the view to send to the graphics card.
    /// </summary>
    public (int X0, int Z0, int X1, int Z1)? TakeDirty()
    {
        lock (_lock)
        {
            var d = _dirty;
            _dirty = null;
            return d;
        }
    }

    /// <summary>
    /// Writes the changes into the world's files (WorldSession.Save); the history stays, its steps saved (an undo is
    /// then pending, saved by the next save), as with Apply live. After zone resets or No limit ground the area is read
    /// again and the history cleared.
    /// </summary>
    /// <returns>What the writer did.</returns>
    public WorldWriter.Result Save()
    {
        WorldWriter.Result result;
        bool reread;
        lock (_lock)
        {
            if (_stroke != null)
            {
                throw new InvalidOperationException("A stroke is still going on.");
            }
            // Every area opened in the app belongs to an open world (WorldScene.Load), which saves.
            var owner = Scene.Owner ?? throw new InvalidOperationException("This area is not part of an open world, so it cannot be saved.");
            var o = owner.Save();
            result = o.Saved!;
            // Read again only when the world was (zone resets) or No limit ground became ground discs
            // (also when the save then failed): the ground under the area is not the same any more.
            // Otherwise, as after Apply live, the area and its history stay; the steps are saved.
            reread = o.Reloaded || o.Lifted;
            if (reread)
            {
                Reread(owner.World);
            }
            else if (result.Saved)
            {
                Ground.TakeEdits(Edits);
                foreach (var c in _undo)
                {
                    c.Applied = true;
                }
            }
        }
        if (reread)
        {
            ThingsReset?.Invoke();
        }
        Changed?.Invoke();
        return result;
    }

    /// <summary>
    /// Live: applies everything to the running game. After zone resets the world is read again from the game (like a
    /// save), and after No limit ground became ground discs the area is (the ground under it changed); otherwise the
    /// history stays.
    /// </summary>
    /// <param name="confirm">See WorldSession.ApplyLive (asked before writing over ground the game changed).</param>
    public async Task<WorldSession.Outcome> ApplyLive(Func<IReadOnlyList<(int X, int Z)>, Task<bool>>? confirm = null)
    {
        var owner = Scene.Owner!;
        // The steps there are now: a step made while the game answers may not be in what was sent.
        List<Change> before;
        lock (_lock)
        {
            before = [.. _undo];
        }
        var o = await owner.ApplyLive(confirm);
        lock (_lock)
        {
            if (o.Reloaded || o.Lifted)
            {
                Reread(owner.World);
            }
            else
            {
                Ground.TakeEdits(Edits);
                if (o.Done)
                {
                    foreach (var c in before)
                    {
                        c.Applied = true;
                    }
                }
            }
        }
        if (o.Reloaded || o.Lifted)
        {
            ThingsReset?.Invoke();
        }
        Changed?.Invoke();
        return o;
    }

    // ---- The history follows the open world from area to area (like the web editor's, kept by the
    // server): while another area is open it is kept with the world, ground points by world position and
    // objects by id, and fitted to the next area opened.
    /// <summary>
    /// A history kept with the open world while another area is open, to fit into the next area opened (Import), and
    /// what HistoryFile writes.
    /// </summary>
    /// <param name="Undo">The undo list, oldest first.</param>
    /// <param name="Redo">The redo list, the next redo last.</param>
    /// <param name="Ox">The world x in metres of the grid's first point in the area it came from.</param>
    /// <param name="Oz">Its world z in metres.</param>
    /// <param name="W">That area's grid width (ground points are its indices).</param>
    /// <param name="X0">That area's first zone x.</param>
    /// <param name="Z0">That area's first zone z.</param>
    /// <param name="Ids">For each change, the ids of its things, in the order of its Things.</param>
    public sealed record Kept(List<Change> Undo, List<Change> Redo, int Ox, int Oz, int W, int X0, int Z0, Dictionary<Change, int[]> Ids);

    /// <summary>
    /// The history as Kept: ground points by world position and objects by id, to follow the world to the next area.
    /// </summary>
    public Kept Export()
    {
        lock (_lock)
        {
            var ids = new Dictionary<Change, int[]>();
            foreach (var c in _undo.Concat(_redo))
            {
                ids[c] = [.. c.Things.Select(t => Scene.Things[t.Index].Id)];
            }
            return new Kept([.. _undo], [.. _redo], Scene.X0 * 64 - 32, Scene.Z0 * 64 - 32, Ground.W, Scene.X0, Scene.Z0, ids);
        }
    }

    /// <summary>
    /// The kept history fitted to this area: a change whose ground or objects are not all in it (and every change
    /// before it) is left out, as it could not be undone here. Objects it needs that the area does not show (deleted,
    /// or added and undone) come back as gone things.
    /// </summary>
    /// <param name="k">The kept history.</param>
    /// <param name="find">Finds things of the world by id (those this area does not have).</param>
    /// <returns>How many were left out.</returns>
    public int Import(Kept k, Func<IReadOnlyCollection<int>, Dictionary<int, WorldScene.Thing>> find)
    {
        lock (_lock)
        {
            int ox = Scene.X0 * 64 - 32, oz = Scene.Z0 * 64 - 32;
            var index = new Dictionary<int, int>();
            for (int i = 0; i < Scene.Things.Count; i++)
            {
                index.TryAdd(Scene.Things[i].Id, i);
            }
            var missing = k.Ids.Values.SelectMany(v => v).Where(id => !index.ContainsKey(id)).Distinct().ToList();
            var found = missing.Count > 0 ? find(missing) : [];
            float minX = Scene.X0 * 64f - 32f, maxX = (Scene.X0 + Scene.ZonesX - 1) * 64f + 32f, minZ = Scene.Z0 * 64f - 32f, maxZ = (Scene.Z0 + Scene.ZonesZ - 1) * 64f + 32f;
            int? Thing(int id)
            {
                if (index.TryGetValue(id, out int i))
                {
                    return i;
                }
                if (found.TryGetValue(id, out var t) && t.Position.X >= minX && t.Position.X < maxX && t.Position.Z >= minZ && t.Position.Z < maxZ)
                {
                    lock (Scene.Things)
                    {
                        Scene.Things.Add(t);
                        index[id] = Scene.Things.Count - 1;
                    }
                    return Scene.Things.Count - 1;
                }
                return null;
            }
            int[]? Points(int[] pts)
            {
                int[] r = new int[pts.Length];
                for (int n = 0; n < pts.Length; n++)
                {
                    int gx = pts[n] % k.W + k.Ox - ox, gz = pts[n] / k.W + k.Oz - oz;
                    if (gx < 0 || gz < 0 || gx >= Ground.W || gz >= Ground.H || Ground.Locked(gx, gz))
                    {
                        return null;
                    }
                    r[n] = gz * Ground.W + gx;
                }
                return r;
            }
            List<(int X, int Z)>? Zones(List<(int X, int Z)> zones)
            {
                var r = zones.Select(z => (X: z.X + k.X0 - Scene.X0, Z: z.Z + k.Z0 - Scene.Z0)).ToList();
                return r.All(z => z.X >= 0 && z.Z >= 0 && z.X < Scene.ZonesX && z.Z < Scene.ZonesZ) ? r : null;
            }
            var map = new Dictionary<Change, Change>();
            Change? Fit(Change c)
            {
                if (Points(c.Points) is not { } pts || Zones(c.Zones) is not { } zones)
                {
                    return null;
                }
                int[] ids = k.Ids[c];
                var things = new (int, bool, bool)[ids.Length];
                for (int n = 0; n < ids.Length; n++)
                {
                    if (Thing(ids[n]) is not int i)
                    {
                        return null;
                    }
                    things[n] = (i, c.Things[n].Before, c.Things[n].After);
                }
                var f = new Change(c.Label, pts, c.Before, c.After, zones)
                {
                    Things = things,
                    Resets = c.Resets,
                    Time = c.Time,
                    Removed = c.Removed,
                    Applied = c.Applied,
                    Earlier = c.Earlier,
                    RevertOf = c.RevertOf != null && map.TryGetValue(c.RevertOf, out var r) ? r : null,
                };
                map[c] = f;
                return f;
            }
            // Oldest first, so a removal finds the change it took out; then keep the newest that fit.
            var undo = k.Undo.Select(Fit).ToList();
            int start = undo.FindLastIndex(c => c == null) + 1;
            _undo.Clear();
            _undo.AddRange(undo.Skip(start)!);
            // The redo list has the next redo last; fitted oldest first too. One that does not fit is
            // dropped with every change after it (they could only be redone after it).
            var redo = Enumerable.Reverse(k.Redo).Select(Fit).Reverse().ToList();
            int end = redo.FindLastIndex(c => c == null);
            _redo.Clear();
            _redo.AddRange(redo.Skip(end + 1)!);
            return start + end + 1;
        }
    }

    /// <summary>
    /// Live: what the game changed (WorldSession.MergeGame), taken into the area without an undo step: objects removed
    /// in the game go, changed ones are shown as they are now (same thing, same place in the history), new ones come,
    /// and the ground of the zones given is read again. Taking the same changes twice changes nothing (an area opened
    /// meanwhile may already have them). Not during a stroke (false: given again next time).
    /// </summary>
    /// <returns>False during a stroke: given again next time.</returns>
    public bool TakeGameChanges(WorldSession.Followed f)
    {
        var list = Scene.Things;
        var changed = new List<int>();
        bool ground = f.Ground.Count > 0 || f.Merged.Modifiers;
        lock (_lock)
        {
            if (_stroke != null)
            {
                return false;
            }
            var world = Scene.World;
            var gone = f.Merged.Vanished.Concat(f.Ours).ToHashSet();
            var update = f.Merged.Updated.Concat(f.Merged.Added).ToHashSet();
            float minX = Scene.X0 * 64f - 32f, maxX = (Scene.X0 + Scene.ZonesX - 1) * 64f + 32f, minZ = Scene.Z0 * 64f - 32f, maxZ = (Scene.Z0 + Scene.ZonesZ - 1) * 64f + 32f;
            bool Inside(System.Numerics.Vector3 p) => p.X >= minX && p.X < maxX && p.Z >= minZ && p.Z < maxZ;
            // The objects as the world has them now, by id (those changed or new).
            var now = WorldScene.FindThings(world, Edits, update);
            lock (list)
            {
                var at = new Dictionary<int, int>();
                for (int i = 0; i < list.Count; i++)
                {
                    at[list[i].Id] = i;
                }
                foreach (int id in gone)
                {
                    if (at.TryGetValue(id, out int i) && !list[i].Gone)
                    {
                        list[i] = list[i] with { Gone = true };
                        changed.Add(i);
                    }
                }
                foreach (var (id, t) in now)
                {
                    if (at.TryGetValue(id, out int i))
                    {
                        // Changed in the game: shown as it is now (deleted in the editor, it stays so).
                        var next = t with { Gone = list[i].Gone };
                        if (next != list[i])
                        {
                            list[i] = next;
                            changed.Add(i);
                        }
                    }
                    else if (Inside(t.Position) && !t.Gone && t.Prefab != WorldSave.LocationProxyPrefab)
                    {
                        changed.Add(list.Count);
                        list.Add(t);
                    }
                }
            }
            if (ground)
            {
                if (f.Merged.Modifiers && Scene.Owner is { } owner)
                {
                    Scene.Modifiers = owner.Modifiers;
                    Ground.ReadBase(owner.Terrain);
                }
                Ground.TakeEdits(Edits);
                Touch((0, 0, Ground.W - 1, Ground.H - 1));
            }
        }
        if (changed.Count > 0 || ground)
        {
            // What was worked out from the area before (an open inspector, a running script) is out of date.
            Interlocked.Increment(ref _generation);
        }
        if (changed.Count > 0)
        {
            // A vehicle's root moved in the game: its parts are drawn where it is now.
            Notify(changed);
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// The world was read again (saved, reloaded): the ground and the objects from it, no history.
    /// </summary>
    private void Reread(WorldSave fresh)
    {
        Interlocked.Increment(ref _generation);
        Scene.World = fresh;
        // The ground discs may have changed: the generated ground with them, everywhere in the area.
        if (Scene.Owner is { } owner)
        {
            Scene.Modifiers = owner.Modifiers;
            Ground.ReadBase(owner.Terrain);
        }
        Ground.TakeEdits(Edits);
        Touch((0, 0, Ground.W - 1, Ground.H - 1));
        _undo.Clear();
        _redo.Clear();
        // Saving gives objects new ids: read them again.
        var things = WorldScene.ReadThings(fresh, Scene.X0, Scene.Z0, Scene.ZonesX, Scene.ZonesZ, Edits.Deleted, Edits.Added);
        lock (Scene.Things)
        {
            Scene.Things.Clear();
            Scene.Things.AddRange(things);
        }
        _nextId = -1;
    }
}
