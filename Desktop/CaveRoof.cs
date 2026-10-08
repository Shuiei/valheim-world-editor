using System.Numerics;
using System.Reflection;
using System.Text.Json;

namespace TerrainEditor.Desktop;

// A cave's roof of the game's boulders (the Path tool's Cave): each boulder upside down, turned, tilted
// and sized at random, and hung by its real underside (the game's mesh, cave-rocks.json) so it clears the headroom
// over the whole floor beneath it. Boulders are added until the floor and the foot of the walls are
// covered, then the walls are raised into the rock wherever its underside is above them: no daylight
// at the sides.
public static class CaveRoof
{
	private sealed record Shape(Vector3[] V, int[] T);

	private static readonly Lazy<Dictionary<string, Shape>> Shapes = new(Load);

	// A boulder of the roof: world position, Unity Euler rotation (degrees), scale, prefab.
	public sealed record Rock(string Prefab, Vector3 Position, Vector3 Rotation, float Scale);

	// The roof's boulders, and the walls' new heights (grid point → height it is raised to, at least).
	public sealed record Result(List<Rock> Rocks, Dictionary<int, float> Seal, int Uncovered);

	private static Dictionary<string, Shape> Load()
	{
		using Stream s = typeof(TerrainEditor.Terrain.PrefabCatalog).Assembly.GetManifestResourceStream("TerrainEditor.cave-rocks.json")
			?? throw new InvalidOperationException("cave-rocks.json is not embedded");
		using JsonDocument doc = JsonDocument.Parse(s);
		var shapes = new Dictionary<string, Shape>();
		foreach (JsonProperty p in doc.RootElement.EnumerateObject())
		{
			var v = p.Value.GetProperty("v").EnumerateArray().Select(a => new Vector3(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle())).ToArray();
			var t = p.Value.GetProperty("t").EnumerateArray().Select(a => a.GetInt32()).ToArray();
			shapes[p.Name] = new Shape(v, t);
		}
		return shapes;
	}

	// The rock4 boulders share one model (coast, forest, heath).
	private static Shape ShapeOf(string prefab) => Shapes.Value[prefab.StartsWith("rock4_", StringComparison.Ordinal) ? "rock4_forest" : prefab];

