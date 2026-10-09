using System.IO.Compression;
using System.Numerics;
using System.Text.Json;

namespace TerrainEditor.Editing;

// The game's hammer, as it places a piece (Player.UpdatePlacementGhost, PieceRayTest,
// FindClosestSnapPoints; Piece.GetSnapPoints), for the Workshop's Build: the build ray hits the nearest
// piece collider or the ground; the ghost, turned as asked, is put 50 m out along the hit's normal, then
// moved so its collider point nearest the hit touches it (ground pieces and "clip everything" pieces:
// their origin at the hit); then, of every snap point of the ghost and every snap point of the pieces
// within 10 m, the closest pair within 0.5 m meets, unless that puts it right where the same piece
// already stands. The pieces' colliders, snap points and flags come from the game
// (piece-place.json.gz, tools/asset-export/scan_piece_place.py). All in world space (Unity's axes).
public static class Hammer
{
	public const float SnapDistance = 0.5f, SnapRadius = 10f;

	public abstract record Shape;
	public sealed record Box(Vector3 C, Vector3 H, Quaternion Q) : Shape;
	public sealed record Sphere(Vector3 C, float R) : Shape;
	public sealed record Capsule(Vector3 A, Vector3 B, float R) : Shape;
	public sealed record Mesh(bool Convex, Vector3[] V, int[] I) : Shape;

	public sealed record Data(bool Ground, bool ClipGround, bool ClipEverything, bool RotatedOverlap, Vector3[] Snaps, Shape[] Colliders)
	{
		// The piece's reach from its origin (for the 10 m search).
		public float Radius { get; } = Colliders.Select(Reach).DefaultIfEmpty(0).Max();
	}

	private static float Reach(Shape s) => s switch
	{
		Box b => b.C.Length() + b.H.Length(),
		Sphere sp => sp.C.Length() + sp.R,
		Capsule c => MathF.Max(c.A.Length(), c.B.Length()) + c.R,
		Mesh m => m.V.Select(v => v.Length()).DefaultIfEmpty(0).Max(),
		_ => 0,
	};

	private static readonly Lazy<Dictionary<string, Data>> All = new(Load);

	private static Dictionary<string, Data> Load()
	{
		using Stream gz = typeof(Hammer).Assembly.GetManifestResourceStream("TerrainEditor.piece-place.json.gz")
			?? throw new InvalidOperationException("piece-place.json.gz is not embedded");
		using var s = new GZipStream(gz, CompressionMode.Decompress);
		using JsonDocument doc = JsonDocument.Parse(s);
		var result = new Dictionary<string, Data>();
		foreach (JsonProperty p in doc.RootElement.EnumerateObject())
		{
			var f = p.Value.GetProperty("f");
			bool Flag(string k) => f.TryGetProperty(k, out var v) && v.GetInt32() != 0;
			var snaps = p.Value.GetProperty("s").EnumerateArray().Select(a => new Vector3(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle())).ToArray();
			var shapes = new List<Shape>();
			foreach (var c in p.Value.GetProperty("c").EnumerateArray())
			{
				float F(int i) => c[i].GetSingle();
				switch (c[0].GetString())
				{
					case "b":
						shapes.Add(new Box(new(F(1), F(2), F(3)), new(F(4), F(5), F(6)), new(F(7), F(8), F(9), F(10))));
						break;
					case "s":
						shapes.Add(new Sphere(new(F(1), F(2), F(3)), F(4)));
						break;
					case "c":
						shapes.Add(new Capsule(new(F(1), F(2), F(3)), new(F(4), F(5), F(6)), F(7)));
						break;
					case "m":
						var v = c[2].EnumerateArray().Select(x => x.GetSingle()).ToArray();
						shapes.Add(new Mesh(c[1].GetInt32() != 0, Enumerable.Range(0, v.Length / 3).Select(i => new Vector3(v[i * 3], v[i * 3 + 1], v[i * 3 + 2])).ToArray(),
							c[3].EnumerateArray().Select(x => x.GetInt32()).ToArray()));
						break;
				}
			}
			result[p.Name] = new Data(Flag("g"), Flag("cg"), Flag("ce"), Flag("ro"), snaps, shapes.ToArray());
		}
		return result;
	}

	public static Data? Get(string prefab) => All.Value.GetValueOrDefault(prefab);

