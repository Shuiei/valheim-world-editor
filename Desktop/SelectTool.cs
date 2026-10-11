using System.Numerics;
using Avalonia;
using Avalonia.Threading;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Select tool, like the web editor's (editor/transform.js, editor/selecting.js): click objects to
/// select them (Shift adds), drag on empty ground to draw a zone and select what is in it, drag a
/// selected object to move the selection, turn it (, .), lift it (PgUp PgDn), drop it onto what is
/// under it (End), type an exact place, and delete it (Del). A move is shown live; when it ends the
/// originals are replaced by copies at the new place (EditSession.Move), in one undo step.
/// </summary>
public sealed class SelectTool
{
    /// <summary>The 3D view whose scene and selection the tool works on.</summary>
    private readonly GlView _view;
    /// <summary>Degrees to radians.</summary>
    private const float D = MathF.PI / 180;

    /// <summary>The tool for one 3D view.</summary>
    public SelectTool(GlView view)
    {
        _view = view;
        _commitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _commitTimer.Tick += (_, _) => Commit();
    }

    /// <summary>What to tell the user ("" clears the line; the flag (warn): shown as a problem).</summary>
    public event Action<string, bool>? Message;
    /// <summary>Put each moved object on the ground where it ends up (pieces as one block: see GroundBlock).</summary>
    public bool OnGround { get; set; } = true;
    /// <summary>Snap moved building pieces to the pieces around.</summary>
    public bool SnapToPieces { get; set; } = true;

    /// <summary>The view's open scene.</summary>
    private WorldScene? Scene => _view.Scene;
    /// <summary>The open scene's edit session.</summary>
    private EditSession? Session => _view.Scene?.Session;

    // ---- Where things are: world coordinates (x east, z north), heights in metres.
    /// <summary>The ground's height at a world point, as drawn.</summary>
    private float GroundAt(float wx, float wz)
    {
        var s = Scene!;
        return Picking.HeightAt(s, wx - s.Cx, -(wz - s.Cz));
    }

    /// <summary>A building piece: read as one, or a prefab the piece catalogue knows (added pieces).</summary>
    private static bool IsPiece(WorldScene.Thing t) => t.Piece || PieceCatalog.Get(t.Prefab) != null;

    /// <summary>Unity's rotation (Euler degrees: z, then x, then y) of a local point.</summary>
    private static Vector3 Rotate(Vector3 p, Vector3 r)
    {
        float cz = MathF.Cos(r.Z * D), sz = MathF.Sin(r.Z * D), cx = MathF.Cos(r.X * D), sx = MathF.Sin(r.X * D), cy = MathF.Cos(r.Y * D), sy = MathF.Sin(r.Y * D);
        float x = p.X, y = p.Y, z = p.Z;
        (x, y) = (x * cz - y * sz, x * sz + y * cz);
        (y, z) = (y * cx - z * sx, y * sx + z * cx);
        return new Vector3(x * cy + z * sy, y, -x * sy + z * cy);
    }

    /// <summary>A piece's snap points in the world, placed at <paramref name="at"/> turned by <paramref name="rot"/> (Euler degrees).</summary>
    private static IEnumerable<Vector3> SnapsAt(int prefab, Vector3 at, Vector3 rot) =>
        (PieceCatalog.Get(prefab)?.Snaps ?? []).Select(p => at + Rotate(new Vector3(p[0], p[1], p[2]), rot));

    // ---- A move in progress.
    /// <summary>One object being moved.</summary>
    private sealed class Item
    {
        /// <summary>Its index in the scene's Things.</summary>
        public int Index;
        /// <summary>The object as it was when the move began.</summary>
        public WorldScene.Thing Thing;
        /// <summary>Height above the ground; a piece's bottom below its origin (from its snap points).</summary>
        public float Lift, Bottom;
        /// <summary>A building piece (moves with the block on the ground, can snap).</summary>
        public bool Piece;
    }

