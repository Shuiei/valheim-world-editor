using System.Numerics;
using Avalonia;
using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// What is under the pointer (the ground, a grid point, an object) and the selection: picking, its boxes, and
// the object under the pointer outlined.
public sealed partial class GlView
{
    /// <summary>Where a grid point (on the ground, lifted a little) is on the view, or null behind the camera.</summary>
    internal Point? ScreenOfGrid(Vector2 g, float lift = 0.6f)
    {
        var s = Scene;
        var size = _surfaceSize.Width > 0 ? _surfaceSize : Bounds.Size;
        if (s == null || size.Width <= 0)
        {
            return null;
        }
        var q = Vector4.Transform(new Vector4(OnGround(s, g.X, g.Y, lift), 1), _lastViewProj);
        if (q.W <= 0)
        {
            return null;
        }
        return new Point((q.X / q.W + 1) / 2 * size.Width, (1 - q.Y / q.W) / 2 * size.Height);
    }

    /// <summary>The grid point of the ground under a point of the view (null: none).</summary>
    internal Vector2? GridAt(Point at, Size size) => Scene is { } s && GroundAt(s, _lastViewProj, at, size) is { } g ? new Vector2(g.X, g.Z) : null;

    /// <summary>The ground's height at a grid point (0 with no area).</summary>
    internal float GridHeight(Vector2 g)
    {
        var s = Scene;
        return s == null ? 0 : OnGround(s, g.X, g.Y, 0).Y;
    }

    /// <summary>The world point (x east, height, z north) of the ground under a point of the view, or null.</summary>
    internal Vector3? WorldAt(Point at, Size size)
    {
        var s = Scene;
        if (s == null || GroundAt(s, _lastViewProj, at, size) is not { } g)
        {
            return null;
        }
        float wx = s.OriginX + g.X, wz = s.OriginZ + g.Z;
        return new Vector3(wx, OnGround(s, g.X, g.Z, 0).Y, wz);
    }

    /// <summary>The ray from the camera through a point of the view (view space).</summary>
    internal (Vector3 O, Vector3 D) RayAt(Point at, Size size) =>
        Picking.Ray(_lastViewProj, (float)(at.X / size.Width * 2 - 1), (float)(1 - at.Y / size.Height * 2));

    /// <summary>The grid point (fractional) of the ground under a point of the view, or null.</summary>
    private static (float X, float Z)? GroundAt(WorldScene s, Matrix4x4 vp, Point at, Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
        {
            return null;
        }
        var (o, d) = Picking.Ray(vp, (float)(at.X / size.Width * 2 - 1), (float)(1 - at.Y / size.Height * 2));
        if (Picking.HitGround(s, o, d) is not float t)
        {
            return null;
        }
        var p = o + d * t;
        return (p.X + (s.W - 1) / 2f, -p.Z + (s.H - 1) / 2f);
    }

    /// <summary>A world point (x east, height, z north) in view space: from the area's middle, north as -z.</summary>
    /// <param name="s">The area.</param>
    /// <param name="world">The world point.</param>
    private static Vector3 ToView(WorldScene s, Vector3 world) => new(world.X - s.Cx, world.Y, -(world.Z - s.Cz));

    /// <summary>A grid point of the area (x east, z north) in view space: from the area's middle, north as -z.</summary>
    /// <param name="s">The area.</param>
    /// <param name="gx">The grid x.</param>
    /// <param name="gz">The grid z.</param>
    private static (float X, float Z) GridToView(WorldScene s, float gx, float gz) => (gx - (s.W - 1) / 2f, -(gz - (s.H - 1) / 2f));

    /// <summary>A grid point of the area in view space, on the ground there and lifted this many metres.</summary>
    /// <param name="s">The area.</param>
    /// <param name="gx">The grid x.</param>
    /// <param name="gz">The grid z.</param>
    /// <param name="lift">Metres above the ground.</param>
    private static Vector3 OnGround(WorldScene s, float gx, float gz, float lift)
    {
        var (x, z) = GridToView(s, gx, gz);
        return new Vector3(x, Picking.HeightAt(s, x, z) + lift, z);
    }

