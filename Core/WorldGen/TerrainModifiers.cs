using System.Numerics;
using System.Reflection;
using System.Text.Json;
using TerrainEditor.Save;

namespace TerrainEditor.Terrain;

// Runtime terrain modifiers (TerrainModifier components) that the game applies to every zone's
// heightmap before the saved terrain edits: the flattening around locations, plus a few network
// prefabs. Their settings were extracted from the game's asset bundles (terrain-modifiers.json);
// placement comes from the world save. The maths mirrors Heightmap.ApplyModifiers.
public sealed class TerrainModifiers
{
	public sealed record Modifier(
		Vector3 Position, bool Level, float LevelOffset, float LevelRadius, bool Square, bool Smooth, float SmoothRadius, float SmoothPower,
		bool PaintCleared, float PaintRadius, int SortOrder, bool Player, long CreationTime, bool Disc = false)
	{
		// TerrainModifier.GetRadius.
		public float Radius => Math.Max(Level ? LevelRadius : 0f, Math.Max(Smooth ? SmoothRadius : 0f, PaintCleared ? PaintRadius : 0f));
	}

	private sealed record Template(Vector3 Offset, bool Level, float LevelOffset, float LevelRadius, bool Square, bool Smooth, float SmoothRadius, float SmoothPower, bool PaintCleared, float PaintRadius, int SortOrder, bool Player);

	private static readonly Dictionary<int, (string Name, bool Location, List<Template> Mods)> Prefabs = LoadPrefabs();

	public static IEnumerable<int> NetworkPrefabHashes => Prefabs.Where(p => !p.Value.Location).Select(p => p.Key);

	private readonly Dictionary<(int, int), List<Modifier>> _byZone = new();

	public int Count { get; }

	public int LocationsWithModifiers { get; }

	public TerrainModifiers(WorldSave world) : this(world.Placed)
	{
	}

	// From any set of placed objects (Uplift: the world's with ground discs added or taken away).
	public TerrainModifiers(IEnumerable<PlacedObject> placed)
	{
		List<Modifier> all = new();
		foreach (PlacedObject p in placed)
		{
			int key = p.Location != 0 ? p.Location : p.Prefab;
			if (!Prefabs.TryGetValue(key, out var prefab) || prefab.Location != (p.Location != 0))
			{
				continue;
			}
			if (prefab.Location)
			{
				LocationsWithModifiers++;
			}
			// Quaternion.Euler in Unity applies z, then x, then y: the same as yaw/pitch/roll here.
			Quaternion rot = Quaternion.CreateFromYawPitchRoll(Deg(p.Rotation.Y), Deg(p.Rotation.X), Deg(p.Rotation.Z));
			foreach (Template t in prefab.Mods)
			{
				Vector3 pos = p.Position + Vector3.Transform(t.Offset, rot);
				all.Add(new Modifier(pos, t.Level, t.LevelOffset, t.LevelRadius, t.Square, t.Smooth, t.SmoothRadius, t.SmoothPower, t.PaintCleared, t.PaintRadius, t.SortOrder, t.Player, prefab.Location ? 0 : p.TimeCreated,
					prefab.Location && TerrainEditor.Editing.Uplift.IsDisc(p.Location)));
			}
		}
		// TerrainModifier.SortByModifiers.
		all.Sort((a, b) =>
		{
			if (a.Player != b.Player) return a.Player.CompareTo(b.Player);
			if (a.SortOrder != b.SortOrder) return a.SortOrder.CompareTo(b.SortOrder);
			if (a.CreationTime != b.CreationTime) return a.CreationTime.CompareTo(b.CreationTime);
			return a.Position.LengthSquared().CompareTo(b.Position.LengthSquared());
		});
		foreach (Modifier m in all)
		{
			// Heightmap.TerrainVSModifier: radius + 4 m against the zone square.
			float r = m.Radius + 4f;
			int zx0 = (int)Math.Floor((m.Position.X - r + 32f) / 64f), zx1 = (int)Math.Floor((m.Position.X + r + 32f) / 64f);
			int zz0 = (int)Math.Floor((m.Position.Z - r + 32f) / 64f), zz1 = (int)Math.Floor((m.Position.Z + r + 32f) / 64f);
			for (int zz = zz0; zz <= zz1; zz++)
			{
				for (int zx = zx0; zx <= zx1; zx++)
				{
					if (Overlaps(m, zx, zz))
					{
						(_byZone.TryGetValue((zx, zz), out var list) ? list : _byZone[(zx, zz)] = new()).Add(m);
					}
				}
			}
		}
		Count = all.Count;
	}

	public IReadOnlyList<Modifier> InZone(int zx, int zz) => _byZone.TryGetValue((zx, zz), out var list) ? list : Array.Empty<Modifier>();

	private static float Deg(float d) => d * (MathF.PI / 180f);

	private static bool Overlaps(Modifier m, int zx, int zz)
	{
		float num = m.Radius + 4f;
		float cx = zx * 64f, cz = zz * 64f, half = 32f;
		return !(m.Position.X + num < cx - half || m.Position.X - num > cx + half || m.Position.Z + num < cz - half || m.Position.Z - num > cz + half);
	}

