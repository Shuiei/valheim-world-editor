using System.Numerics;
using Avalonia;
using Avalonia.Threading;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// The Select tool, like the web editor's (editor/transform.js, editor/selecting.js): click objects to
// select them (Shift adds), drag on empty ground to draw a zone and select what is in it, drag a
// selected object to move the selection, turn it (, .), lift it (PgUp PgDn), drop it onto what is
// under it (End), type an exact place, and delete it (Del). A move is shown live; when it ends the
// originals are replaced by copies at the new place (EditSession.Move), in one undo step.
public sealed class SelectTool
{
	private readonly GlView _view;
	private const float D = MathF.PI / 180;

	public SelectTool(GlView view)
	{
		_view = view;
		_commitTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
		_commitTimer.Tick += (_, _) => Commit();
	}

	// What to tell the user ("" clears the line; warn: shown as a problem).
	public event Action<string, bool>? Message;
	// Put each moved object on the ground where it ends up; snap building pieces to the pieces around.
	public bool OnGround { get; set; } = true;
	public bool SnapToPieces { get; set; } = true;

	private WorldScene? Scene => _view.Scene;
	private EditSession? Session => _view.Scene?.Session;

	// ---- Where things are: world coordinates (x east, z north), heights in metres.
	private float GroundAt(float wx, float wz)
	{
		var s = Scene!;
		return Picking.HeightAt(s, wx - s.Cx, -(wz - s.Cz));
	}

	private static bool IsPiece(WorldScene.Thing t) => t.Piece || PieceCatalog.Get(t.Prefab) != null;

	// Unity's rotation (Euler degrees: z, then x, then y) of a local point.
	private static Vector3 Rotate(Vector3 p, Vector3 r)
	{
		float cz = MathF.Cos(r.Z * D), sz = MathF.Sin(r.Z * D), cx = MathF.Cos(r.X * D), sx = MathF.Sin(r.X * D), cy = MathF.Cos(r.Y * D), sy = MathF.Sin(r.Y * D);
		float x = p.X, y = p.Y, z = p.Z;
		(x, y) = (x * cz - y * sz, x * sz + y * cz);
		(y, z) = (y * cx - z * sx, y * sx + z * cx);
		return new Vector3(x * cy + z * sy, y, -x * sy + z * cy);
	}

	private static IEnumerable<Vector3> SnapsAt(int prefab, Vector3 at, Vector3 rot) =>
		(PieceCatalog.Get(prefab)?.Snaps ?? Array.Empty<float[]>()).Select(p => at + Rotate(new Vector3(p[0], p[1], p[2]), rot));

	// ---- A move in progress.
	private sealed class Item
	{
		public int Index;
		public WorldScene.Thing Thing;
		// Height above the ground; a piece's bottom below its origin (from its snap points).
		public float Lift, Bottom;
		public bool Piece;
	}

	private sealed class Move
	{
		public required List<Item> Items;
		public float Cx, Cz, Dx, Dz, Dy, Turn, Sx, Sy, Sz, Block;
		public bool Dropped, NoSnap;
		public string? Snapped;
		public List<(Vector3 P, string Name)>? Others;
	}