    /// <summary>A move of the selection, shown live until it is committed.</summary>
    private sealed class Move
    {
        /// <summary>What is moved (runestones and gone objects left out).</summary>
        public required List<Item> Items;
        /// <summary>
        /// The selection's middle when the move began (Cx, Cz: turns go around it); how far it moved (Dx, Dz) and was
        /// lifted (Dy), in metres; the turn in degrees (-180..180); the snap's shift (Sx, Sy, Sz); how far the
        /// pieces' block goes up or down to sit on the ground (Block).
        /// </summary>
        public float Cx, Cz, Dx, Dz, Dy, Turn, Sx, Sy, Sz, Block;
        /// <summary>Dropped with End (each item keeps the height it dropped to); typed exactly (no snapping).</summary>
        public bool Dropped, NoSnap;
        /// <summary>The name of the piece the selection snapped to, for the message; null when not snapped.</summary>
        public string? Snapped;
        /// <summary>The snap points of the pieces around (within 60 m), gathered once per move.</summary>
        public List<(Vector3 P, string Name)>? Others;
    }

    /// <summary>The move in progress, if any.</summary>
    private Move? _move;
    /// <summary>Commits a key-driven move (turn, lift, drop) once the keys stop for 700 ms.</summary>
    private readonly DispatcherTimer _commitTimer;
    /// <summary>A move is in progress (not committed yet).</summary>
    public bool Moving => _move != null;

    /// <summary>
    /// The move in progress, or a new one of the selection; null when there is nothing to move (a runestone alone
    /// says why).
    /// </summary>
    private Move? Begin()
    {
        if (_move != null)
        {
            return _move;
        }
        var s = Scene;
        if (s == null)
        {
            return null;
        }
        var items = new List<Item>();
        int runestones = 0;
        foreach (int i in _view.Selected)
        {
            var t = s.Things[i];
            if (t.Gone)
            {
                continue;
            }
            // A runestone's stone is built by the game from its location: it can only be deleted.
            if (t.Runestone)
            {
                runestones++;
                continue;
            }
            float[][]? snaps = PieceCatalog.Get(t.Prefab)?.Snaps;
            items.Add(new Item
            {
                Index = i,
                Thing = t,
                Lift = t.Position.Y - GroundAt(t.Position.X, t.Position.Z),
                Bottom = snaps is { Length: > 0 } ? snaps.Min(p => p[1]) : 0,
                Piece = IsPiece(t),
            });
        }
        if (items.Count == 0)
        {
            if (runestones > 0)
            {
                Message?.Invoke("Runestones cannot be moved or turned, only deleted (the game builds them from their location).", true);
            }
            return null;
        }
        return _move = new Move { Items = items, Cx = items.Average(it => it.Thing.Position.X), Cz = items.Average(it => it.Thing.Position.Z) };
    }

    /// <summary>
    /// Where an item ends up: turned around the selection's middle (clockwise from above, like Unity's
    /// yaw), moved, and on the ground there (or as high above it as before). Pieces on the ground move
    /// as one block (see GroundBlock).
    /// </summary>
    /// <param name="m">The move.</param>
    /// <param name="it">The item.</param>
    /// <param name="block">Add the block's ground shift.</param>
    /// <param name="shift">Add the snap's shift.</param>
    private Vector3 Target(Move m, Item it, bool block = true, bool shift = true)
    {
        float t = m.Turn * D, c = MathF.Cos(t), sn = MathF.Sin(t), ox = it.Thing.Position.X - m.Cx, oz = it.Thing.Position.Z - m.Cz;
        float sx = shift ? m.Sx : 0, sy = shift ? m.Sy : 0, sz = shift ? m.Sz : 0;
        float x = m.Cx + ox * c + oz * sn + m.Dx + sx, z = m.Cz - ox * sn + oz * c + m.Dz + sz;
        if (it.Piece && OnGround && !m.Dropped)
        {
            return new Vector3(x, it.Thing.Position.Y + (block ? m.Block : 0) + m.Dy + sy, z);
        }
        return new Vector3(x, GroundAt(x, z) + (m.Dropped || !OnGround ? it.Lift : -it.Bottom) + m.Dy + sy, z);
    }

    /// <summary>
    /// On the ground, the selected building pieces keep their shape: the whole block goes up or down
    /// until its bottom layer sits on the ground (sinking into a slope rather than floating).
    /// </summary>
    private void GroundBlock(Move m)
    {
        m.Block = 0;
        var pieces = m.Items.Where(it => it.Piece).ToList();
        if (pieces.Count == 0 || !OnGround || m.Dropped)
        {
            return;
        }
        float low = pieces.Min(it => it.Thing.Position.Y + it.Bottom), need = float.PositiveInfinity;
        foreach (var it in pieces.Where(it => it.Thing.Position.Y + it.Bottom <= low + 0.3f))
        {
            var p = Target(m, it, block: false, shift: false);
            need = MathF.Min(need, GroundAt(p.X, p.Z) - (it.Thing.Position.Y + it.Bottom));
        }
        if (float.IsFinite(need))
        {
            m.Block = need;
        }
    }