    /// <summary>The ground's height at a world point.</summary>
    internal float GroundHeight(float wx, float wz)
    {
        var s = Scene;
        return s == null ? 0 : Picking.HeightAt(s, wx - s.Cx, -(wz - s.Cz));
    }

    // ---- Selection: the objects picked (indices into the scene's things), drawn as orange boxes.
    /// <summary>Each thing's box in view space (for picking and the selection), by thing index.</summary>
    private (Vector3 Min, Vector3 Max)[] _bounds = [];

    /// <summary>
    /// What a thing's outline marks (selected, or under the pointer): its box, except for a standing tree,
    /// where it is a column round the root the full height of the tree (a tenth of the crown's width
    /// across, 0.5 to 2 m either way), so the outline shows the trunk, not the whole crown. A log or a
    /// stump (wider than tall) keeps its box.
    /// </summary>
    /// <param name="kind">The thing's View kind.</param>
    /// <param name="box">Its box, in view space.</param>
    /// <param name="root">Where it stands (its position), in view space.</param>
    internal static (Vector3 Min, Vector3 Max) OutlineBox(ObjectKind kind, (Vector3 Min, Vector3 Max) box, Vector3 root)
    {
        if (kind != ObjectKind.Trees)
        {
            return box;
        }
        float w = MathF.Max(box.Max.X - box.Min.X, box.Max.Z - box.Min.Z);
        if (box.Max.Y - box.Min.Y < 0.6f * w)
        {
            return box;
        }
        float r = Math.Clamp(0.1f * w, 0.5f, 2f);
        return (new Vector3(root.X - r, box.Min.Y, root.Z - r), new Vector3(root.X + r, box.Max.Y, root.Z + r));
    }

    /// <summary>A thing's outline box (<see cref="OutlineBox"/>), under the objects lock.</summary>
    private (Vector3 Min, Vector3 Max) OutlineBoxOf(WorldScene s, int i)
    {
        var at = _boundsAt[i];
        return OutlineBox(_kinds[i], _bounds[i], ToView(s, at));
    }
    /// <summary>Whether a thing's box is known (its model read, and it is not gone).</summary>
    private bool[] _known = [];
    /// <summary>Where each thing was when its box was made (a move's preview may be ahead of the boxes).</summary>
    private Vector3[] _boundsAt = [];
    /// <summary>Each thing's View kind, by thing index.</summary>
    private ObjectKind[] _kinds = [];
    /// <summary>The objects picked, as indices into the scene's things.</summary>
    private readonly HashSet<int> _selection = [];
    /// <summary>The camera's view × projection of the last frame, which the mouse is read with.</summary>
    private Matrix4x4 _lastViewProj;
    /// <summary>The selection changed: its boxes are made again at the next frame.</summary>
    private bool _selectionDirty;
    /// <summary>The selection changed: the things now selected.</summary>
    public event Action<IReadOnlyList<WorldScene.Thing>>? SelectionChanged;

    /// <summary>A double click in Select mode.</summary>
    public event Action? InspectAsked;
    /// <summary>A double click on an object in the View tool: the Select tool wanted, the object picked.</summary>
    public event Action? SelectToolAsked;
    /// <summary>The objects picked, as indices into the scene's things (a copy).</summary>
    internal IReadOnlyCollection<int> Selected { get { lock (_selection) { return [.. _selection]; } } }

    /// <summary>
    /// The Workshop's cut: nothing drawn above this height (world y), and the cursor goes through what is
    /// hidden; null: none.
    /// </summary>
    internal float? CutY
    {
        get; set
        {
            field = value;
            Wake();
        }
    }