	private Move? _move;
	private readonly DispatcherTimer _commitTimer;
	public bool Moving => _move != null;

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
		foreach (int i in _view.Selected)
		{
			var t = s.Things[i];
			if (t.Gone)
			{
				continue;
			}
			var snaps = PieceCatalog.Get(t.Prefab)?.Snaps;
			items.Add(new Item
			{
				Index = i, Thing = t, Lift = t.Position.Y - GroundAt(t.Position.X, t.Position.Z),
				Bottom = snaps is { Length: > 0 } ? snaps.Min(p => p[1]) : 0, Piece = IsPiece(t),
			});
		}
		if (items.Count == 0)
		{
			return null;
		}
		return _move = new Move { Items = items, Cx = items.Average(it => it.Thing.Position.X), Cz = items.Average(it => it.Thing.Position.Z) };
	}

	// Where an item ends up: turned around the selection's middle (clockwise from above, like Unity's
	// yaw), moved, and on the ground there (or as high above it as before). Pieces on the ground move
	// as one block (see GroundBlock).
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

	// On the ground, the selected building pieces keep their shape: the whole block goes up or down
	// until its bottom layer sits on the ground (sinking into a slope rather than floating).
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

	// Snapping: the closest pair of snap points (one of a moved piece, one of a piece around) within
	// 0.75 m; the whole selection shifts so they meet.
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
			m.Others = new();
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

	// Ends a move: the originals are replaced by copies at their new places, and those get selected.
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

	// Puts a move back (Esc).
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

	private void Later()
	{
		_commitTimer.Stop();
		_commitTimer.Start();
	}

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

	// End: each selected object drops onto whatever is under it: the top of another object, or else
	// the ground.
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

	// Moves the selection there (by: by these amounts), as one undo step; typed values are not snapped.
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
		_view.Select(Array.Empty<int>());
		Message?.Invoke($"Deleted {sel.Count} object(s). Ctrl+Z brings them back; they leave the world when it is saved.", false);
	}

	// ---- More ways to select.
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

	// Every building piece connected to the selected ones through pieces that touch (boxes grown a little).
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
		const float cell = 4;
		var grid = new Dictionary<(int, int, int), List<int>>();
		IEnumerable<(int, int, int)> Cells((Vector3 Min, Vector3 Max) b)
		{
			for (int x = (int)MathF.Floor(b.Min.X / cell); x <= (int)MathF.Floor(b.Max.X / cell); x++)
				for (int y = (int)MathF.Floor(b.Min.Y / cell); y <= (int)MathF.Floor(b.Max.Y / cell); y++)
					for (int z = (int)MathF.Floor(b.Min.Z / cell); z <= (int)MathF.Floor(b.Max.Z / cell); z++)
						yield return (x, y, z);
		}
		foreach (var (i, b) in boxes)
		{
			foreach (var c in Cells(b))
			{
				(grid.TryGetValue(c, out var l) ? l : grid[c] = new()).Add(i);
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
				foreach (int o in grid.GetValueOrDefault(c) ?? new())
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
	private Gizmo.Handle? _hover;
	private (Gizmo.Handle Handle, Vector3 Centre, float Start, float Dx, float Dz, float Dy, float Turn)? _handleDrag;
	public Gizmo.Handle? HotHandle => _handleDrag?.Handle ?? _hover;

	private Gizmo.Handle? HandleAt(Point at, Size size)
	{
		if (_view.GizmoAt() is not var (c, scale))
		{
			return null;
		}
		var (o, d) = _view.RayAt(at, size);
		return Gizmo.Hit(o, d, c, scale);
	}

	// The pointer is over a move handle (a click there drags, it does not pick).
	public bool OverHandle => _hover != null;

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
	private (Point At, Vector3 From)? _drag;
	private bool _dragMoving;
	private (Point At, List<Vector2> Points, bool Add, bool Drawing)? _lasso;
	// A copy of the zone's points for the drawing thread.
	public Vector2[]? Lasso { get; private set; }

	public void Down(Point at, Size size, bool shift, bool alt, int clicks)
	{
		var s = Scene;
		if (s == null)
		{
			return;
		}
		// A handle: drag along its axis, or around the ring.
		if (HandleAt(at, size) is { } handle && _view.GizmoAt() is var (c, _))
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
		if (clicks >= 2 && hit is int dbl && IsPiece(s.Things[dbl]))
		{
			WholeBuilding(new[] { dbl });
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
		_view.Pick(at, size, shift);
	}

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
			Lasso = l.Points.ToArray();
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
					_view.Select(Array.Empty<int>());
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
			_view.Pick(at, size, shift);
		}
	}

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

	// The tool's keys; true when the key was one.
	public bool Key(Avalonia.Input.Key key, bool shift, bool ctrl)
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