    /// <summary>
    /// Snapping: the closest pair of snap points (one of a moved piece, one of a piece around) within
    /// 0.75 m; the whole selection shifts so they meet.
    /// </summary>
    private void Snap(Move m)
    {
        m.Sx = m.Sy = m.Sz = 0;
        m.Snapped = null;
        var s = Scene!;
        if (!SnapToPieces || m.NoSnap || m.Dropped || !m.Items.Any(it => PieceCatalog.Get(it.Thing.Prefab)?.Snaps.Length > 0))
        {
            return;
        }
        if (m.Others == null)
        {
            var sel = m.Items.Select(it => it.Index).ToHashSet();
            m.Others = [];
            for (int i = 0; i < s.Things.Count; i++)
            {
                var t = s.Things[i];
                if (t.Gone || sel.Contains(i) || PieceCatalog.Get(t.Prefab) is not { Snaps.Length: > 0 } info)
                {
                    continue;
                }
                if (MathF.Sqrt(MathF.Pow(t.Position.X - m.Cx - m.Dx, 2) + MathF.Pow(t.Position.Z - m.Cz - m.Dz, 2)) > 60)
                {
                    continue;
                }
                foreach (var p in SnapsAt(t.Prefab, t.Position, t.Rotation))
                {
                    m.Others.Add((p, info.Name));
                }
            }
        }
        (float D, Vector3 M, Vector3 O, string Name)? best = null;
        foreach (var it in m.Items)
        {
            var at = Target(m, it, shift: false);
            foreach (var mp in SnapsAt(it.Thing.Prefab, at, it.Thing.Rotation + new Vector3(0, m.Turn, 0)))
            {
                foreach (var (o, name) in m.Others)
                {
                    float d = Vector3.Distance(o, mp);
                    if (d < 0.75f && (best == null || d < best.Value.D))
                    {
                        best = (d, mp, o, name);
                    }
                }
            }
        }
        if (best is not { } b)
        {
            return;
        }
        m.Sx = b.O.X - b.M.X;
        m.Sz = b.O.Z - b.M.Z;
        // Heights follow the ground where the pieces end up, so the height is matched after the shift.
        var first = m.Items.First(it => PieceCatalog.Get(it.Thing.Prefab)?.Snaps.Length > 0);
        float before = Target(m, first, shift: false).Y;
        m.Sy = 0;
        float after = Target(m, first).Y;
        m.Sy = b.O.Y - b.M.Y - (after - before);
        m.Snapped = b.Name;
    }