    /// <summary>
    /// The view's ray through a point, in world space (x east, up, z north), and how far along it the
    /// ground is (null: it does not meet it).
    /// </summary>
    internal (Vector3 O, Vector3 D, float? GroundT)? WorldRay(Point at, Size size)
    {
        var s = Scene;
        if (s == null || size.Width <= 0)
        {
            return null;
        }
        var (o, d) = Picking.Ray(_lastViewProj, (float)(at.X / size.Width * 2 - 1), (float)(1 - at.Y / size.Height * 2));
        d = Vector3.Normalize(d);
        float? g = Picking.HitGround(s, o, d);
        var wo = new Vector3(o.X + s.Cx, o.Y, -o.Z + s.Cz);
        var wd = new Vector3(d.X, d.Y, -d.Z);
        // A dungeon's rooms are the ground there (their floors and walls).
        if (_shown[(int)ObjectKind.Dungeons] && DungeonRooms.Shown(s, Wake) is { Count: > 0 } dungeons && RoomSurfaces.Hit(dungeons, Models, wo, wd, CutY ?? float.MaxValue) is float rt && (g == null || rt < g))
        {
            g = rt;
        }
        return (wo, wd, g);
    }

    /// <summary>
    /// The shown object under a point of the view (null: the ground or nothing is nearer): the nearest one
    /// whose drawn shape the ray meets, unless the ground is hit first (half a metre of slack). The boxes say
    /// which objects to try; then the triangles of each one's model decide, so a click between a tree's
    /// branches finds the chest under them. Cut-out parts (leaves, bushes, grass) count only when nothing
    /// solid is met along the ray; an object without its model yet is its stand-in box.
    /// </summary>
    internal int? ObjectAt(Point at, Size size)
    {
        var s = Scene;
        if (s == null || size.Width <= 0)
        {
            return null;
        }
        var (o, d) = Picking.Ray(_lastViewProj, (float)(at.X / size.Width * 2 - 1), (float)(1 - at.Y / size.Height * 2));
        var boxes = new List<(float T, int I)>();
        for (int i = 0; i < _bounds.Length; i++)
        {
            // Not what the cut hides (whole above it).
            if (!_known[i] || !_shown[(int)_kinds[i]] || CutY is float cut && _bounds[i].Min.Y > cut)
            {
                continue;
            }
            if (Picking.HitBox(o, d, _bounds[i].Min, _bounds[i].Max) is float t)
            {
                boxes.Add((t, i));
            }
        }
        boxes.Sort((a, b) => a.T.CompareTo(b.T));
        float bestSolid = float.MaxValue, bestCut = float.MaxValue;
        int? solid = null, cutOut = null;
        foreach (var (t, i) in boxes)
        {
            if (t > bestSolid)
            {
                // Its box is entered past the nearest solid hit: nothing of it can be nearer.
                break;
            }
            var (so, cu) = MeshHit(s, i, o, d, t);
            if (so is float a && a < bestSolid)
            {
                bestSolid = a;
                solid = i;
            }
            if (cu is float c && c < bestCut)
            {
                bestCut = c;
                cutOut = i;
            }
        }
        int? hit = solid ?? cutOut;
        float best = solid != null ? bestSolid : bestCut;
        if (hit != null && Picking.HitGround(s, o, d, best + 1) is float g && g + 0.5f < best)
        {
            return null;
        }
        return hit;
    }

