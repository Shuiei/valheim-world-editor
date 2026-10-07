using System.Numerics;
using TerrainEditor.Terrain;

namespace TerrainEditor.Desktop;

// What the View panel's overlays draw, as line segments in view space (pairs of points), like the web
// editor (editor.html borders and markers, editor/overlays.js rings): zone borders, location markers,
// the ground wards protect, the range crafting stations let you build in, and the ground locations
// flatten. Built on the CPU from the scene; GlView sends them to the graphics card.
public static class Overlays
{
	public enum Layer
	{
		Borders,
		Markers,
		Wards,
		Stations,
		Flatten,
		FlattenEdge,
	}

	public static readonly Layer[] All = Enum.GetValues<Layer>();

	// Colour (sRGB) and opacity of each layer, and whether the ground and objects hide it.
	public static (Vector4 Color, bool Hidden) Style(Layer l) => l switch
	{
		Layer.Borders => (new Vector4(1, 1, 1, 0.25f), true),
		Layer.Markers => (new Vector4(1, 0.54f, 0.16f, 1), true),
		Layer.Wards => (new Vector4(1, 0.44f, 0.26f, 0.9f), false),
		Layer.Stations => (new Vector4(0.31f, 0.76f, 1, 0.9f), false),
		Layer.Flatten => (new Vector4(1, 0.84f, 0.31f, 0.95f), false),
		_ => (new Vector4(1, 0.84f, 0.31f, 0.35f), false),
	};

	public sealed record Built(Dictionary<Layer, float[]> Lines, int Wards, int Stations, int Flattened, int Locations);

	// names: each thing's prefab name (null when unknown).
	public static Built Build(WorldScene s, TerrainModifiers? modifiers, Func<int, string?> names)
	{
		var lines = All.ToDictionary(l => l, _ => new List<float>());
		Vector3 OnGround(float gx, float gz, float lift)
		{
			float x = gx - (s.W - 1) / 2f, z = -(gz - (s.H - 1) / 2f);
			return new Vector3(x, Picking.HeightAt(s, x, z) + lift, z);
		}
		void Strip(Layer l, IReadOnlyList<Vector3> pts)
		{
			for (int i = 1; i < pts.Count; i++)
			{
				lines[l].AddRange(new[] { pts[i - 1].X, pts[i - 1].Y, pts[i - 1].Z, pts[i].X, pts[i].Y, pts[i].Z });
			}
		}
		float ox = s.X0 * 64f - 32f, oz = s.Z0 * 64f - 32f;
		// A ring (or square) around a world point, following the ground.
		void Ring(Layer l, float wx, float wz, float r, bool square = false)
		{
			float gx = wx - ox, gz = wz - oz;
			var pts = new List<Vector3>();
			if (square)
			{
				int n = Math.Max(1, (int)MathF.Ceiling(2 * r));
				foreach (var (ax, az, bx, bz) in new[] { (-r, -r, r, -r), (r, -r, r, r), (r, r, -r, r), (-r, r, -r, -r) })
				{
					for (int k = 0; k <= n; k++)
					{
						pts.Add(OnGround(gx + ax + (bx - ax) * k / n, gz + az + (bz - az) * k / n, 0.4f));
					}
				}
			}
			else
			{
				int n = Math.Max(24, (int)MathF.Ceiling(2 * MathF.PI * r));
				for (int k = 0; k <= n; k++)
				{
					float a = k / (float)n * MathF.Tau;
					pts.Add(OnGround(gx + MathF.Cos(a) * r, gz + MathF.Sin(a) * r, 0.4f));
				}
			}
			Strip(l, pts);
		}

		// Zone borders, every 2 m along each zone edge, just above the ground.
		for (int i = 0; i <= s.Size; i++)
		{
			foreach (bool vertical in new[] { true, false })
			{
				var pts = new List<Vector3>();
				for (int t = 0; t < s.W; t += 2)
				{
					int gx = vertical ? i * 64 : t, gz = vertical ? t : i * 64;
					if (gx < s.W && gz < s.H)
					{
						pts.Add(OnGround(gx, gz, 0.15f));
					}
				}
				Strip(Layer.Borders, pts);
			}
		}

		// Location markers: a post 14 m tall at each location in or near the area.
		float minX = ox - 40, maxX = ox + s.W + 40, minZ = oz - 40, maxZ = oz + s.H + 40;
		int locations = 0;
		foreach (var (p, _) in s.World?.Locations ?? new())
		{
			if (p.X < minX || p.X > maxX || p.Z < minZ || p.Z > maxZ)
			{
				continue;
			}
			locations++;
			var b = OnGround(p.X - ox, p.Z - oz, 0);
			foreach (var (dx, dz) in new[] { (0.8f, 0f), (-0.8f, 0f), (0f, 0.8f), (0f, -0.8f), (0f, 0f) })
			{
				lines[Layer.Markers].AddRange(new[] { b.X + dx, b.Y, b.Z + dz, b.X + dx, b.Y + 14, b.Z + dz });
			}
		}

		// Wards and crafting stations: the kinds with a ward radius or a build range.
		var ranges = PrefabCatalog.Placeable.Where(p => p.WardRadius > 0 || p.BuildRange > 0).ToDictionary(p => p.Name, p => (p.WardRadius, p.BuildRange));
		int wards = 0, stations = 0;
		for (int i = 0; i < s.Things.Count; i++)
		{
			if (names(i) is not string n || !ranges.TryGetValue(n, out var r))
			{
				continue;
			}
			var t = s.Things[i];
			if (t.Gone)
			{
				continue;
			}
			if (r.WardRadius > 0)
			{
				Ring(Layer.Wards, t.Position.X, t.Position.Z, r.WardRadius);
				wards++;
			}
			if (r.BuildRange > 0)
			{
				Ring(Layer.Stations, t.Position.X, t.Position.Z, r.BuildRange);
				stations++;
			}
		}

		// Ground that locations flatten: the flat part, and fainter where it blends into the land.
		int flattened = 0;
		if (modifiers != null)
		{
			var seen = new HashSet<TerrainModifiers.Modifier>();
			for (int zz = s.Z0; zz < s.Z0 + s.Size; zz++)
			{
				for (int zx = s.X0; zx < s.X0 + s.Size; zx++)
				{
					foreach (var m in modifiers.InZone(zx, zz).Where(m => !m.Player && (m.Level || m.Smooth)))
					{
						if (!seen.Add(m))
						{
							continue;
						}
						flattened++;
						if (m.Level && m.LevelRadius > 0)
						{
							Ring(Layer.Flatten, m.Position.X, m.Position.Z, m.LevelRadius, m.Square);
						}
						if (m.Smooth && m.SmoothRadius > 0)
						{
							Ring(Layer.FlattenEdge, m.Position.X, m.Position.Z, m.SmoothRadius, m.Square);
						}
					}
				}
			}
		}
		return new Built(lines.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()), wards, stations, flattened, locations);
	}
}