    /// <summary>Shows the move in progress in the view (as previews) and says how far it went.</summary>
    private void Preview()
    {
        var m = _move;
        if (m == null)
        {
            return;
        }
        GroundBlock(m);
        Snap(m);
        var shown = new Dictionary<int, WorldScene.Thing>();
        foreach (var it in m.Items)
        {
            shown[it.Index] = it.Thing with { Position = Target(m, it), Rotation = it.Thing.Rotation + new Vector3(0, m.Turn, 0) };
        }
        _view.SetPreviews(shown);
        float moved = MathF.Sqrt(m.Dx * m.Dx + m.Dz * m.Dz);
        Message?.Invoke($"Moving {m.Items.Count} object(s): {moved:0.0} m{(m.Turn != 0 ? $", turned {m.Turn:0.#}°" : "")}{(m.Dy != 0 ? $", {(m.Dy > 0 ? "+" : "")}{m.Dy:0.00} m" : "")}{(m.Snapped != null ? $" · snapped to {m.Snapped}" : "")}", false);
    }

    /// <summary>
    /// Ends a move: the originals are replaced by copies at their new places, and those get selected. A move that
    /// changed nothing is just dropped.
    /// </summary>
    public void Commit()
    {
        _commitTimer.Stop();
        var m = _move;
        _move = null;
        if (m == null)
        {
            return;
        }
        _view.SetPreviews(null);
        if (MathF.Abs(m.Dx) + MathF.Abs(m.Dz) < 0.01f && m.Turn == 0 && MathF.Abs(m.Dy) < 0.001f && !m.Dropped || Session is not { } session)
        {
            return;
        }
        GroundBlock(m);
        Snap(m);
        var moves = m.Items.Select(it => (it.Index, Target(m, it), it.Thing.Rotation + new Vector3(0, m.Turn, 0))).ToList();
        var copies = session.Move(moves, $"Moved {moves.Count}");
        _view.Select(copies);
        Message?.Invoke($"Moved {copies.Count} object(s). Ctrl+Z puts them back.", false);
    }

    /// <summary>Puts a move back (Esc); false when there was none.</summary>
    public bool Cancel()
    {
        if (_move == null)
        {
            return false;
        }
        _commitTimer.Stop();
        _move = null;
        _handleDrag = null;
        _view.SetPreviews(null);
        Message?.Invoke("Move cancelled.", false);
        return true;
    }

    /// <summary>Restarts the commit timer: the move is committed once the keys stop.</summary>
    private void Later()
    {
        _commitTimer.Stop();
        _commitTimer.Start();
    }

    /// <summary>Turns the selection about its middle (positive: clockwise from above), committed shortly after.</summary>
    public void Turn(float degrees)
    {
        if (Begin() is not { } m)
        {
            return;
        }
        m.Turn = ((m.Turn + degrees) % 360 + 360) % 360;
        if (m.Turn > 180)
        {
            m.Turn -= 360;
        }
        Preview();
        Later();
    }

    /// <summary>Lifts (or, negative, lowers) the selection, committed shortly after.</summary>
    public void Lift(float metres)
    {
        if (Begin() is not { } m)
        {
            return;
        }
        m.Dy += metres;
        Preview();
        Later();
    }

    /// <summary>
    /// End: each selected object drops onto whatever is under it: the top of another object, or else
    /// the ground.
    /// </summary>
    public void Drop()
    {
        if (Begin() is not { } m)
        {
            return;
        }
        var s = Scene!;
        var sel = m.Items.Select(it => it.Index).ToHashSet();
        int onObjects = 0;
        foreach (var it in m.Items)
        {
            var p = Target(m, it);
            // The object's box (view space) brought to where the move puts it: the box was made where the
            // object was shown then, which the move may already have changed. Or a small box at its place.
            Vector3 lo, hi;
            if (_view.IsPickable(it.Index))
            {
                var (blo, bhi, at) = _view.BoxOf(it.Index);
                var shift = new Vector3(p.X - at.X, p.Y - at.Y, -(p.Z - at.Z));
                (lo, hi) = (blo + shift, bhi + shift);
            }
            else
            {
                (lo, hi) = (new Vector3(p.X - s.Cx - 0.1f, p.Y, -(p.Z - s.Cz) - 0.1f), new Vector3(p.X - s.Cx + 0.1f, p.Y + 0.2f, -(p.Z - s.Cz) + 0.1f));
            }
            float cx = (lo.X + hi.X) / 2, cz = (lo.Z + hi.Z) / 2, hx = (hi.X - lo.X) * 0.35f, hz = (hi.Z - lo.Z) * 0.35f;
            float ground = GroundAt(p.X, p.Z), best = float.NegativeInfinity;
            // The highest top of another drawn object under the middle or four points of the footprint.
            foreach (var (ox, oz) in new[] { (0f, 0f), (-hx, -hz), (hx, -hz), (-hx, hz), (hx, hz) })
            {
                float x = cx + ox, z = cz + oz;
                for (int i = 0; i < s.Things.Count; i++)
                {
                    if (sel.Contains(i) || !_view.IsPickable(i))
                    {
                        continue;
                    }
                    var (bl, bh) = _view.BoundsOf(i);
                    if (x >= bl.X && x <= bh.X && z >= bl.Z && z <= bh.Z && bh.Y <= hi.Y + 0.05f && bh.Y > best)
                    {
                        best = bh.Y;
                    }
                }
            }
            float y = best > ground + 0.01f ? best - (lo.Y - p.Y) : ground;
            if (best > ground + 0.01f)
            {
                onObjects++;
            }
            it.Lift = y - ground - m.Dy;
        }
        m.Dropped = true;
        Preview();
        Later();
        Message?.Invoke($"Dropped {m.Items.Count} object(s): {onObjects} onto other objects, {m.Items.Count - onObjects} onto the ground.", false);
    }

    // ---- Exact place: the selection's middle (height: its lowest object) and the first one's turn.
    /// <summary>
    /// The selection's middle (height: its lowest object), the first one's turn (-180..180) and how many there
    /// are; null when nothing is selected.
    /// </summary>
    public (float X, float Y, float Z, float Turn, int Count)? Where()
    {
        var s = Scene;
        if (s == null)
        {
            return null;
        }
        var things = _view.Selected.Where(i => i < s.Things.Count).Select(i => s.Things[i]).Where(t => !t.Gone).ToList();
        if (things.Count == 0)
        {
            return null;
        }
        float turn = ((things[0].Rotation.Y + 180) % 360 + 360) % 360 - 180;
        return (things.Average(t => t.Position.X), things.Min(t => t.Position.Y), things.Average(t => t.Position.Z), turn, things.Count);
    }

    /// <summary>Moves the selection there (by: by these amounts), as one undo step; typed values are not snapped.</summary>
    /// <param name="x">World x of the middle, in metres (or how far to move).</param>
    /// <param name="y">Height of the lowest object, in metres (or how far to lift).</param>
    /// <param name="z">World z of the middle, in metres (or how far to move).</param>
    /// <param name="turn">The first object's turn, in degrees (or how far to turn).</param>
    /// <param name="by">The values are amounts to move by, not a place.</param>
    public void PlaceAt(float x, float y, float z, float turn, bool by)
    {
        Commit();
        if (Where() is not { } w || Begin() is not { } m)
        {
            return;
        }
        m.Dx += by ? x : x - w.X;
        m.Dz += by ? z : z - w.Z;
        m.Turn += by ? turn : turn - w.Turn;
        m.NoSnap = true;
        GroundBlock(m);
        var low = m.Items.MinBy(it => it.Thing.Position.Y)!;
        float now = Target(m, low).Y;
        m.Dy += low.Thing.Position.Y + (by ? y : y - w.Y) - now;
        Commit();
    }

    /// <summary>Deletes the selection (Del), as one undo step.</summary>
    public void Delete()
    {
        Commit();
        var sel = _view.Selected.ToList();
        if (sel.Count == 0 || Session is not { } session)
        {
            Message?.Invoke("Select objects to delete first.", true);
            return;
        }
        session.Delete(sel);
        _view.Select([]);
        Message?.Invoke($"Deleted {sel.Count} object(s). Ctrl+Z brings them back; they leave the world when it is saved.", false);
    }

    // ---- More ways to select.
    /// <summary>The indices of the objects drawn and pickable now (not gone, not hidden by View).</summary>
    private IEnumerable<int> Shown()
    {
        var s = Scene!;
        for (int i = 0; i < s.Things.Count; i++)
        {
            if (!s.Things[i].Gone && _view.IsPickable(i))
            {
                yield return i;
            }
        }
    }

    /// <summary>Selects every shown object of the selected objects' kinds (prefabs).</summary>
    public void SameKind()
    {
        var s = Scene;
        if (s == null)
        {
            return;
        }
        var kinds = _view.Selected.Select(i => s.Things[i].Prefab).ToHashSet();
        if (kinds.Count == 0)
        {
            Message?.Invoke("Select an object of each kind you want first.", true);
            return;
        }
        var ids = Shown().Where(i => kinds.Contains(s.Things[i].Prefab)).ToList();
        _view.Select(ids);
        Message?.Invoke($"Selected every shown object of the same kind: {ids.Count}.", false);
    }

    /// <summary>Selects the shown objects that are not selected, and only those.</summary>
    public void Invert()
    {
        if (Scene == null)
        {
            return;
        }
        var sel = _view.Selected.ToHashSet();
        var ids = Shown().Where(i => !sel.Contains(i)).ToList();
        _view.Select(ids);
        Message?.Invoke($"Selected the {ids.Count} shown object(s) that were not selected.", false);
    }

    /// <summary>
    /// Every building piece connected to the selected ones through pieces that touch (boxes grown a little), added
    /// to the selection.
    /// </summary>
    /// <param name="from">The pieces to start from; null for the selection.</param>
    public void WholeBuilding(IEnumerable<int>? from = null)
    {
        var s = Scene;
        if (s == null)
        {
            return;
        }
        var start = (from ?? _view.Selected).Where(i => IsPiece(s.Things[i])).ToList();
        if (start.Count == 0)
        {
            Message?.Invoke("Select a building piece first (or double-click one).", true);
            return;
        }
        var boxes = new Dictionary<int, (Vector3 Min, Vector3 Max)>();
        foreach (int i in Shown().Where(i => IsPiece(s.Things[i])))
        {
            var (lo, hi) = _view.BoundsOf(i);
            boxes[i] = (lo - new Vector3(0.15f), hi + new Vector3(0.15f));
        }
        const float Cell = 4;
        var grid = new Dictionary<(int, int, int), List<int>>();
        static IEnumerable<(int, int, int)> Cells((Vector3 Min, Vector3 Max) b)
        {
            for (int x = (int)MathF.Floor(b.Min.X / Cell); x <= (int)MathF.Floor(b.Max.X / Cell); x++)
            {
                for (int y = (int)MathF.Floor(b.Min.Y / Cell); y <= (int)MathF.Floor(b.Max.Y / Cell); y++)
                {
                    for (int z = (int)MathF.Floor(b.Min.Z / Cell); z <= (int)MathF.Floor(b.Max.Z / Cell); z++)
                    {
                        yield return (x, y, z);
                    }
                }
            }
        }
        foreach (var (i, b) in boxes)
        {
            foreach (var c in Cells(b))
            {
                (grid.TryGetValue(c, out var l) ? l : grid[c] = []).Add(i);
            }
        }
        static bool Touch((Vector3 Min, Vector3 Max) a, (Vector3 Min, Vector3 Max) b) =>
            a.Min.X <= b.Max.X && a.Max.X >= b.Min.X && a.Min.Y <= b.Max.Y && a.Max.Y >= b.Min.Y && a.Min.Z <= b.Max.Z && a.Max.Z >= b.Min.Z;
        var seen = start.Where(boxes.ContainsKey).ToHashSet();
        var queue = new Stack<int>(seen);
        while (queue.Count > 0)
        {
            var b = boxes[queue.Pop()];
            foreach (var c in Cells(b))
            {
                foreach (int o in grid.GetValueOrDefault(c) ?? [])
                {
                    if (!seen.Contains(o) && Touch(boxes[o], b))
                    {
                        seen.Add(o);
                        queue.Push(o);
                    }
                }
            }
        }
        _view.Select(seen, add: true);
        Message?.Invoke($"Selected the whole building: {seen.Count} connected piece(s).", false);
    }

    // ---- The handles (Gizmo): the one under the mouse or being dragged is drawn highlighted.
    /// <summary>The handle under the mouse, if any.</summary>
    private Gizmo.Handle? _hover;
    /// <summary>
    /// A handle being dragged: which, the gizmo's centre, where along its axis (or heading round the ring) the drag
    /// began, and the move's offsets and turn then.
    /// </summary>
    private (Gizmo.Handle Handle, Vector3 Centre, float Start, float Dx, float Dz, float Dy, float Turn)? _handleDrag;
    /// <summary>The handle to draw highlighted: the one dragged, else the one under the mouse.</summary>
    public Gizmo.Handle? HotHandle => _handleDrag?.Handle ?? _hover;

    /// <summary>The handle under a point of the view, if the gizmo shows.</summary>
    private Gizmo.Handle? HandleAt(Point at, Size size)
    {
        if (_view.GizmoAt() is not var (c, scale))
        {
            return null;
        }
        var (o, d) = _view.RayAt(at, size);
        return Gizmo.Hit(o, d, c, scale);
    }

    /// <summary>The pointer is over a move handle (a click there drags, it does not pick).</summary>
    public bool OverHandle => _hover != null;

    /// <summary>The pointer moved with no button down: highlights the handle under it.</summary>
    public void Hover(Point at, Size size)
    {
        var h = HandleAt(at, size);
        if (h != _hover)
        {
            _hover = h;
            _view.LassoChanged();
        }
    }

    // ---- The mouse (left button; the view passes it on in the Select tool).
    /// <summary>A press on a selected object: where on screen, and the ground point under it.</summary>
    private (Point At, Vector3 From)? _drag;
    /// <summary>That press moved far enough (5 px) to be a move, not a click.</summary>
    private bool _dragMoving;
    /// <summary>
    /// A press on empty ground: where on screen, the zone's ground points (x, z), whether it adds to the selection
    /// (Shift), and whether it is being drawn (moved past 6 px) rather than a click.
    /// </summary>
    private (Point At, List<Vector2> Points, bool Add, bool Drawing)? _lasso;
    /// <summary>A copy of the zone's points for the drawing thread.</summary>
    public Vector2[]? Lasso { get; private set; }

    /// <summary>Shift (not Ctrl) held: a click takes the row from the last object clicked (GlView.Pick).</summary>
    public bool Range { get; set; }

    /// <summary>
    /// The left button went down: drags a handle, starts a move of the selection, starts a zone on empty ground,
    /// or picks.
    /// </summary>
    /// <param name="at">The point in the view.</param>
    /// <param name="size">The view's size.</param>
    /// <param name="shift">Shift held: add to the selection.</param>
    /// <param name="alt">Alt held: objects are not hit (the ground only).</param>
    /// <param name="clicks">The click count (2 or more on a building piece other than a chest: the whole building).</param>
    public void Down(Point at, Size size, bool shift, bool alt, int clicks)
    {
        var s = Scene;
        if (s == null)
        {
            return;
        }
        // A handle: drag along its axis, or around the ring (not for a Shift + click's row: the pieces
        // in the row may lie under the handles).
        if (!Range && HandleAt(at, size) is { } handle && _view.GizmoAt() is var (c, _))
        {
            _commitTimer.Stop();
            if (Begin() is not { } mv)
            {
                return;
            }
            var (o, d) = _view.RayAt(at, size);
            float? start = handle == Gizmo.Handle.Ring ? Gizmo.Heading(o, d, c) : Gizmo.Along(o, d, c, Gizmo.Axis(handle));
            if (start is float st)
            {
                _handleDrag = (handle, c, st, mv.Dx, mv.Dz, mv.Dy, mv.Turn);
            }
            return;
        }
        int? hit = alt ? null : _view.ObjectAt(at, size);
        var ground = _view.WorldAt(at, size);
        // A double click on a building piece: the whole building; on a chest, the chest alone (its
        // inspector opens: GlView.InspectAsked).
        if (clicks >= 2 && hit is int dbl && IsPiece(s.Things[dbl]) && !(PrefabCatalog.Details(s.Things[dbl].Prefab)?.ContainerW > 0))
        {
            WholeBuilding([dbl]);
            return;
        }
        if (hit is int h && _view.Selected.Contains(h) && !shift && ground is { } g)
        {
            _drag = (at, g);
            _dragMoving = false;
            return;
        }
        Commit();
        if (hit == null && ground is { } g2)
        {
            // Empty ground: a click deselects, a drag draws a zone to select.
            _lasso = (at, new List<Vector2> { new(g2.X, g2.Z) }, shift, false);
            return;
        }
        _view.Pick(at, size, shift, Range);
    }

    /// <summary>The pointer moved with the left button down: drags the handle, draws the zone, or moves the selection.</summary>
    /// <param name="at">The point in the view.</param>
    /// <param name="size">The view's size.</param>
    /// <param name="ctrl">Ctrl held: handle drags go in 15° or half-metre steps.</param>
    public void Moved(Point at, Size size, bool ctrl = false)
    {
        if (_handleDrag is { } hd)
        {
            if (_move is not { } mv)
            {
                return;
            }
            var (ro, rd) = _view.RayAt(at, size);
            if (hd.Handle == Gizmo.Handle.Ring)
            {
                if (Gizmo.Heading(ro, rd, hd.Centre) is not float h)
                {
                    return;
                }
                // Ctrl: 15° steps.
                float turn = hd.Turn + h - hd.Start;
                if (ctrl)
                {
                    turn = MathF.Round(turn / 15) * 15;
                }
                mv.Turn = ((turn + 180) % 360 + 360) % 360 - 180;
            }
            else
            {
                if (Gizmo.Along(ro, rd, hd.Centre, Gizmo.Axis(hd.Handle)) is not float t)
                {
                    return;
                }
                // Ctrl: half-metre steps. X and Z keep following the ground.
                float by = t - hd.Start;
                if (ctrl)
                {
                    by = MathF.Round(by / 0.5f) * 0.5f;
                }
                (mv.Dx, mv.Dz, mv.Dy) = (hd.Dx, hd.Dz, hd.Dy);
                switch (hd.Handle)
                {
                    case Gizmo.Handle.X: mv.Dx += by; break;
                    case Gizmo.Handle.Z: mv.Dz += by; break;
                    default: mv.Dy += by; break;
                }
            }
            Preview();
            return;
        }
        if (_lasso is { } l)
        {
            bool drawing = l.Drawing || Point.Distance(at, l.At) > 6;
            if (drawing && _view.WorldAt(at, size) is { } g && Vector2.Distance(l.Points[^1], new Vector2(g.X, g.Z)) >= 0.5f)
            {
                l.Points.Add(new Vector2(g.X, g.Z));
            }
            _lasso = l with { Drawing = drawing };
            Lasso = [.. l.Points];
            _view.LassoChanged();
            return;
        }
        if (_drag is not { } d)
        {
            return;
        }
        if (!_dragMoving && Point.Distance(at, d.At) > 5)
        {
            _dragMoving = true;
            _commitTimer.Stop();
            Begin();
        }
        if (_dragMoving && _move is { } m && _view.WorldAt(at, size) is { } g2)
        {
            m.Dx = g2.X - d.From.X;
            m.Dz = g2.Z - d.From.Z;
            Preview();
        }
    }

    /// <summary>
    /// The left button came up: ends a handle drag or a move, selects what is inside the zone drawn, or (a plain
    /// click on a selected object) picks that one.
    /// </summary>
    public void Up(Point at, Size size, bool shift)
    {
        if (_handleDrag != null)
        {
            _handleDrag = null;
            Commit();
            return;
        }
        if (_lasso is { } l)
        {
            _lasso = null;
            Lasso = null;
            _view.LassoChanged();
            if (!l.Drawing || l.Points.Count < 3)
            {
                if (!l.Add)
                {
                    _view.Select([]);
                }
                return;
            }
            var s = Scene!;
            var ids = Shown().Where(i => Inside(l.Points, s.Things[i].Position.X, s.Things[i].Position.Z)).ToList();
            _view.Select(ids, l.Add);
            Message?.Invoke(ids.Count > 0 ? $"Selected {ids.Count} object(s) inside the zone{(l.Add ? $" ({_view.Selected.Count} in all)" : "")}." : "Nothing shown inside the zone.", false);
            return;
        }
        var d = _drag;
        _drag = null;
        if (d == null)
        {
            return;
        }
        if (_dragMoving)
        {
            Commit();
        }
        else
        {
            // A plain click on a selected object: that one only.
            _view.Pick(at, size, shift, Range);
        }
    }

    /// <summary>Whether a point (x, z) is inside a polygon of (x, z) points (even-odd rule).</summary>
    public static bool Inside(IReadOnlyList<Vector2> poly, float x, float z)
    {
        bool c = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i];
            var b = poly[j];
            if ((a.Y > z) != (b.Y > z) && x < (b.X - a.X) * (z - a.Y) / (b.Y - a.Y) + a.X)
            {
                c = !c;
            }
        }
        return c;
    }