    /// <summary>
    /// How far along the ray a thing's drawn shape is met: by its solid parts, and by its cut-out parts
    /// (leaves, bushes, grass), null each where the ray misses them. Without its model (the stand-in
    /// box), its box's distance counts as solid. What the Workshop's cut hides (above <see cref="CutY"/>)
    /// is not met, as it is not drawn: a wall crossing the cut is met below it or not at all.
    /// </summary>
    /// <param name="s">The scene.</param>
    /// <param name="i">The thing's index.</param>
    /// <param name="o">The ray's origin (view space).</param>
    /// <param name="d">The ray's direction, normalised.</param>
    /// <param name="box">Where the ray enters the thing's box.</param>
    private (float? Solid, float? CutOut) MeshHit(WorldScene s, int i, Vector3 o, Vector3 d, float box)
    {
        float? cutY = CutY;
        WorldScene.Thing t;
        lock (s.Things)
        {
            if (i >= s.Things.Count)
            {
                return (BoxUnderCut(), null);
            }
            t = _previews.TryGetValue(i, out var p) ? p : s.Things[i];
        }
        // A copy of the group's parts: the drawing thread adds them as the model comes in.
        (Batch Batch, Matrix4x4 Pre)[] batches;
        Vector3 rootScale;
        lock (_groups)
        {
            if (!_groups.TryGetValue(KeyOf(t), out var g) || !g.HasModel || !g.Ready || g.Batches.Count == 0)
            {
                return (BoxUnderCut(), null);
            }
            rootScale = g.RootScale;
            batches = [.. g.Batches];
        }
        var m = Placement(s, t, rootScale);
        float? solid = null, cutOut = null;
        bool any = false;
        foreach (var (b, pre) in batches)
        {
            if (!_pickMeshes.TryGetValue(b.MeshId, out var pm) || b.Sub >= pm.Submeshes.Length)
            {
                continue;
            }
            any = true;
            if (Picking.HitMesh(o, d, pm.Positions, pm.Submeshes[b.Sub], pm.Bounds, pre * m, cutY ?? float.MaxValue) is not float h)
            {
                continue;
            }
            if (b.Material.Cutoff > 0)
            {
                cutOut = cutOut is float c && c < h ? c : h;
            }
            else
            {
                solid = solid is float a && a < h ? a : h;
            }
        }
        return any ? (solid, cutOut) : (BoxUnderCut(), null);

        // The stand-in box: where the ray enters it under the cut (anywhere without one). The cut is not a face of it.
        float? BoxUnderCut()
        {
            if (cutY is not float c)
            {
                return box;
            }
            var (lo, hi) = _bounds[i];
            return Picking.HitBox(o, d, lo, hi, c);
        }
    }

    /// <summary>
    /// A click on an object: it alone, or (add) added or taken out; range (Shift): every shown object in
    /// a row from the last one clicked to this one (w1, Shift + click w3: w1, w2 and w3).
    /// </summary>
    /// <returns>The object clicked, null when the click was beside every object.</returns>
    internal int? Pick(Point at, Size size, bool add, bool range = false)
    {
        int? i = ObjectAt(at, size);
        var s = Scene;
        lock (_selection)
        {
            if (range && i is int k && _anchor is int a && a != k && s != null && a < s.Things.Count && !s.Things[a].Gone)
            {
                foreach (int j in Between(s, a, k))
                {
                    _selection.Add(j);
                }
            }
            else
            {
                if (!add)
                {
                    _selection.Clear();
                }
                if (i is int k2 && !_selection.Remove(k2))
                {
                    _selection.Add(k2);
                }
                _anchor = i;
            }
        }
        SelectionDone();
        return i;
    }

    /// <summary>The object a Shift + click's row starts from: the last one clicked.</summary>
    private int? _anchor;

    /// <summary>
    /// The shown objects on the line from object a to object b (their middles within 0.75 m of it), both
    /// ends included.
    /// </summary>
    internal IEnumerable<int> Between(WorldScene s, int a, int b)
    {
        var pa = s.Things[a].Position;
        var pb = s.Things[b].Position;
        var ab = pb - pa;
        float len2 = MathF.Max(ab.LengthSquared(), 1e-6f);
        for (int i = 0; i < s.Things.Count && i < _known.Length; i++)
        {
            var t = s.Things[i];
            if (t.Gone || !_known[i] || !_shown[(int)_kinds[i]])
            {
                continue;
            }
            float u = Math.Clamp(Vector3.Dot(t.Position - pa, ab) / len2, 0, 1);
            if (Vector3.Distance(t.Position, pa + ab * u) <= 0.75f)
            {
                yield return i;
            }
        }
    }

