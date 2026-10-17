using System.Numerics;
using Avalonia;
using Avalonia.Threading;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

/// <summary>
/// The Place tool's mouse and keys (the web editor's plant.js handlers). Brush: a click places what the preview shows,
/// a drag paints more (Shift + drag removes the chosen kinds); each stroke is one undo step. Line, Zone: click points
/// (or hold and drag to draw freely), drag a point to move it, the line to add one, Ctrl + click to remove one; Circle
/// and Rectangle: press and drag; Grid: drag a box; Enter (or a double-click on the last point) places.
/// </summary>
public sealed class PlaceInput
{
    /// <summary>The view: hit tests, selection highlight and redraws.</summary>
    private readonly GlView _view;
    /// <summary>The Place tool whose settings and shapes the input drives.</summary>
    public PlaceTool Tool { get; }
    /// <summary>The edit session placements are committed to (null while no world is open).</summary>
    public Func<EditSession?> Session { get; set; } = () => null;
    /// <summary>A line for the status bar.</summary>
    public event Action<Said>? Message;
    /// <summary>The step <c>,</c> and <c>.</c> turn by, in degrees (Shift: 1°); null: 1°, Shift 15°.</summary>
    public float? TurnStep { get; set; }
    /// <summary>Something was placed: the kinds placed, for the recent list.</summary>
    public event Action<IReadOnlyList<string>>? Placed;

    /// <summary>
    /// Wires the input to the view and the tool; the preview is worked out again whenever the tool changes.
    /// </summary>
    public PlaceInput(GlView view, PlaceTool tool)
    {
        _view = view;
        Tool = tool;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => Paint();
        tool.Changed += Refresh;
    }

    /// <summary>
    /// What is drawn: the preview (or what a stroke has placed so far), as copies for the drawing thread.
    /// </summary>
    public PlaceTool.Placement[] Shown { get; private set; } = [];
    /// <summary>What a click or Enter would place now.</summary>
    private List<PlaceTool.Placement> _preview = [];
    /// <summary>What a click or Enter would place now (for the panel's count).</summary>
    public IReadOnlyList<PlaceTool.Placement> PreviewNow => _preview;
    /// <summary>The cursor's grid point, or null off the ground.</summary>
    private Vector2? _at;
    /// <summary>The mouse on the view, in pixels, once it has moved.</summary>
    private Point? _pointer;
    /// <summary>The view's size with the last mouse event.</summary>
    private Size _size;
    /// <summary>Shift is held: in Brush the preview is hidden (a drag removes instead).</summary>
    private bool _shift;

    /// <summary>
    /// The cursor's grid point changed, or a setting: the preview is worked out again (not during a stroke).
    /// </summary>
    public void Refresh()
    {
        if (_stroke != null)
        {
            return;
        }
        int? under = (Tool.OnTop || Tool.Building) && _pointer is Point p ? _view.ObjectAt(p, _size) : null;
        _preview = _view.Mode == ToolMode.Place && !(Tool.Mode == PlaceTool.Modes.Brush && _shift) ? Tool.Preview(_at, under) : [];
        Shown = [.. _preview];
        _view.LassoChanged();
        Changed?.Invoke();
    }
    /// <summary>The preview was worked out again.</summary>
    public event Action? Changed;

    /// <summary>Shift was pressed or let go.</summary>
    public void ShiftHeld(bool on)
    {
        if (_shift != on)
        {
            _shift = on;
            Refresh();
        }
    }

    // ---- Brush strokes.
    /// <summary>A brush stroke, from press to release: one undo step.</summary>
    private sealed class Stroke
    {
        /// <summary>Shift + drag: removes the chosen kinds instead of placing.</summary>
        public bool Erase;
        /// <summary>Where the press was, in pixels; past 6 px it is a drag.</summary>
        public Point From;
        /// <summary>The mouse moved far enough to paint (or erase).</summary>
        public bool Dragging;
        /// <summary>What the preview showed at the press: what a click places.</summary>
        public List<PlaceTool.Placement> Stamp = [];
        /// <summary>What the drag has painted so far (the stamp included).</summary>
        public List<PlaceTool.Placement> Added = [];
        /// <summary>Erasing: the things to remove (indices into the scene's things).</summary>
        public HashSet<int> Removed = [];
        /// <summary>When the stroke last painted (ms on the input's clock).</summary>
        public long Last;
    }

