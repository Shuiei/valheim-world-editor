using System.Numerics;
using Avalonia;
using Avalonia.Threading;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The Place tool's mouse and keys (the web editor's plant.js handlers). Brush: a click places what
// the preview shows, a drag paints more (Shift + drag removes the chosen kinds); each stroke is one
// undo step. Line, Zone: click points (or hold and drag to draw freely), drag a point to move it, the
// line to add one, Ctrl + click to remove one; Circle and Rectangle: press and drag; Grid: drag a box;
// Enter (or a double-click on the last point) places.
public sealed class PlaceInput
{
	private readonly GlView _view;
	public PlaceTool Tool { get; }
	public Func<EditSession?> Session { get; set; } = () => null;
	public event Action<string>? Message;
	// Placed: the kinds, for the recent list.
	public event Action<IReadOnlyList<string>>? Placed;

	public PlaceInput(GlView view, PlaceTool tool)
	{
		_view = view;
		Tool = tool;
		_timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
		_timer.Tick += (_, _) => Paint();
		tool.Changed += Refresh;
	}

	// What is drawn: the preview (or what a stroke has placed so far), as copies for the drawing thread.
	public PlaceTool.Placement[] Shown { get; private set; } = Array.Empty<PlaceTool.Placement>();
	private List<PlaceTool.Placement> _preview = new();
	public IReadOnlyList<PlaceTool.Placement> PreviewNow => _preview;
	private Vector2? _at;
	private Point? _pointer;
	private Size _size;
	private bool _shift;

	// The cursor's grid point changed, or a setting: the preview is worked out again.
	public void Refresh()
	{
		if (_stroke != null)
		{
			return;
		}
		int? under = Tool.OnTop && _pointer is Point p ? _view.ObjectAt(p, _size) : null;
		_preview = _view.Mode == ToolMode.Place && !(Tool.Mode == PlaceTool.Modes.Brush && _shift) ? Tool.Preview(_at, under) : new();
		Shown = _preview.ToArray();
		_view.LassoChanged();
		Changed?.Invoke();
	}
	public event Action? Changed;

	public void ShiftHeld(bool on)
	{
		if (_shift != on)
		{
			_shift = on;
			Refresh();
		}
	}

	// ---- Brush strokes.
	private sealed class Stroke
	{
		public bool Erase;
		public Point From;
		public bool Dragging;
		public List<PlaceTool.Placement> Stamp = new();
		public List<PlaceTool.Placement> Added = new();
		public HashSet<int> Removed = new();
		public long Last;
	}