    /// <summary>
    /// Eyedropper: the next left click gives the object under it (null: none) instead of going to
    /// the tool; its release is swallowed too.
    /// </summary>
    internal Action<int?>? PickObjectOnce { get; set; }
    /// <summary>The eyedropper's click was taken: its release is swallowed.</summary>
    private bool _pickSwallow;

    /// <summary>Selects these things (add: to what is selected).</summary>
    internal void Select(IEnumerable<int> things, bool add = false)
    {
        var s = Scene;
        lock (_selection)
        {
            if (!add)
            {
                _selection.Clear();
            }
            foreach (int i in things)
            {
                if (s != null && i < s.Things.Count && !s.Things[i].Gone)
                {
                    _selection.Add(i);
                }
            }
        }
        SelectionDone();
    }

    /// <summary>The selection changed: its boxes made again, and <c>SelectionChanged</c> raised on the UI thread.</summary>
    private void SelectionDone()
    {
        _selectionDirty = true;
        var s = Scene;
        if (s != null)
        {
            var things = Selected.Select(n => s.Things[n]).ToList();
            Dispatcher.UIThread.Post(() => SelectionChanged?.Invoke(things));
        }
        Wake();
    }

    /// <summary>Tests (headless, so nothing is drawn): the boxes drawing gives things, in view space.</summary>
    internal void SetBoxes(IEnumerable<(int Index, Vector3 Min, Vector3 Max)> boxes)
    {
        var s = Scene!;
        EnsureSize(s.Things.Count);
        lock (_objLock)
        {
            foreach (var (i, lo, hi) in boxes)
            {
                var t = s.Things[i];
                _kinds[i] = ObjectKinds.Of(NameOf(t.Prefab), t.Piece || t.Joined, t.Tamed);
                _bounds[i] = (lo, hi);
                _boundsAt[i] = t.Position;
                _known[i] = !t.Gone;
            }
        }
    }

    /// <summary>Whether a thing is drawn (not gone, its kind shown, its model known): only those can be picked.</summary>
    internal bool IsPickable(int i)
    {
        lock (_objLock)
        {
            return i < _known.Length && _known[i] && _shown[(int)_kinds[i]];
        }
    }

    /// <summary>A thing's box (view space) and the position it was made at (world coordinates).</summary>
    internal (Vector3 Min, Vector3 Max, Vector3 At) BoxOf(int i)
    {
        lock (_objLock)
        {
            return (_bounds[i].Min, _bounds[i].Max, _boundsAt[i]);
        }
    }

    /// <summary>A thing's box, in view space.</summary>
    internal (Vector3 Min, Vector3 Max) BoundsOf(int i)
    {
        lock (_objLock)
        {
            return _bounds[i];
        }
    }

    /// <summary>A thing's View kind.</summary>
    internal ObjectKind KindOf(int i)
    {
        lock (_objLock)
        {
            return _kinds[i];
        }
    }

    /// <summary>The line shader (made at first use), and the selection's lines on the graphics card.</summary>
    private uint _lineProg, _lineVao, _lineVbo;

    /// <summary>
    /// Over this many selected objects, one box round them all instead of a box each: thousands of boxes
    /// over a stacked structure make a solid blob that hides it.
    /// </summary>
    internal const int BoxEachUpTo = 500;

    /// <summary>
    /// The selection's box lines: a box round each object (12 edges, two points each), or, past
    /// <see cref="BoxEachUpTo"/> objects, one box round all of them.
    /// </summary>
    /// <param name="bounds">The selected objects' boxes.</param>
    internal static float[] SelectionBoxes(IReadOnlyList<(Vector3 Min, Vector3 Max)> bounds)
    {
        var v = new List<float>();
        void Box(Vector3 lo, Vector3 hi)
        {
            Vector3 C(int k) => new((k & 1) == 0 ? lo.X : hi.X, (k & 2) == 0 ? lo.Y : hi.Y, (k & 4) == 0 ? lo.Z : hi.Z);
            foreach (var (a, b) in BoxEdges)
            {
                var p = C(a); var q = C(b);
                v.AddRange([p.X, p.Y, p.Z, q.X, q.Y, q.Z]);
            }
        }
        if (bounds.Count > BoxEachUpTo)
        {
            Vector3 lo = bounds[0].Min, hi = bounds[0].Max;
            foreach (var (min, max) in bounds)
            {
                lo = Vector3.Min(lo, min);
                hi = Vector3.Max(hi, max);
            }
            Box(lo, hi);
        }
        else
        {
            foreach (var (lo, hi) in bounds)
            {
                Box(lo, hi);
            }
        }
        return [.. v];
    }