    /// <summary>
    /// The tool's keys (Esc, Del; with a selection: , . turn, PgUp PgDn lift, End drops; Shift for bigger steps);
    /// true when the key was one.
    /// </summary>
    public bool Key(Avalonia.Input.Key key, bool shift)
    {
        switch (key)
        {
            case Avalonia.Input.Key.Escape when _lasso != null:
                _lasso = null;
                Lasso = null;
                _view.LassoChanged();
                return true;
            case Avalonia.Input.Key.Escape:
                return Cancel();
            case Avalonia.Input.Key.Delete:
                Delete();
                return true;
        }
        if (_view.Selected.Count == 0)
        {
            return false;
        }
        // 1° (Shift: 15°), like the web editor.
        float step = shift ? 15 : 1;
        switch (key)
        {
            case Avalonia.Input.Key.OemComma:
                Turn(-step);
                return true;
            case Avalonia.Input.Key.OemPeriod:
                Turn(step);
                return true;
            case Avalonia.Input.Key.PageUp:
                Lift(shift ? 1 : 0.25f);
                return true;
            case Avalonia.Input.Key.PageDown:
                Lift(shift ? -1 : -0.25f);
                return true;
            case Avalonia.Input.Key.End:
                Drop();
                return true;
        }
        return false;
    }
}