    /// <summary>The stroke under way, or null.</summary>
    private Stroke? _stroke;
    /// <summary>Paints about 30 times a second during a stroke.</summary>
    private readonly DispatcherTimer _timer;
    /// <summary>Times the paint steps.</summary>
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    /// <summary>
    /// A paint step of the stroke under way: erases under the brush, or adds placements around the cursor (not while
    /// building).
    /// </summary>
    private void Paint()
    {
        if (_stroke is not { Dragging: true } s || _at is not { } at)
        {
            return;
        }
        long now = _clock.ElapsedMilliseconds;
        float dt = (now - s.Last) / 1000f;
        s.Last = now;
        if (s.Erase)
        {
            foreach (int i in Tool.EraseAt(at))
            {
                s.Removed.Add(i);
            }
            _view.Select(s.Removed);
            return;
        }
        // Building (the Workshop) or One at a time: a click puts one; a drag does not paint more.
        if (Tool.Building || Tool.OneAtATime)
        {
            return;
        }
        s.Added.AddRange(Tool.PaintStep(at, dt, s.Added));
        Shown = [.. s.Added];
        _view.LassoChanged();
    }

    // ---- Shapes.
    /// <summary>A press on a shape: where (pixels and grid point) and whether it has become a drag.</summary>
    private (Point At, Vector2 Hit, bool Drag)? _press;
    /// <summary>The shape's point being dragged, or null.</summary>
    private int? _dragPt;

    /// <summary>The shape's points can be dragged, inserted and removed (Line with Points, and Zone).</summary>
    private bool Editable => Tool.Mode == PlaceTool.Modes.Line && Tool.LineShape == PlaceTool.LineShapes.Points || Tool.Mode == PlaceTool.Modes.Zone;
    /// <summary>The shape is made of clicked points (Line and Zone).</summary>
    private bool Pointed => Tool.Mode is PlaceTool.Modes.Line or PlaceTool.Modes.Zone;

    /// <summary>The points where they are drawn (a zone can be turned: its points are kept unturned).</summary>
    public List<Vector2> ShownPoints()
    {
        lock (Tool.Points)
        {
            return Tool.Mode == PlaceTool.Modes.Zone ? [.. Tool.Points.Select(Tool.Xf)] : [.. Tool.Points];
        }
    }

    /// <summary>Where a grid point is on the view (30 cm above the ground), or null behind the camera.</summary>
    private Point? Screen(Vector2 g) => _view.ScreenOfGrid(g, 0.3f);

    /// <summary>The shape's point within 10 px of the mouse, or -1.</summary>
    private int PointAt(Point at) => Lines.PointNear(ShownPoints(), at, Screen);

    /// <summary>The stretch of the outline under the mouse: the index of the point it starts from, or -1.</summary>
    private int SegmentAt(Point at)
    {
        var dense = Tool.Mode == PlaceTool.Modes.Zone
            ? ShownPoints() is { Count: > 0 } z ? [.. z.Append(z[0]).Select((p, i) => (P: p, Seg: i))] : []
            : Tool.LineCurve();
        return Lines.StretchNear(dense, at, Screen);
    }

    /// <summary>
    /// Pick from world: the next click takes the kind of the object clicked (with whether Shift was held).
    /// </summary>
    public Action<string, bool>? PickOnce { get; set; }