    /// <summary>
    /// The selected objects' boxes, orange, seen through what is in front of them (one box round a large
    /// selection, <see cref="SelectionBoxes"/>).
    /// </summary>
    private unsafe void DrawSelection(Matrix4x4 vp)
    {
        if (_selectionDirty && Scene is { } s)
        {
            _selectionDirty = false;
            // Only things whose box is known: one without its model yet has no box to draw (it is drawn once it has).
            _selectionSegs = SelectionBoxes([.. Selected.Where(i => i < _bounds.Length && _known[i]).Select(i => OutlineBoxOf(s, i))]);
        }
        // Seen through what is in front of it, like the web editor's selection.
        DrawLines(ref _lineVao, ref _lineVbo, _selectionSegs, vp, new Vector4(1, 0.66f, 0.2f, 1));
    }

    /// <summary>The selection's box lines, made again when it changes.</summary>
    private float[] _selectionSegs = [];

    /// <summary>
    /// The Select tool: the object under the pointer, outlined faintly before a click picks it; null when none.
    /// </summary>
    internal int? HoverObject { get; private set; }
    /// <summary>The hovered object's box lines on the graphics card.</summary>
    private uint _hoverBoxVao, _hoverBoxVbo;

    /// <summary>A look for the object under the pointer is waiting (<see cref="PickHover"/>).</summary>
    private bool _hoverQueued;

    /// <summary>Tests: how many times the object under the pointer was looked for.</summary>
    internal int HoverPicks { get; private set; }

    /// <summary>Queues a look for the object under the pointer, unless one is waiting.</summary>
    private void QueueHoverPick()
    {
        if (!_hoverQueued)
        {
            _hoverQueued = true;
            Dispatcher.UIThread.Post(PickHover, DispatcherPriority.Background);
        }
    }

    /// <summary>
    /// The object under the pointer as it is now, looked for once for all the pointer's moves since the last look
    /// (Select mode, no button down: as the move that queued it would have).
    /// </summary>
    private void PickHover()
    {
        _hoverQueued = false;
        if (!SelectMode || Tool != null || _selectDown || _pointer is not Point p)
        {
            return;
        }
        HoverPicks++;
        SetHoverObject(SelectTool.OverHandle ? null : ObjectAt(p, _surfaceSize));
    }

    /// <summary>Sets the object under the pointer (null: none).</summary>
    private void SetHoverObject(int? i)
    {
        if (i != HoverObject)
        {
            HoverObject = i;
            Wake();
        }
    }

    /// <summary>The object under the pointer's box, faint, when a click can pick it.</summary>
    private void DrawHoverObject(Matrix4x4 vp)
    {
        // Only what a click can pick: drawn and not deleted (a deleted object stays under the pointer).
        if (!SelectMode || Tool != null || HoverObject is not int i || i >= _bounds.Length || _selection.Contains(i)
            || !_known[i] || !_shown[(int)_kinds[i]] || Scene is not { } sc || i >= sc.Things.Count || sc.Things[i].Gone)
        {
            return;
        }
        DrawLines(ref _hoverBoxVao, ref _hoverBoxVbo, SelectionBoxes([OutlineBoxOf(sc, i)]), vp, new Vector4(1, 0.85f, 0.55f, 0.6f), blend: true, width: 0.8f);
    }
}