	// The underside of a boulder (its pivot at height 0) over the grid: the lowest point of the model
	// above each grid point it covers. (px, pz): the pivot in grid coordinates.
	internal static Dictionary<int, float> Underside(string prefab, float px, float pz, Vector3 rotation, float scale, int w, int h)
	{
		var shape = ShapeOf(prefab);
		// Quaternion.Euler in Unity applies z, then x, then y: the same as yaw/pitch/roll here.
		var q = Quaternion.CreateFromYawPitchRoll(rotation.Y * MathF.PI / 180, rotation.X * MathF.PI / 180, rotation.Z * MathF.PI / 180);
		var v = shape.V.Select(p => Vector3.Transform(p * scale, q) + new Vector3(px, 0, pz)).ToArray();
		var low = new Dictionary<int, float>();
		for (int k = 0; k < shape.T.Length; k += 3)
		{
			Vector3 a = v[shape.T[k]], b = v[shape.T[k + 1]], c = v[shape.T[k + 2]];
			float d = (b.Z - c.Z) * (a.X - c.X) + (c.X - b.X) * (a.Z - c.Z);
			if (MathF.Abs(d) < 1e-6f)
			{
				continue;
			}
			int x0 = Math.Max(0, (int)MathF.Ceiling(MathF.Min(a.X, MathF.Min(b.X, c.X)))), x1 = Math.Min(w - 1, (int)MathF.Floor(MathF.Max(a.X, MathF.Max(b.X, c.X))));
			int z0 = Math.Max(0, (int)MathF.Ceiling(MathF.Min(a.Z, MathF.Min(b.Z, c.Z)))), z1 = Math.Min(h - 1, (int)MathF.Floor(MathF.Max(a.Z, MathF.Max(b.Z, c.Z))));
			for (int z = z0; z <= z1; z++)
			{
				for (int x = x0; x <= x1; x++)
				{
					float l1 = ((b.Z - c.Z) * (x - c.X) + (c.X - b.X) * (z - c.Z)) / d, l2 = ((c.Z - a.Z) * (x - c.X) + (a.X - c.X) * (z - c.Z)) / d, l3 = 1 - l1 - l2;
					if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f)
					{
						continue;
					}
					float y = l1 * a.Y + l2 * b.Y + l3 * c.Y;
					int p = z * w + x;
					if (!low.TryGetValue(p, out float old) || y < old)
					{
						low[p] = y;
					}
				}
			}
		}
		return low;
	}

	// The roof over a cave (grid w × h, ox/oz the grid's world corner). rockAt: the boulders for a
	// world point.
	public static Result Build(PathTool path, PathTool.CavePlan c, int w, int h, float ox, float oz, Func<float, float, PathTool.CaveRock> rockAt)
	{
		var rnd = new Random(path.Seed);
		int n = c.Curve.Count;
		var rocks = new List<Rock>();
		var seal = new Dictionary<int, float>();
		if (n < 2)
		{
			return new Result(rocks, seal, 0);
		}
		float half = path.Width / 2, cover = half + path.Soft * 0.6f, reach = half + path.Soft + 3;
		// Each grid point near the roofed stretch: its distance from the line and the ceiling above it.
		var near = new Dictionary<int, (float Dist, float Ceiling, bool Roofed)>();
		int gx0 = Math.Max(0, (int)MathF.Floor(c.Curve.Min(q => q.P.X) - reach)), gx1 = Math.Min(w - 1, (int)MathF.Ceiling(c.Curve.Max(q => q.P.X) + reach));
		int gz0 = Math.Max(0, (int)MathF.Floor(c.Curve.Min(q => q.P.Y) - reach)), gz1 = Math.Min(h - 1, (int)MathF.Ceiling(c.Curve.Max(q => q.P.Y) + reach));
		for (int z = gz0; z <= gz1; z++)
		{
			for (int x = gx0; x <= gx1; x++)
			{
				float best = float.MaxValue, ceiling = 0, depth = 0;
				for (int i = 1; i < n; i++)
				{
					Vector2 a = c.Curve[i - 1].P, d = c.Curve[i].P - a;
					float t = Math.Clamp(((x - a.X) * d.X + (z - a.Y) * d.Y) / MathF.Max(1e-9f, d.LengthSquared()), 0, 1);
					float dist = Vector2.Distance(new Vector2(x, z), a + d * t);
					if (dist < best)
					{
						best = dist;
						ceiling = c.Floor[i - 1] + (c.Floor[i] - c.Floor[i - 1]) * t + path.Headroom;
						depth = c.Depth[i - 1] + (c.Depth[i] - c.Depth[i - 1]) * t;
					}
				}
				if (best <= reach)
				{
					near[z * w + x] = (best, ceiling, depth >= path.Headroom + 1.5f);
				}
			}
		}
		// What must be under rock: the floor and the foot of the walls along the roofed stretch.
		var must = near.Where(kv => kv.Value.Roofed && kv.Value.Dist <= cover).Select(kv => kv.Key).ToHashSet();
		var covered = new HashSet<int>();
		var underside = new Dictionary<int, float>();
		void Place(float px, float pz)
		{
			var kind = rockAt(ox + px, oz + pz);
			var (prefab, footprint, _) = PathTool.RoofRocks[kind == PathTool.CaveRock.Auto ? PathTool.CaveRock.Forest : kind];
			float scale = Math.Clamp((path.Width + path.Soft * 2 + 4) / (footprint * 0.8f), 0.25f, 2.5f) * (0.8f + 0.5f * (float)rnd.NextDouble());
			// Upside down: the boulder's flat dome (the part the game shows above ground) is the ceiling,
			// nearly level across the cave; its deep keel stands up out of the ground like an outcrop.
			var rot = new Vector3(180 + ((float)rnd.NextDouble() - 0.5f) * 16, (float)rnd.NextDouble() * 360, ((float)rnd.NextDouble() - 0.5f) * 16);
			var low = Underside(prefab, px, pz, rot, scale, w, h);
			// High enough that its underside clears the ceiling everywhere over the cave's floor.
			float y = float.MinValue;
			foreach (var (p, u) in low)
			{
				if (near.TryGetValue(p, out var q) && q.Dist <= half + 0.5f)
				{
					y = MathF.Max(y, q.Ceiling - u);
				}
			}
			if (y == float.MinValue)
			{
				return;
			}
			rocks.Add(new Rock(prefab, new Vector3(ox + px, y, oz + pz), rot, scale));
			foreach (var (p, u) in low)
			{
				covered.Add(p);
				underside[p] = underside.TryGetValue(p, out float o) ? MathF.Min(o, u + y) : u + y;
			}
		}
		// Along the roofed stretch, a boulder every so often; then more where the floor is still open.
		float total = c.Along[^1], s = 0;
		while (s <= total)
		{
			int i = Math.Max(1, Array.FindIndex(c.Along, a => a >= s));
			float t = (s - c.Along[i - 1]) / MathF.Max(1e-4f, c.Along[i] - c.Along[i - 1]);
			Vector2 p = Vector2.Lerp(c.Curve[i - 1].P, c.Curve[i].P, t);
			if (c.Depth[i - 1] + (c.Depth[i] - c.Depth[i - 1]) * t >= path.Headroom + 1.5f)
			{
				Place(p.X + ((float)rnd.NextDouble() - 0.5f) * half * 0.4f, p.Y + ((float)rnd.NextDouble() - 0.5f) * half * 0.4f);
			}
			s += MathF.Max(2, path.Width * 0.9f);
		}
		var tried = new HashSet<int>();
		for (int tries = 0; tries < 200; tries++)
		{
			// A point no boulder can cover (at the very edge of the grid) is not tried forever.
			int open = must.FirstOrDefault(p => !covered.Contains(p) && !tried.Contains(p), -1);
			if (open < 0)
			{
				break;
			}
			tried.Add(open);
			Place(open % w, open / w);
		}
		// The walls rise into the rock wherever its underside is above them (half a metre into it).
		foreach (var (p, q) in near)
		{
			if (q.Dist > half && underside.TryGetValue(p, out float u))
			{
				seal[p] = u - 0.5f;
			}
		}
		return new Result(rocks, seal, must.Count(p => !covered.Contains(p)));
	}
}
