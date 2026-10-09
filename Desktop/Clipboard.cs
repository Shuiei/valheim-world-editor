using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// What Copy keeps, like the web editor's clipboard (editor/area.js): the ground's shape relative to its
// average height (NaN: not copied), each point's weight (the soft edge), its paint (-1: not painted),
// the objects relative to the middle, and the outline. W × H grid points around the middle.
public sealed class CopyData
{
	public required int W { get; init; }
	public required int H { get; init; }
	public required float[] Rel { get; init; }
	public required float[] Wt { get; init; }
	public required float[] Pnt { get; init; }
	public required List<Obj> Objects { get; init; }
	public required List<Vector2> Poly { get; init; }
	public string? Name { get; set; }

	// An object: offsets from the middle (east, north) and from the reference height (or, Follow, from
	// the ground under it: objects copied with the Select tool), its turn and scale, and what it was
	// copied from (its data goes with it).
	public sealed record Obj(int Prefab, string Name, float Dx, float Dz, float Dy, Vector3 Rotation, float Scale, int? SourceId, bool Follow = false);

	// The ground and the shown objects inside an Area selection.
	public static CopyData? FromArea(AreaTool area, Ground g, WorldScene s, IReadOnlyList<int> objects, Func<int, string?> nameOf)
	{
		if (area.WeightsIn(g.W, g.H) is not { Cells.Count: > 0 } a)
		{
			return null;
		}
		float reference = a.Cells.Average(c => g.HeightOf(c.G));
		int w = a.X1 - a.X0 + 1, h = a.Z1 - a.Z0 + 1;
		var rel = Enumerable.Repeat(float.NaN, w * h).ToArray();
		var wt = new float[w * h];
		var pnt = Enumerable.Repeat(-1f, w * h * 4).ToArray();
		foreach (var (p, k) in a.Cells)
		{
			int gx = p % g.W, gz = p / g.W, i = (gz - a.Z0) * w + gx - a.X0;
			rel[i] = g.HeightOf(p) - reference;
			wt[i] = k;
			if (g.PMod[p] != 0)
			{
				Array.Copy(g.Paint, p * 4, pnt, i * 4, 4);
			}
		}
		float cx = (a.X0 + a.X1) / 2f, cz = (a.Z0 + a.Z1) / 2f, ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		var objs = objects.Select(i => s.Things[i]).Where(t => s.World == null || s.World.CanCreate(t.Prefab)).Select(t => new Obj(t.Prefab, nameOf(t.Prefab) ?? t.Prefab.ToString(),
			t.Position.X - ox - cx, t.Position.Z - oz - cz, t.Position.Y - reference, t.Rotation, t.Scale, SourceOf(s, t))).ToList();
		return new CopyData { W = w, H = h, Rel = rel, Wt = wt, Pnt = pnt, Objects = objs, Poly = a.Poly.Select(p => p - new Vector2(cx, cz)).ToList() };
	}

	// Objects only (Select tool's copy): each keeps its height above the ground where it lands.
	public static CopyData? FromThings(WorldScene s, IReadOnlyList<int> things, Func<int, string?> nameOf, Func<float, float, float> groundAt)
	{
		var list = things.Select(i => s.Things[i]).Where(t => !t.Gone && (s.World == null || s.World.CanCreate(t.Prefab))).ToList();
		if (list.Count == 0)
		{
			return null;
		}
		float cx = list.Average(t => t.Position.X), cz = list.Average(t => t.Position.Z);
		float x0 = list.Min(t => t.Position.X) - cx - 1, x1 = list.Max(t => t.Position.X) - cx + 1, z0 = list.Min(t => t.Position.Z) - cz - 1, z1 = list.Max(t => t.Position.Z) - cz + 1;
		var objs = list.Select(t => new Obj(t.Prefab, nameOf(t.Prefab) ?? t.Prefab.ToString(), t.Position.X - cx, t.Position.Z - cz,
			t.Position.Y - groundAt(t.Position.X, t.Position.Z), t.Rotation, t.Scale, SourceOf(s, t), Follow: true)).ToList();
		return new CopyData
		{
			W = 1, H = 1, Rel = new[] { float.NaN }, Wt = new float[1], Pnt = new[] { -1f, -1, -1, -1 }, Objects = objs,
			Poly = new() { new(x0, z0), new(x1, z0), new(x1, z1), new(x0, z1) },
		};
	}