	// Applies the zone's modifiers to its 65x65 base heights (in place). Returns the heights the
	// saved edits are clamped against (TerrainComp.ApplyToHeightmap's baseHeights).
	public float[] Apply(int zx, int zz, float[] heights)
	{
		float[]? baseHeights = null, levelOnly = null;
		foreach (Modifier m in InZone(zx, zz))
		{
			if (m.Player && baseHeights == null)
			{
				baseHeights = (float[])heights.Clone();
				levelOnly = (float[])heights.Clone();
			}
			Vector3 at = m.Position + new Vector3(0f, m.LevelOffset, 0f);
			if (m.Level)
			{
				LevelTerrain(zx, zz, heights, at, m.LevelRadius, m.Square, baseHeights, levelOnly, m.Player);
			}
			if (m.Smooth)
			{
				SmoothTerrain2(zx, zz, heights, at, m.SmoothRadius, levelOnly, m.SmoothPower, m.Player);
			}
		}
		return baseHeights ?? (float[])heights.Clone();
	}

	private const int Width = 64;

	// Heightmap.LevelTerrain (WorldToVertexMask, square or round area, sets the modifier's height).
	private static void LevelTerrain(int zx, int zz, float[] h, Vector3 worldPos, float radius, bool square, float[]? baseHeights, float[]? levelOnly, bool player)
	{
		float dx = worldPos.X - zx * 64f, dz = worldPos.Z - zz * 64f;
		int half = (Width + 1) / 2;
		int x = (int)MathF.Floor(dx / 1f + 0.5f + half);
		int y = (int)MathF.Floor(dz / 1f + 0.5f + half);
		float num = (float)((double)radius / 1.0);
		int num2 = (int)MathF.Ceiling(num);
		int n = Width + 1;
		for (int i = y - num2; i <= y + num2; i++)
		{
			for (int j = x - num2; j <= x + num2; j++)
			{
				if ((square || !(Distance(x, y, j, i) > num)) && j >= 0 && i >= 0 && j < n && i < n)
				{
					float v = worldPos.Y;
					if (player)
					{
						float b = baseHeights![i * n + j];
						v = levelOnly![i * n + j] = Math.Clamp(v, (float)((double)b - 8.0), (float)((double)b + 8.0));
					}
					h[i * n + j] = v;
				}
			}
		}
	}

	// Heightmap.SmoothTerrain2 (WorldToVertex, round area, blend towards the modifier's height).
	private static void SmoothTerrain2(int zx, int zz, float[] h, Vector3 worldPos, float radius, float[]? levelOnly, float power, bool player)
	{
		float dx = worldPos.X - zx * 64f, dz = worldPos.Z - zz * 64f;
		int x = (int)MathF.Floor(dx / 1f + 0.5f) + Width / 2;
		int y = (int)MathF.Floor(dz / 1f + 0.5f) + Width / 2;
		float b = worldPos.Y;
		float num = radius / 1f;
		int num2 = (int)MathF.Ceiling(num);
		int n = Width + 1;
		for (int i = y - num2; i <= y + num2; i++)
		{
			for (int j = x - num2; j <= x + num2; j++)
			{
				float d = Distance(x, y, j, i);
				if (d > num)
				{
					continue;
				}
				float num5 = d / num;
				if (j >= 0 && i >= 0 && j < n && i < n)
				{
					num5 = power != 3f ? MathF.Pow(num5, power) : (float)((double)num5 * num5 * num5);
					float t = (float)(1.0 - num5);
					float v = ValheimGen.DUtils.Lerp(h[i * n + j], b, t);
					if (player)
					{
						float lo = levelOnly![i * n + j];
						v = Math.Clamp(v, (float)((double)lo - 1.0), (float)((double)lo + 1.0));
					}
					h[i * n + j] = v;
				}
			}
		}
	}

	// UnityEngine.Vector2.Distance in single precision.
	private static float Distance(int ax, int ay, int bx, int by)
	{
		float fx = ax - bx, fy = ay - by;
		return (float)Math.Sqrt(fx * fx + fy * fy);
	}

	private static Dictionary<int, (string, bool, List<Template>)> LoadPrefabs()
	{
		using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("TerrainEditor.terrain-modifiers.json")
			?? throw new InvalidOperationException("terrain-modifiers.json is not embedded");
		using JsonDocument doc = JsonDocument.Parse(s);
		Dictionary<int, (string, bool, List<Template>)> result = new();
		foreach (JsonProperty p in doc.RootElement.EnumerateObject())
		{
			List<Template> mods = new();
			foreach (JsonElement m in p.Value.GetProperty("modifiers").EnumerateArray())
			{
				JsonElement pos = m.GetProperty("pos");
				bool B(string k) => m.GetProperty(k).GetInt32() != 0;
				float F(string k) => m.GetProperty(k).GetSingle();
				mods.Add(new Template(new Vector3(pos[0].GetSingle(), pos[1].GetSingle(), pos[2].GetSingle()), B("level"), F("levelOffset"), F("levelRadius"), B("square"),
					B("smooth"), F("smoothRadius"), F("smoothPower"), B("paintCleared"), F("paintRadius"), m.GetProperty("sortOrder").GetInt32(), B("player")));
			}
			result[StableHash.Of(p.Name)] = (p.Name, p.Value.GetProperty("location").GetBoolean(), mods);
		}
		return result;
	}
}