	// The points of a piece's colliders (box corners, mesh vertices, the bottoms and sides of spheres
	// and capsules), turned and scaled, from its origin; none for a piece the game gives no collider.
	public static IEnumerable<Vector3> Outline(string prefab, Quaternion rotation, float scale = 1)
	{
		if (Get(prefab) is not { } d)
		{
			yield break;
		}
		foreach (var c in d.Colliders)
		{
			IEnumerable<Vector3> pts = c switch
			{
				Box b => Enumerable.Range(0, 8).Select(i => b.C + Vector3.Transform(new Vector3((i & 1) == 0 ? -b.H.X : b.H.X, (i & 2) == 0 ? -b.H.Y : b.H.Y, (i & 4) == 0 ? -b.H.Z : b.H.Z), b.Q)),
				Sphere sp => Round(sp.C, sp.R),
				Capsule cp => Round(cp.A, cp.R).Concat(Round(cp.B, cp.R)),
				Mesh m => m.V,
				_ => Array.Empty<Vector3>(),
			};
			foreach (var v in pts)
			{
				yield return Vector3.Transform(v * scale, rotation);
			}
		}

		static IEnumerable<Vector3> Round(Vector3 c, float r) => new[] { Vector3.UnitX, -Vector3.UnitX, Vector3.UnitY, -Vector3.UnitY, Vector3.UnitZ, -Vector3.UnitZ }.Select(u => c + u * r);
	}

	// How far below its origin a piece reaches (its colliders' lowest point), turned and scaled; 0 for a
	// piece the game gives no collider.
	public static float Bottom(string prefab, Quaternion rotation, float scale = 1) =>
		Outline(prefab, rotation, scale).Select(v => v.Y).DefaultIfEmpty(0).Min();

	// A piece standing in the world: its kind, where, turned how.
	public sealed record Placed(int Index, string Prefab, Vector3 Position, Quaternion Rotation);

	// What the build ray hits first: a piece (its index) or the ground (null), the point and its normal.
	public sealed record Hit(int? Piece, Vector3 Point, Vector3 Normal);

	// PieceRayTest: the nearest of the pieces' colliders and the ground (groundT: where the ray meets the
	// ground, if it does).
	// maxY: nothing above it counts (the Workshop's cut hides it).
	public static Hit? Ray(Vector3 o, Vector3 d, IEnumerable<Placed> pieces, float? groundT, float maxY = float.MaxValue)
	{
		d = Vector3.Normalize(d);
		float best = groundT ?? float.MaxValue;
		Hit? hit = groundT is float g ? new Hit(null, o + d * g, Vector3.UnitY) : null;
		foreach (var p in pieces)
		{
			if (Get(p.Prefab) is not { } data)
			{
				continue;
			}
			var inv = Quaternion.Inverse(p.Rotation);
			// In the piece's frame.
			var lo = Vector3.Transform(o - p.Position, inv);
			var ld = Vector3.Transform(d, inv);
			foreach (var shape in data.Colliders)
			{
				if (RayShape(lo, ld, shape) is (float t, Vector3 n) && t >= 0 && t < best && o.Y + d.Y * t <= maxY + 1e-3f)
				{
					best = t;
					hit = new Hit(p.Index, o + d * t, Vector3.Normalize(Vector3.Transform(n, p.Rotation)));
				}
			}
		}
		return hit;
	}

	// UpdatePlacementGhost: where the piece goes, pointed at hit, turned by rotation (and lifted, the
	// editor's own Ctrl + wheel), then snapped (snap: the snap points' search is done). Returns the
	// position and what it snapped to (null: nothing).
	public static (Vector3 Position, int? SnappedTo) Place(string prefab, Quaternion rotation, Hit hit, IReadOnlyList<Placed> pieces, float lift = 0, bool snap = true)
	{
		var data = Get(prefab);
		Vector3 point = hit.Point, normal = hit.Normal;
		Vector3 pos;
		if (data == null || (data.Ground || data.ClipGround) && hit.Piece == null || data.ClipEverything)
		{
			pos = point;
		}
		else
		{
			// Out along the normal, then back until its nearest collider point touches the hit. Only the
			// colliders Unity's ClosestPoint works on: not concave meshes.
			var ghost = point + normal * 50f;
			Vector3? nearest = null;
			float nd = float.MaxValue;
			foreach (var shape in data.Colliders)
			{
				if (shape is Mesh { Convex: false })
				{
					continue;
				}
				var lp = Vector3.Transform(point - ghost, Quaternion.Inverse(rotation));
				var c = ghost + Vector3.Transform(Closest(shape, lp), rotation);
				float dist = Vector3.Distance(c, point);
				if (dist < nd)
				{
					nd = dist;
					nearest = c;
				}
			}
			pos = nearest is { } np ? point + (ghost - np) : point;
		}
		pos.Y += lift;
		if (!snap || data == null || data.Snaps.Length == 0)
		{
			return (pos, null);
		}
		// FindClosestSnapPoints: the pieces within 10 m (their colliders), every pair of snap points.
		float bestD = SnapDistance;
		Vector3? move = null;
		int? to = null;
		foreach (var p in pieces)
		{
			if (Get(p.Prefab) is not { Snaps.Length: > 0 } other || Vector3.Distance(p.Position, pos) - other.Radius > SnapRadius)
			{
				continue;
			}
			foreach (var b in other.Snaps)
			{
				var bw = p.Position + Vector3.Transform(b, p.Rotation);
				foreach (var a in data.Snaps)
				{
					var aw = pos + Vector3.Transform(a, rotation);
					float dist = Vector3.Distance(aw, bw);
					if (dist <= bestD)
					{
						bestD = dist;
						move = bw - aw;
						to = p.Index;
					}
				}
			}
		}
		if (move is not { } m)
		{
			return (pos, null);
		}
		var snapped = pos + m;
		// IsOverlappingOtherPiece: not right where the same piece stands (turned alike, unless it may overlap turned).
		bool overlaps = pieces.Any(p => p.Prefab == prefab && Vector3.Distance(p.Position, snapped) < 0.05f
			&& (!data.RotatedOverlap || AngleDegrees(p.Rotation, rotation) <= 10f));
		return overlaps ? (pos, null) : (snapped, to);
	}