    /// <summary>
    /// A mouse press: picks a kind (Pick from world), picks the elevation (Alt), starts a brush stroke, or starts or
    /// edits a shape.
    /// </summary>
    /// <param name="at">The mouse on the view, in pixels.</param>
    /// <param name="size">The view's size.</param>
    /// <param name="shift">Shift held (Brush: erase; picking: add the kind).</param>
    /// <param name="ctrl">Ctrl held: remove the point clicked.</param>
    /// <param name="alt">Alt held (without Shift): pick the elevation.</param>
    /// <param name="clicks">The click count; a double-click on the last point places the shape.</param>
    public void Down(Point at, Size size, bool shift, bool ctrl, bool alt, int clicks)
    {
        _pointer = at;
        _size = size;
        if (PickOnce is { } pick)
        {
            PickOnce = null;
            if (_view.ObjectAt(at, size) is int i && _view.Scene is { } sc && Tool.NameOf(sc.Things[i].Prefab) is string name)
            {
                pick(name, shift);
            }
            else
            {
                Message?.Invoke(Said.Of("view.msg.nothingPicked"));
            }
            return;
        }
        // Building: where the cursor points (on a piece, or the ground), as the preview shows it.
        var hit = Tool.Building && _at != null ? _at : _view.GridAt(at, size);
        if (alt && !shift)
        {
            if (_view.WorldAt(at, size) is { } w)
            {
                Message?.Invoke(Tool.PickElevation(_view.ObjectAt(at, size), w.Y));
            }
            return;
        }
        if (hit is not { } h)
        {
            return;
        }
        _at = h;
        if (Tool.Mode == PlaceTool.Modes.Brush)
        {
            // The click's placements are taken now, from what the preview shows.
            _stroke = new Stroke { Erase = shift, From = at, Stamp = [.. _preview], Last = _clock.ElapsedMilliseconds };
            _timer.Start();
            return;
        }
        _press = (at, h, false);
        if (Tool.Mode == PlaceTool.Modes.Line && Tool.LineShape != PlaceTool.LineShapes.Points)
        {
            Tool.FigA = h;
            Tool.FigB = null;
            Tool.Notify();
            return;
        }
        // A double-click on the last point places the shape (before the point is taken for a drag: the
        // second click of a double-click is always on it).
        if (Pointed && clicks >= 2 && Tool.Points.Count > 0 && Vector2.Distance(Tool.Points[^1], Tool.Local(h)) < 1.5f)
        {
            _press = null;
            PlaceShape();
            return;
        }
        if (Editable && Tool.Points.Count > 0)
        {
            int pt = PointAt(at);
            if (pt >= 0)
            {
                _press = null;
                if (ctrl)
                {
                    lock (Tool.Points) { Tool.Points.RemoveAt(pt); }
                    Tool.Notify();
                    return;
                }
                _dragPt = pt;
                return;
            }
            int seg = Tool.Points.Count >= (Tool.Mode == PlaceTool.Modes.Zone ? 3 : 2) ? SegmentAt(at) : -1;
            if (seg >= 0)
            {
                _press = null;
                lock (Tool.Points) { Tool.Points.Insert(seg + 1, Tool.Local(h)); }
                _dragPt = seg + 1;
                Tool.Notify();
                return;
            }
        }
        if (Tool.Mode == PlaceTool.Modes.Grid)
        {
            Tool.GridA = h;
            Tool.GridB = null;
            Tool.ClearTurn();
        }
    }

    /// <summary>Building: Ctrl + wheel lifts the piece (or lowers it) from where the cursor points.</summary>
    public void Lift(float metres)
    {
        Tool.HeightNudge = MathF.Round(Tool.HeightNudge + metres, 2);
        if (_pointer is Point p)
        {
            Moved(p, _size);
        }
        Tool.Notify();
        Message?.Invoke(Tool.HeightNudge == 0 ? Said.Of("place.msg.liftNone") : Said.Of("place.msg.lift", Tool.HeightNudge));
    }

    /// <summary>
    /// The mouse moved: follows the cursor, starts a drag past 6 px, drags points, figures and grid boxes, or draws
    /// freehand (a point every 2 m).
    /// </summary>
    public void Moved(Point at, Size size)
    {
        _pointer = at;
        _size = size;
        var hit = _view.GridAt(at, size);
        // Building: the build ray itself (the piece goes where it hits, a piece or the ground).
        if (Tool.Building)
        {
            Tool.AimRay = _view.WorldRay(at, size);
            hit ??= Tool.AimRay != null ? Vector2.Zero : null;
        }
        _at = hit;
        if (_stroke is { } s)
        {
            if (!s.Dragging && Point.Distance(at, s.From) > 6)
            {
                s.Dragging = true;
                // Painting: the ground under the click gets the preview too, then the brush adds more. Building keeps
                // its one piece where it is: the click puts it, however far the mouse went (EndStroke).
                if (!s.Erase && !Tool.Building)
                {
                    s.Added.AddRange(s.Stamp);
                    s.Stamp.Clear();
                }
            }
            return;
        }
        if (hit is not { } h)
        {
            Refresh();
            return;
        }
        if (_press is { } && Tool.Mode == PlaceTool.Modes.Line && Tool.LineShape != PlaceTool.LineShapes.Points)
        {
            Tool.FigB = h;
            Tool.Notify();
            return;
        }
        if (_dragPt is int dp)
        {
            lock (Tool.Points) { Tool.Points[dp] = Tool.Local(h); }
            Tool.Notify();
            return;
        }
        if (_press is { } pr)
        {
            if (!pr.Drag && Point.Distance(at, pr.At) > 6)
            {
                _press = pr with { Drag = true };
                if (Pointed)
                {
                    var q = Tool.Local(pr.Hit);
                    lock (Tool.Points)
                    {
                        if (Tool.Points.Count == 0 || Vector2.Distance(Tool.Points[^1], q) > 0.3f)
                        {
                            Tool.Points.Add(q);
                        }
                    }
                }
            }
            if (_press.Value.Drag)
            {
                if (Tool.Mode == PlaceTool.Modes.Grid)
                {
                    Tool.GridB = h;
                }
                else
                {
                    // Freehand: a point every 2 m, smoothed by the curve.
                    var q = Tool.Local(h);
                    lock (Tool.Points)
                    {
                        if (Vector2.Distance(Tool.Points[^1], q) >= 2)
                        {
                            Tool.Points.Add(q);
                        }
                    }
                }
                Tool.Notify();
                return;
            }
        }
        Refresh();
    }