	private static int? SourceOf(WorldScene s, WorldScene.Thing t) => t.Id >= 0 ? t.Id : s.Session?.Edits.FindAdded(t.Id)?.SourceId;
}

// Pasting a clip (the Paste tool): where it goes, turned (degrees, counter-clockwise seen from above)
// and mirrored, with a height offset and repeats (like WorldEdit's //stack).
public sealed class PasteTool
{
	public CopyData? Clip { get; set; }
	public float Turn { get; set; }
	public bool Mirror { get; set; }
	public bool Ground { get; set; } = true;
	public bool Objects { get; set; } = true;
	// Clear the site: where the pasted building stands, the ground in its way is dug down to its lowest
	// piece (a metre around it too), and the trees, rocks and the like there go (Site, for the caller).
	public bool ClearSite { get; set; } = true;
	// The grid points the last Apply cleared (its buildings' outlines and the metre around them).
	public HashSet<(int X, int Z)> Site { get; } = new();
	public float Offset { get; set; }
	public int Copies { get; set; } = 1;
	public enum Along { Width, Depth, Up }
	public Along Direction { get; set; } = Along.Width;
	public float Gap { get; set; }
	// Where the mouse is (grid point), for the preview.
	public Vector2? At { get; set; }
	public event Action? Changed;
	public void Notify() => Changed?.Invoke();

	public void TurnBy(float degrees)
	{
		Turn = ((Turn + degrees) % 360 + 360) % 360;
		Notify();
	}