	private static float AngleDegrees(Quaternion a, Quaternion b) => MathF.Acos(MathF.Min(1, MathF.Abs(Quaternion.Dot(a, b)))) * 2 * 180 / MathF.PI;

	// ---- Collider.ClosestPoint, in the piece's frame (p inside: p itself).
	internal static Vector3 Closest(Shape s, Vector3 p) => s switch
	{
		Box b => ClosestBox(b, p),
		Sphere sp => Vector3.Distance(p, sp.C) <= sp.R ? p : sp.C + Vector3.Normalize(p - sp.C) * sp.R,
		Capsule c => ClosestCapsule(c, p),
		Mesh m => ClosestHull(m, p),
		_ => p,
	};

	private static Vector3 ClosestBox(Box b, Vector3 p)
	{
		var l = Vector3.Transform(p - b.C, Quaternion.Inverse(b.Q));
		var c = Vector3.Clamp(l, -b.H, b.H);
		return b.C + Vector3.Transform(c, b.Q);
	}

	private static Vector3 ClosestCapsule(Capsule c, Vector3 p)
	{
		var seg = c.B - c.A;
		float t = seg.LengthSquared() < 1e-9f ? 0 : Math.Clamp(Vector3.Dot(p - c.A, seg) / seg.LengthSquared(), 0, 1);
		var axis = c.A + seg * t;
		return Vector3.Distance(p, axis) <= c.R ? p : axis + Vector3.Normalize(p - axis) * c.R;
	}

	// A convex mesh: the point itself when inside, else the nearest point of its triangles.
	private static Vector3 ClosestHull(Mesh m, Vector3 p)
	{
		Vector3 best = p;
		float bd = float.MaxValue;
		bool inside = true;
		var centre = m.V.Aggregate(Vector3.Zero, (a, v) => a + v) / Math.Max(1, m.V.Length);
		for (int i = 0; i + 2 < m.I.Length; i += 3)
		{
			Vector3 a = m.V[m.I[i]], b = m.V[m.I[i + 1]], c = m.V[m.I[i + 2]];
			var q = ClosestTriangle(p, a, b, c);
			float d = Vector3.DistanceSquared(p, q);
			if (d < bd)
			{
				bd = d;
				best = q;
			}
			// Outside when beyond any face (its normal turned away from the middle).
			var n = Vector3.Cross(b - a, c - a);
			if (Vector3.Dot(n, centre - a) > 0)
			{
				n = -n;
			}
			if (Vector3.Dot(n, p - a) > 1e-5f)
			{
				inside = false;
			}
		}
		return inside ? p : best;
	}

	// Ericson, Real-Time Collision Detection 5.1.5.
	internal static Vector3 ClosestTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
	{
		Vector3 ab = b - a, ac = c - a, ap = p - a;
		float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
		if (d1 <= 0 && d2 <= 0)
		{
			return a;
		}
		var bp = p - b;
		float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
		if (d3 >= 0 && d4 <= d3)
		{
			return b;
		}
		float vc = d1 * d4 - d3 * d2;
		if (vc <= 0 && d1 >= 0 && d3 <= 0)
		{
			return a + ab * (d1 / (d1 - d3));
		}
		var cp = p - c;
		float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
		if (d6 >= 0 && d5 <= d6)
		{
			return c;
		}
		float vb = d5 * d2 - d1 * d6;
		if (vb <= 0 && d2 >= 0 && d6 <= 0)
		{
			return a + ac * (d2 / (d2 - d6));
		}
		float va = d3 * d6 - d5 * d4;
		if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
		{
			return b + (c - b) * ((d4 - d3) / (d4 - d3 + (d5 - d6)));
		}
		float denom = 1 / (va + vb + vc);
		return a + ab * (vb * denom) + ac * (vc * denom);
	}