	private Stroke? _stroke;
	private readonly DispatcherTimer _timer;
	private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

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
		s.Added.AddRange(Tool.PaintStep(at, dt, s.Added));
		Shown = s.Added.ToArray();
		_view.LassoChanged();
	}

	// ---- Shapes.
	private (Point At, Vector2 Hit, bool Drag)? _press;
	private int? _dragPt;

	private bool Editable => Tool.Mode == PlaceTool.Modes.Line && Tool.LineShape == PlaceTool.LineShapes.Points || Tool.Mode == PlaceTool.Modes.Zone;
	private bool Pointed => Tool.Mode is PlaceTool.Modes.Line or PlaceTool.Modes.Zone;

	// The points where they are drawn (a zone can be turned: its points are kept unturned).
	public List<Vector2> ShownPoints()
	{
		lock (Tool.Points)
		{
			return Tool.Mode == PlaceTool.Modes.Zone ? Tool.Points.Select(Tool.Xf).ToList() : Tool.Points.ToList();
		}
	}

	private Point? Screen(Vector2 g) => _view.ScreenOfGrid(g, 0.3f);

	private int PointAt(Point at)
	{
		int best = -1;
		double bd = 10;
		var pts = ShownPoints();
		for (int i = 0; i < pts.Count; i++)
		{
			if (Screen(pts[i]) is { } s && Point.Distance(s, at) < bd)
			{
				bd = Point.Distance(s, at);
				best = i;
			}
		}
		return best;
	}

	// The stretch of the outline under the mouse: the index of the point it starts from, or -1.
	private int SegmentAt(Point at)
	{
		var dense = Tool.Mode == PlaceTool.Modes.Zone
			? ShownPoints() is { Count: > 0 } z ? z.Append(z[0]).Select((p, i) => (P: p, Seg: i)).ToList() : new()
			: Tool.LineCurve();
		int best = -1;
		double bd = 8;
		for (int i = 1; i < dense.Count; i++)
		{
			if (Screen(dense[i - 1].P) is not { } a || Screen(dense[i].P) is not { } b)
			{
				continue;
			}
			double dx = b.X - a.X, dy = b.Y - a.Y, l2 = Math.Max(dx * dx + dy * dy, 1e-9);
			double t = Math.Clamp(((at.X - a.X) * dx + (at.Y - a.Y) * dy) / l2, 0, 1);
			double d = Point.Distance(at, new Point(a.X + dx * t, a.Y + dy * t));
			if (d < bd)
			{
				bd = d;
				best = dense[i - 1].Seg;
			}
		}
		return best;
	}

	// Pick from world: the next click takes the kind of the object clicked.
	public Action<string>? PickOnce { get; set; }

	public void Down(Point at, Size size, bool shift, bool ctrl, bool alt, int clicks)
	{
		_pointer = at;
		_size = size;
		if (PickOnce is { } pick)
		{
			PickOnce = null;
			if (_view.ObjectAt(at, size) is int i && _view.Scene is { } sc && Tool.NameOf(sc.Things[i].Prefab) is string name)
			{
				pick(name);
			}
			else
			{
				Message?.Invoke("No object there: pick again from the panel.");
			}
			return;
		}
		var hit = _view.GridAt(at, size);
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
			_stroke = new Stroke { Erase = shift, From = at, Stamp = _preview.ToList(), Last = _clock.ElapsedMilliseconds };
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

	public void Moved(Point at, Size size)
	{
		_pointer = at;
		_size = size;
		var hit = _view.GridAt(at, size);
		_at = hit;
		if (_stroke is { } s)
		{
			if (!s.Dragging && Point.Distance(at, s.From) > 6)
			{
				s.Dragging = true;
				// Painting: the ground under the click gets the preview too, then the brush adds more.
				if (!s.Erase)
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
		if (_press is { } p && Tool.Mode == PlaceTool.Modes.Line && Tool.LineShape != PlaceTool.LineShapes.Points)
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
				Message?.Invoke(Tool.LineShape == PlaceTool.LineShapes.Circle ? "Press at the centre and drag out to the size." : "Press at a corner and drag to the opposite one.");
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

	private void EndStroke(Stroke s)
	{
		_view.Select(Array.Empty<int>());
		if (Session() is not { } session)
		{
			return;
		}
		if (s.Erase)
		{
			if (s.Removed.Count > 0)
			{
				session.Commit($"Place: removed {s.Removed.Count}", null, s.Removed, Array.Empty<(NewObject, bool)>());
			}
			Message?.Invoke($"Removed {s.Removed.Count} object(s).");
		}
		else
		{
			var list = s.Dragging ? s.Added : s.Stamp;
			if (list.Count > 0)
			{
				var names = list.Select(o => o.Name).Distinct().ToList();
				Commit(session, list, $"Placed {list.Count} ({string.Join(", ", names.Take(3))})");
				Placed?.Invoke(names);
			}
			Message?.Invoke($"Placed {list.Count} object(s). Ctrl+Z removes them; Save writes them to the world.");
		}
		Tool.NewLayout();
		Refresh();
	}

	private static void Commit(EditSession session, List<PlaceTool.Placement> list, string label)
	{
		var adds = list.Select(o =>
		{
			int prefab = StableHash.Of(o.Name);
			return (new NewObject(0, prefab, o.Position, o.Rotation, o.Scale), PieceCatalog.Get(prefab)?.Tool != null);
		}).ToList();
		session.Commit(label, null, Array.Empty<int>(), adds);
	}

	// Enter: places what the line, grid or zone shows.
	public void PlaceShape()
	{
		if (_preview.Count == 0 || Session() is not { } session)
		{
			Message?.Invoke(Tool.Mode == PlaceTool.Modes.Line ? "Draw a line first (click points on the ground)." : Tool.Mode == PlaceTool.Modes.Zone ? "Draw a zone first (click points around it, or drag)." : "Drag a box on the ground first.");
			return;
		}
		var list = _preview.ToList();
		string where = Tool.Mode == PlaceTool.Modes.Line ? "along a line" : Tool.Mode == PlaceTool.Modes.Zone ? "in a zone" : "in a grid";
		Commit(session, list, $"Placed {list.Count} {where}");
		Placed?.Invoke(list.Select(o => o.Name).Distinct().ToList());
		Message?.Invoke($"Placed {list.Count} object(s). Ctrl+Z removes them.");
		Refresh();
	}

	public void RemoveLastPoint()
	{
		if (Pointed && Tool.Points.Count > 0)
		{
			lock (Tool.Points) { Tool.Points.RemoveAt(Tool.Points.Count - 1); }
			Tool.Notify();
		}
	}

	// The tool's keys; true when the key was one.
	public bool Key(Avalonia.Input.Key key, bool shift, bool ctrl)
	{
		if (ctrl)
		{
			return false;
		}
		float step = shift ? 15 : 1;
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
						Message?.Invoke(Tool.Mode == PlaceTool.Modes.Zone ? "Draw a zone first, then turn it." : "Drag a box first, then turn it.");
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