    /// <summary>
    /// The mouse was let go: ends a stroke (committing it), a point drag, or a press on a shape (adding the point, or
    /// finishing the figure or box).
    /// </summary>
    public void Up(Point at, Size size)
    {
        if (_stroke is { } s)
        {
            _stroke = null;
            _timer.Stop();
            EndStroke(s);
            return;
        }
        if (_dragPt != null)
        {
            _dragPt = null;
            return;
        }
        if (_press is not { } p)
        {
            return;
        }
        _press = null;
        var hit = _view.GridAt(at, size) ?? _at ?? p.Hit;
        if (Tool.Mode == PlaceTool.Modes.Line && Tool.LineShape != PlaceTool.LineShapes.Points)
        {
            if (!p.Drag && Tool.FigB == null)
            {
                Tool.FigA = null;
                Message?.Invoke(Tool.LineShape == PlaceTool.LineShapes.Circle ? Said.Of("place.msg.circleHow") : Said.Of("place.msg.rectHow"));
            }
            Tool.Notify();
            return;
        }
        if (Pointed)
        {
            var q = Tool.Local(hit);
            lock (Tool.Points)
            {
                if (!p.Drag || Vector2.Distance(Tool.Points[^1], q) > 0.3f)
                {
                    Tool.Points.Add(q);
                }
            }
        }
        else if (!p.Drag)
        {
            Tool.GridA = Tool.GridB = null;
        }
        Tool.Notify();
    }

    /// <summary>
    /// Commits a finished stroke as one undo step (the removed things, or the click's or drag's placements) and says
    /// what it did.
    /// </summary>
    private void EndStroke(Stroke s)
    {
        _view.Select([]);
        if (Session() is not { } session)
        {
            return;
        }
        if (s.Erase)
        {
            if (s.Removed.Count > 0)
            {
                session.Commit(Lang.T("place.undo.removed", s.Removed.Count), null, s.Removed, []);
            }
            Message?.Invoke(Said.Of("place.msg.removed", s.Removed.Count));
        }
        else
        {
            var list = s.Dragging && !Tool.Building ? s.Added : s.Stamp;
            if (list.Count > 0)
            {
                var names = list.Select(o => o.Name).Distinct().ToList();
                Commit(session, list, Lang.T("place.undo.placed", list.Count, string.Join(", ", names.Take(3))));
                Placed?.Invoke(names);
            }
            Message?.Invoke(Tool.Building
                ? list.Count == 0 ? Said.Of("place.msg.nothingPlaced") : Built(string.Join(", ", list.Select(o => PieceCost.PieceName(o.Name)).Distinct()), Tool.Snapped)
                : Said.Of("place.msg.placedSave", list.Count));
        }
        Tool.NewLayout();
        Refresh();
    }

    /// <summary>What the status bar says when pieces were built: what, and what they snapped to.</summary>
    /// <param name="pieces">The pieces' names, joined.</param>
    /// <param name="snapped">What the last piece snapped to, if anything.</param>
    private static Said Built(string pieces, (PlaceTool.SnapWays Way, string Name)? snapped)
    {
        return snapped switch
        {
            null => Said.Of("place.msg.built", pieces),
            { Way: PlaceTool.SnapWays.OnTop } s => Said.Of("place.msg.builtOnTop", pieces, s.Name),
            { Way: PlaceTool.SnapWays.Beside } s => Said.Of("place.msg.builtBeside", pieces, s.Name),
            { } s => Said.Of("place.msg.builtSnapped", pieces, s.Name),
        };
    }