	// ---- Raycasts, in the piece's frame: the distance along d (unit length) and the surface's normal.
	private static (float, Vector3)? RayShape(Vector3 o, Vector3 d, Shape s) => s switch
	{
		Box b => RayBox(o, d, b),
		Sphere sp => RaySphere(o, d, sp.C, sp.R),
		Capsule c => RayCapsule(o, d, c),
		Mesh m => RayMesh(o, d, m),
		_ => null,
	};

	private static (float, Vector3)? RayBox(Vector3 o, Vector3 d, Box b)
	{
		var inv = Quaternion.Inverse(b.Q);
		var lo = Vector3.Transform(o - b.C, inv);
		var ld = Vector3.Transform(d, inv);
		float tmin = float.MinValue, tmax = float.MaxValue;
		Vector3 n = Vector3.Zero;
		for (int k = 0; k < 3; k++)
		{
			float oo = k == 0 ? lo.X : k == 1 ? lo.Y : lo.Z, dd = k == 0 ? ld.X : k == 1 ? ld.Y : ld.Z, h = k == 0 ? b.H.X : k == 1 ? b.H.Y : b.H.Z;
			if (MathF.Abs(dd) < 1e-9f)
			{
				if (oo < -h || oo > h)
				{
					return null;
				}
				continue;
			}
			float t1 = (-h - oo) / dd, t2 = (h - oo) / dd;
			float sign = -1;
			if (t1 > t2)
			{
				(t1, t2) = (t2, t1);
				sign = 1;
			}
			if (t1 > tmin)
			{
				tmin = t1;
				n = k == 0 ? new Vector3(sign, 0, 0) : k == 1 ? new Vector3(0, sign, 0) : new Vector3(0, 0, sign);
			}
			tmax = MathF.Min(tmax, t2);
			if (tmin > tmax)
			{
				return null;
			}
		}
		return tmin < 0 ? null : (tmin, Vector3.Transform(n, b.Q));
	}

	private static (float, Vector3)? RaySphere(Vector3 o, Vector3 d, Vector3 c, float r)
	{
		var m = o - c;
		float b = Vector3.Dot(m, d), cc = Vector3.Dot(m, m) - r * r;
		if (cc > 0 && b > 0)
		{
			return null;
		}
		float disc = b * b - cc;
		if (disc < 0)
		{
			return null;
		}
		float t = -b - MathF.Sqrt(disc);
		if (t < 0)
		{
			return null;
		}
		return (t, Vector3.Normalize(o + d * t - c));
	}

	private static (float, Vector3)? RayCapsule(Vector3 o, Vector3 d, Capsule c)
	{
		// Marched: close enough for a cursor, and capsules are rare among pieces.
		float t = 0;
		for (int i = 0; i < 64 && t < 1000; i++)
		{
			var p = o + d * t;
			var q = ClosestCapsule(c with { R = 0 }, p);
			float dist = Vector3.Distance(p, q) - c.R;
			if (dist < 1e-3f)
			{
				return (t, Vector3.Normalize(p - q));
			}
			t += dist;
		}
		return null;
	}

	// Möller–Trumbore over the triangles, both sides (Unity's mesh colliders are hit from outside).
	private static (float, Vector3)? RayMesh(Vector3 o, Vector3 d, Mesh m)
	{
		(float, Vector3)? best = null;
		for (int i = 0; i + 2 < m.I.Length; i += 3)
		{
			Vector3 a = m.V[m.I[i]], b = m.V[m.I[i + 1]], c = m.V[m.I[i + 2]];
			Vector3 e1 = b - a, e2 = c - a, pv = Vector3.Cross(d, e2);
			float det = Vector3.Dot(e1, pv);
			if (MathF.Abs(det) < 1e-9f)
			{
				continue;
			}
			float inv = 1 / det;
			var tv = o - a;
			float u = Vector3.Dot(tv, pv) * inv;
			if (u < 0 || u > 1)
			{
				continue;
			}
			var qv = Vector3.Cross(tv, e1);
			float v = Vector3.Dot(d, qv) * inv;
			if (v < 0 || u + v > 1)
			{
				continue;
			}
			float t = Vector3.Dot(e2, qv) * inv;
			if (t >= 0 && (best == null || t < best.Value.Item1))
			{
				var n = Vector3.Normalize(Vector3.Cross(e1, e2));
				best = (t, Vector3.Dot(n, d) > 0 ? -n : n);
			}
		}
		return best;
	}
}