	// Source offset (from the clip's middle) → offset where it lands.
	public Vector2 Xf(Vector2 d)
	{
		if (Mirror)
		{
			d.X = -d.X;
		}
		float t = Turn * MathF.PI / 180, c = MathF.Cos(t), s = MathF.Sin(t);
		return new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c);
	}

	public Vector2 Inv(Vector2 d)
	{
		float t = -Turn * MathF.PI / 180, c = MathF.Cos(t), s = MathF.Sin(t);
		d = new Vector2(d.X * c - d.Y * s, d.X * s + d.Y * c);
		if (Mirror)
		{
			d.X = -d.X;
		}
		return d;
	}

	// The step between two copies: the size of the copy plus the gap along the chosen direction.
	private (Vector2 D, float Up) Step()
	{
		var c = Clip!;
		if (Direction == Along.Up)
		{
			float y0 = c.Objects.Count > 0 ? c.Objects.Min(o => o.Dy) : 0, y1 = c.Objects.Count > 0 ? c.Objects.Max(o => o.Dy) : 0;
			return (Vector2.Zero, (c.Objects.Count > 0 ? MathF.Max(1, y1 - y0 + 1) : 2) + Gap);
		}
		float x0 = c.Poly.Min(p => p.X), x1 = c.Poly.Max(p => p.X), z0 = c.Poly.Min(p => p.Y), z1 = c.Poly.Max(p => p.Y);
		return Direction == Along.Width ? (new Vector2(x1 - x0 + Gap, 0), 0) : (new Vector2(0, z1 - z0 + Gap), 0);
	}

	// Copy k's anchor (grid point) and the height added to it.
	public (Vector2 At, float Up) CopyAt(Vector2 at, int k)
	{
		var (d, up) = Step();
		return (at + Xf(d * k), up * k);
	}

	public int Count => Math.Clamp(Copies, 1, 50);

	// Where the objects of every copy would land (grid x, height, grid z), and each extra copy's outline.
	public (List<Vector3> Objects, List<List<Vector2>> Outlines) Preview(Vector2 at, Func<Vector2, float> heightAt)
	{
		var objs = new List<Vector3>();
		var outlines = new List<List<Vector2>>();
		if (Clip is not { } c)
		{
			return (objs, outlines);
		}
		float baseH = heightAt(at) + Offset;
		for (int k = 0; k < Count; k++)
		{
			var (ca, up) = CopyAt(at, k);
			float anchor = (Direction == Along.Up ? baseH : heightAt(ca) + Offset) + up;
			foreach (var o in c.Objects)
			{
				var p = ca + Xf(new Vector2(o.Dx, o.Dz));
				objs.Add(new Vector3(p.X, o.Follow ? heightAt(p) + o.Dy + up : anchor + o.Dy, p.Y));
			}
			outlines.Add(c.Poly.Select(p => ca + Xf(p)).ToList());
		}
		return (objs, outlines);
	}

	// The objects a paste at grid point at would place (as Apply places them, the ground as it is now):
	// name, world position, rotation, scale; for drawing them as ghosts.
	public List<(int Prefab, Vector3 Position, Vector3 Rotation, float Scale)> Ghosts(Vector2 at, Func<Vector2, float> heightAt, float ox, float oz)
	{
		var list = new List<(int, Vector3, Vector3, float)>();
		if (Clip is not { } c || !Objects)
		{
			return list;
		}
		float baseH = heightAt(at) + Offset;
		for (int k = 0; k < Count; k++)
		{
			var (ca, up) = CopyAt(at, k);
			float anchor = (Direction == Along.Up ? baseH : heightAt(ca) + Offset) + up;
			foreach (var o in c.Objects)
			{
				var p = ca + Xf(new Vector2(o.Dx, o.Dz));
				float y = o.Follow ? heightAt(p) + o.Dy + up : anchor + o.Dy;
				var r = o.Rotation;
				list.Add((o.Prefab, new Vector3(ox + p.X, y, oz + p.Y), new Vector3(Mirror ? -r.X : r.X, (Mirror ? -r.Y : r.Y) - Turn, Mirror ? -r.Z : r.Z), o.Scale));
			}
		}
		return list;
	}

	// Pastes at a grid point: the ground (shape and paint, with the copy's soft edge) and the objects,
	// as new objects copied from the originals. Returns the ground points changed and their rectangle,
	// and the objects to add (Piece: a player-built piece).
	public (List<int> Touched, (int X0, int Z0, int X1, int Z1) Rect, List<(NewObject, bool)> Add) Apply(Ground g, Vector2 at, Func<int, float>? mask = null)
	{
		var c = Clip!;
		var touched = new List<int>();
		var add = new List<(NewObject, bool)>();
		Site.Clear();
		int half = (int)MathF.Ceiling(MathF.Sqrt(c.W * c.W + c.H * c.H) / 2) + 1;
		int bx0 = g.W, bx1 = 0, bz0 = g.H, bz1 = 0;
		float baseH = g.HeightOf(Index(g, at)) + Offset;
		float ox = g.X0 * 64 - 32, oz = g.Z0 * 64 - 32;
		bool up = Direction == Along.Up;
		for (int k = 0; k < Count; k++)
		{
			var (ca, lift) = CopyAt(at, k);
			float anchor = (up ? baseH : g.HeightOf(Index(g, ca)) + Offset) + lift;
			int x0 = Math.Max(1, (int)MathF.Floor(ca.X - half)), x1 = Math.Min(g.W - 2, (int)MathF.Ceiling(ca.X + half));
			int z0 = Math.Max(1, (int)MathF.Floor(ca.Y - half)), z1 = Math.Min(g.H - 2, (int)MathF.Ceiling(ca.Y + half));
			// Stacked upwards, only the first copy shapes the ground.
			if (Ground && !(up && k > 0))
			{
				(bx0, bx1, bz0, bz1) = (Math.Min(bx0, x0), Math.Max(bx1, x1), Math.Min(bz0, z0), Math.Max(bz1, z1));
				for (int gz = z0; gz <= z1; gz++)
				{
					for (int gx = x0; gx <= x1; gx++)
					{
						var src = Inv(new Vector2(gx - ca.X, gz - ca.Y));
						int ix = (int)MathF.Round(src.X + (c.W - 1) / 2f), iz = (int)MathF.Round(src.Y + (c.H - 1) / 2f);
						if (ix < 0 || iz < 0 || ix >= c.W || iz >= c.H)
						{
							continue;
						}
						int i = iz * c.W + ix;
						if (float.IsNaN(c.Rel[i]) || g.Locked(gx, gz))
						{
							continue;
						}
						int p = gz * g.W + gx;
						float w = c.Wt[i] * (mask?.Invoke(p) ?? 1);
						if (w <= 0)
						{
							continue;
						}
						float h = g.HeightOf(p);
						g.SetHeight(p, h + (anchor + c.Rel[i] - h) * w);
						if (c.Pnt[i * 4] >= 0)
						{
							if (g.PMod[p] == 0)
							{
								g.Paint[p * 4] = g.Paint[p * 4 + 1] = g.Paint[p * 4 + 2] = 0;
								g.Paint[p * 4 + 3] = 1;
								g.PMod[p] = 1;
							}
							for (int ch = 0; ch < 4; ch++)
							{
								g.Paint[p * 4 + ch] += (c.Pnt[i * 4 + ch] - g.Paint[p * 4 + ch]) * w;
							}
						}
						touched.Add(p);
					}
				}
			}
			if (Objects)
			{
				foreach (var o in c.Objects)
				{
					var p = ca + Xf(new Vector2(o.Dx, o.Dz));
					// Objects copied with the Select tool keep their height above the ground where they land.
					float y = o.Follow ? g.HeightOf(Index(g, p)) + o.Dy + lift : anchor + o.Dy;
					var r = o.Rotation;
					var rot = new Vector3(Mirror ? -r.X : r.X, (Mirror ? -r.Y : r.Y) - Turn, Mirror ? -r.Z : r.Z);
					add.Add((new NewObject(0, o.Prefab, new Vector3(ox + p.X, y, oz + p.Y), rot, o.Scale, o.SourceId, true), PieceCatalog.Get(o.Prefab)?.Tool != null));
				}
			}
		}
		if (ClearSite && Objects)
		{
			var (dug, rect) = Clear(g, add, mask);
			touched.AddRange(dug);
			if (dug.Count > 0)
			{
				(bx0, bx1, bz0, bz1) = (Math.Min(bx0, rect.X0), Math.Max(bx1, rect.X1), Math.Min(bz0, rect.Z0), Math.Max(bz1, rect.Z1));
			}
		}
		return (touched, (Math.Min(bx0, bx1) - 1, Math.Min(bz0, bz1) - 1, bx1 + 1, bz1 + 1), add);
	}

	// Each grid point under (or within a metre of) a pasted buildable piece: the ground there dug down
	// to the lowest such piece's bottom (its colliders, as the game has them), never raised; past the
	// game's ±8 m if need be (a building dug into a mountain).
	private (List<int> Dug, (int X0, int Z0, int X1, int Z1) Rect) Clear(Ground g, List<(NewObject O, bool Piece)> placed, Func<int, float>? mask)
	{
		float ox = g.X0 * 64 - 32, oz = g.Z0 * 64 - 32;
		var to = new Dictionary<(int, int), float>();
		foreach (var (o, _) in placed)
		{
			string? name = TerrainEditor.Terrain.PrefabCatalog.NameOf(o.Prefab);
			if (name == null || !Workshop.Buildable(name))
			{
				continue;
			}
			var pts = TerrainEditor.Editing.Hammer.Outline(name, TerrainEditor.App.BlueprintFormats.FromEuler(o.Rotation), o.Scale > 0 ? o.Scale : 1).ToList();
			if (pts.Count == 0)
			{
				continue;
			}
			float bottom = o.Position.Y + pts.Min(v => v.Y);
			int x0 = (int)MathF.Floor(o.Position.X - ox + pts.Min(v => v.X)) - 1, x1 = (int)MathF.Ceiling(o.Position.X - ox + pts.Max(v => v.X)) + 1;
			int z0 = (int)MathF.Floor(o.Position.Z - oz + pts.Min(v => v.Z)) - 1, z1 = (int)MathF.Ceiling(o.Position.Z - oz + pts.Max(v => v.Z)) + 1;
			for (int gz = Math.Max(1, z0); gz <= Math.Min(g.H - 2, z1); gz++)
			{
				for (int gx = Math.Max(1, x0); gx <= Math.Min(g.W - 2, x1); gx++)
				{
					to[(gx, gz)] = to.TryGetValue((gx, gz), out float was) ? MathF.Min(was, bottom) : bottom;
				}
			}
		}
		var dug = new List<int>();
		int rx0 = g.W, rx1 = 0, rz0 = g.H, rz1 = 0;
		bool limit = g.NoLimit;
		g.NoLimit = true;
		foreach (var ((gx, gz), y) in to)
		{
			Site.Add((gx, gz));
			int p = gz * g.W + gx;
			if (g.Locked(gx, gz) || (mask?.Invoke(p) ?? 1) <= 0 || g.HeightOf(p) <= y)
			{
				continue;
			}
			g.SetHeight(p, y);
			dug.Add(p);
			(rx0, rx1, rz0, rz1) = (Math.Min(rx0, gx), Math.Max(rx1, gx), Math.Min(rz0, gz), Math.Max(rz1, gz));
		}
		g.NoLimit = limit;
		return (dug, (rx0, rz0, rx1, rz1));
	}

	private static int Index(Ground g, Vector2 p) => Math.Clamp((int)MathF.Round(p.Y), 0, g.H - 1) * g.W + Math.Clamp((int)MathF.Round(p.X), 0, g.W - 1);
}