    /// <summary>
    /// Commits placements as new objects in one undo step; kinds built with a tool go in as player-built pieces.
    /// </summary>
    private static void Commit(EditSession session, List<PlaceTool.Placement> list, string label)
    {
        var adds = list.Select(o =>
        {
            int prefab = StableHash.Of(o.Name);
            return (new NewObject(0, prefab, o.Position, o.Rotation, o.Scale), PieceCatalog.Get(prefab)?.Tool != null);
        }).ToList();
        session.Commit(label, null, [], adds);
    }

    /// <summary>Enter: places what the line, grid or zone shows.</summary>
    public void PlaceShape()
    {
        if (_preview.Count == 0 || Session() is not { } session)
        {
            Message?.Invoke(Tool.Mode == PlaceTool.Modes.Line ? Said.Of("place.msg.drawLine") : Tool.Mode == PlaceTool.Modes.Zone ? Said.Of("place.msg.drawZone") : Said.Of("place.msg.dragBox"));
            return;
        }
        var list = _preview.ToList();
        string label = Tool.Mode == PlaceTool.Modes.Line ? Lang.T("place.undo.placedLine", list.Count)
            : Tool.Mode == PlaceTool.Modes.Zone ? Lang.T("place.undo.placedZone", list.Count) : Lang.T("place.undo.placedGrid", list.Count);
        Commit(session, list, label);
        Placed?.Invoke([.. list.Select(o => o.Name).Distinct()]);
        Message?.Invoke(Said.Of("place.msg.placed", list.Count));
        Refresh();
    }

    /// <summary>Removes the line's or zone's last point (Backspace).</summary>
    public void RemoveLastPoint()
    {
        if (Pointed && Tool.Points.Count > 0)
        {
            lock (Tool.Points) { Tool.Points.RemoveAt(Tool.Points.Count - 1); }
            Tool.Notify();
        }
    }

    /// <summary>The tool's keys; true when the key was one.</summary>
    public bool Key(Avalonia.Input.Key key, bool shift, bool ctrl)
    {
        if (ctrl)
        {
            return false;
        }
        // The Workshop turns pieces by its own step (Shift: by 1°).
        float step = TurnStep is float ts ? shift ? 1 : ts : shift ? 15 : 1;
        bool shape = Tool.Mode != PlaceTool.Modes.Brush;
        switch (key)
        {
            case Avalonia.Input.Key.PageUp:
            case Avalonia.Input.Key.PageDown:
                Message?.Invoke(Tool.NudgeElevation((key == Avalonia.Input.Key.PageUp ? 1 : -1) * (shift ? 0.1f : 0.5f)));
                return true;
            case Avalonia.Input.Key.R:
                Tool.NewLayout();
                return true;
            case Avalonia.Input.Key.OemComma:
            case Avalonia.Input.Key.OemPeriod:
            {
                float d = (key == Avalonia.Input.Key.OemComma ? -1 : 1) * step;
                if (Tool.Mode is PlaceTool.Modes.Zone or PlaceTool.Modes.Grid)
                {
                    if (!Tool.TurnShape(d))
                    {
                        Message?.Invoke(Tool.Mode == PlaceTool.Modes.Zone ? Said.Of("place.msg.turnZone") : Said.Of("place.msg.turnBox"));
                    }
                }
                else
                {
                    Tool.TurnBy(d);
                }
                return true;
            }
            case Avalonia.Input.Key.Enter when shape:
                PlaceShape();
                return true;
            case Avalonia.Input.Key.Escape when PickOnce != null:
                PickOnce = null;
                Message?.Invoke(Said.Of("view.msg.pickCancelled"));
                return true;
            case Avalonia.Input.Key.Escape when shape && (Tool.Points.Count > 0 || Tool.GridA != null || Tool.FigA != null):
                Tool.ClearShape();
                return true;
            case Avalonia.Input.Key.Back when Pointed && Tool.Points.Count > 0:
                RemoveLastPoint();
                return true;
        }
        return false;
    }
}
