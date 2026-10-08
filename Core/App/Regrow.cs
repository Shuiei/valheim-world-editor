using System.Numerics;
using System.Reflection;
using System.Text.Json;
using TerrainEditor.Editing;
using ValheimGen;
using NQuaternion = System.Numerics.Quaternion;
using NVector3 = System.Numerics.Vector3;
using Rnd = ValheimGen.UnityEngine.Random;

namespace TerrainEditor.App;

// "Regrow nature": the trees, rocks, bushes and pickables the game would put in a zone, by its own
// vegetation rules (WorldGen/vegetation.json, from ZoneSystem.m_vegetation and the LocationLists) and
// the same random draws (ZoneSystem.PlaceVegetation, Unity's Random seeded per zone and rule), on the
// ground as it is now. What the editor cannot know is skipped: the physics checks against objects
// already standing (IsBlocked, snapping onto rocks), clear areas around locations, and the Deep North's
// alt biomes. The page drops spots taken by objects that are there and keeps those in the selection.
public static class Regrow
{
	private sealed class Rule
	{
		public string prefab { get; set; } = "";
		public string name { get; set; } = "";
		public int enable { get; set; }
		public float min { get; set; }
		public float max { get; set; }
		public int forcePlacement { get; set; }
		public float scaleMin { get; set; } = 1f;
		public float scaleMax { get; set; } = 1f;
		public float randTilt { get; set; }
		public float chanceToUseGroundTilt { get; set; }
		public int biome { get; set; }
		public int biomeArea { get; set; } = 7;
		public int blockCheck { get; set; }
		public int snapToStaticSolid { get; set; }
		public float minAltitude { get; set; } = -1000f;
		public float maxAltitude { get; set; } = 1000f;
		public float minVegetation { get; set; }
		public float maxVegetation { get; set; }
		public float minOceanDepth { get; set; }
		public float maxOceanDepth { get; set; }
		public float minTilt { get; set; }
		public float maxTilt { get; set; } = 90f;
		public float terrainDeltaRadius { get; set; }
		public float maxTerrainDelta { get; set; } = 2f;
		public float minTerrainDelta { get; set; }
		public int snapToWater { get; set; }
		public float groundOffset { get; set; }
		public int groupSizeMin { get; set; } = 1;
		public int groupSizeMax { get; set; } = 1;
		public float groupRadius { get; set; }
		public float minDistanceFromCenter { get; set; }
		public float maxDistanceFromCenter { get; set; }
		public int inForest { get; set; }
		public float forestTresholdMin { get; set; }
		public float forestTresholdMax { get; set; } = 1f;
		// Random draws the object's components make when the game creates it (Awake): they move the
		// sequence on for the rest of the rule.
		public int draws { get; set; }
	}

	private static readonly Lazy<List<Rule>> Rules = new(() =>
	{
		using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("TerrainEditor.vegetation.json")
			?? throw new InvalidOperationException("vegetation.json is not embedded");
		return JsonSerializer.Deserialize<List<Rule>>(s)!;
	});

	public static int RuleCount => Rules.Value.Count;



	// One object the game would place: prefab, Unity world position, Euler rotation (degrees), scale.
	public sealed record Spot(string Name, float X, float Y, float Z, float Rx, float Ry, float Rz, float Scale);

	// Unity's Random is one shared state: one regrow at a time (the terrain generator uses it too, once).
	private static readonly object RandomLock = new();

	// What the game would place in zones x0..x1, z0..z1 on the current ground. Kinds the editor cannot
	// create (no networked object) are left out by the caller.
	public static List<Spot> Zones(TerrainService terrain, EditStore edits, int seed, int x0, int z0, int x1, int z1)
	{
		// The ground one zone around too: groups reach up to 20 m past their zone.
		var (w, h, heights) = HeightGrid.Read(terrain, edits, x0 - 1, z0 - 1, x1 + 1, z1 + 1);
		Ground ground = new(heights, w, h, (x0 - 1) * 64f - 32f, (z0 - 1) * 64f - 32f);
		List<Spot> spots = new();
		lock (RandomLock)
		{
			Rnd.State saved = Rnd.state;
			try
			{
				for (int zz = z0; zz <= z1; zz++)
				{
					for (int zx = x0; zx <= x1; zx++)
					{
						PlaceZone(terrain, ground, seed, zx, zz, spots);
					}
				}
			}
			finally
			{
				Rnd.state = saved;
			}
		}
		return spots;
	}

	// The ground the editor has now: heights every metre, triangles split like the game's terrain
	// mesh (from (x + 1, z) to (x, z + 1)).
	private sealed class Ground(float[] heights, int w, int h, float wx0, float wz0)
	{
		public bool Inside(float x, float z) => x >= wx0 && z >= wz0 && x < wx0 + w - 1 && z < wz0 + h - 1;

		public float Height(float x, float z, out NVector3 normal)
		{
			float gx = Math.Clamp(x - wx0, 0f, w - 1.001f), gz = Math.Clamp(z - wz0, 0f, h - 1.001f);
			int ix = (int)gx, iz = (int)gz;
			float fx = gx - ix, fz = gz - iz;
			float h00 = heights[iz * w + ix], h10 = heights[iz * w + ix + 1], h01 = heights[(iz + 1) * w + ix], h11 = heights[(iz + 1) * w + ix + 1];
			if (fx + fz <= 1f)
			{
				normal = NVector3.Normalize(new NVector3(-(h10 - h00), 1f, -(h01 - h00)));
				return h00 + (h10 - h00) * fx + (h01 - h00) * fz;
			}
			normal = NVector3.Normalize(new NVector3(-(h11 - h01), 1f, -(h11 - h10)));
			return h11 + (h01 - h11) * (1f - fx) + (h10 - h11) * (1f - fz);
		}

		public float Height(float x, float z) => Height(x, z, out _);
	}

	// One zone's heightmap as the game sees it while placing: corner biomes, edge or middle of a biome,
	// the vegetation mask and the ocean depth at its corners.
	private sealed class Zone
	{
		public required int[] Corners;
		public required float[] Mask;
		public required float[] OceanDepth;
		public float Cx, Cz;

		public bool HaveBiome(int biome) => Corners.Any(c => (c & biome) != 0);

		public int Area => Corners[0] == Corners[1] && Corners[0] == Corners[2] && Corners[0] == Corners[3] ? 2 : 1;   // Median : Edge

		public int Biome(float x, float z)
		{
			if (Corners[0] == Corners[1] && Corners[0] == Corners[2] && Corners[0] == Corners[3])
			{
				return Corners[0];
			}
			float nx = (x - Cx) / 64f + 0.5f, ny = (z - Cz) / 64f + 0.5f;
			Dictionary<int, float> weights = new();
			void Add(int b, float cx, float cy)
			{
				float dx = nx - cx, dy = ny - cy, d = MathF.Sqrt(dx * dx + dy * dy), k = 1.414f - d;
				weights[b] = weights.GetValueOrDefault(b) + k * k * k;
			}
			Add(Corners[0], 0f, 0f); Add(Corners[1], 1f, 0f); Add(Corners[2], 0f, 1f); Add(Corners[3], 1f, 1f);
			// Ties go to the lowest biome index, as the game walks its weights in index order.
			return weights.OrderByDescending(kv => kv.Value).ThenBy(kv => BitOperations.Log2((uint)kv.Key)).First().Key;
		}

		// Heightmap.GetVegetationMask: the alpha of the base paint mask at the vertex under the point.
		public float VegetationMask(float x, float z)
		{
			int mx = Math.Clamp((int)MathF.Floor(x - 0.5f - Cx + 0.5f + 32f), 0, 64), my = Math.Clamp((int)MathF.Floor(z - 0.5f - Cz + 0.5f + 32f), 0, 64);
			return Mask[(my * 65 + mx) * 4 + 3];
		}

		public float Ocean(float x, float z)
		{
			int vx = (int)MathF.Floor(x - Cx + 0.5f) + 32, vy = (int)MathF.Floor(z - Cz + 0.5f) + 32;
			float t = vx / 64f, t2 = vy / 64f;
			float a = Lerp(OceanDepth[3], OceanDepth[2], t), b = Lerp(OceanDepth[0], OceanDepth[1], t);
			return Lerp(a, b, t2);
		}
	}

	private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0f, 1f);

	private static Zone ZoneAt(TerrainService terrain, Ground ground, Dictionary<(int, int), Zone> cache, float x, float z)
	{
		int zx = (int)MathF.Floor((x + 32f) / 64f), zz = (int)MathF.Floor((z + 32f) / 64f);
		if (cache.TryGetValue((zx, zz), out Zone? zone))
		{
			return zone;
		}
		float cx = zx * 64f, cz = zz * 64f;
		float Depth(float px, float pz) => MathF.Max(0f, TerrainService.WaterLevel - ground.Height(px, pz));
		zone = new Zone
		{
			Corners = terrain.CornerBiomes(zx, zz),
			Mask = terrain.BaseMask(zx, zz),
			// Heightmap corners 0: (0, width), 1: (width, width), 2: (width, 0), 3: (0, 0).
			OceanDepth = new[] { Depth(cx - 32f, cz + 32f), Depth(cx + 32f, cz + 32f), Depth(cx + 32f, cz - 32f), Depth(cx - 32f, cz - 32f) },
			Cx = cx,
			Cz = cz,
		};
		cache[(zx, zz)] = zone;
		return zone;
	}

	private static void PlaceZone(TerrainService terrain, Ground ground, int seed, int zx, int zz, List<Spot> spots)
	{
		Dictionary<(int, int), Zone> zones = new();
		Zone hmap = ZoneAt(terrain, ground, zones, zx * 64f, zz * 64f);
		float cx = zx * 64f, cz = zz * 64f;
		foreach (Rule veg in Rules.Value)
		{
			if (veg.enable == 0 || !hmap.HaveBiome(veg.biome))
			{
				continue;
			}
			Rnd.InitState(seed + zx * 4271 + zz * 9187 + Save.StableHash.Of(veg.prefab));
			int count = 1;
			if (veg.max < 1f)
			{
				if (Rnd.value > veg.max)
				{
					continue;
				}
			}
			else
			{
				count = Rnd.Range((int)veg.min, (int)veg.max + 1);
			}
			float maxTiltCos = MathF.Cos(MathF.PI / 180f * veg.maxTilt), minTiltCos = MathF.Cos(MathF.PI / 180f * veg.minTilt);
			float reach = 32f - veg.groupRadius;
			int tries = veg.forcePlacement != 0 ? count * 50 : count, placedGroups = 0;
			for (int i = 0; i < tries; i++)
			{
				float vx = Rnd.Range(cx - reach, cx + reach), vz = Rnd.Range(cz - reach, cz + reach);
				int groupSize = Rnd.Range(veg.groupSizeMin, veg.groupSizeMax + 1);
				bool placed = false;
				for (int j = 0; j < groupSize; j++)
				{
					float px = vx, pz = vz;
					if (j > 0)
					{
						float f = Rnd.value * MathF.PI * 2f, r = Rnd.Range(0f, veg.groupRadius);
						px += MathF.Sin(f) * r;
						pz += MathF.Cos(f) * r;
					}
					float ry = Rnd.Range(0, 360);
					float scale = Rnd.Range(veg.scaleMin, veg.scaleMax);
					float rx = Rnd.Range(-veg.randTilt, veg.randTilt), rz = Rnd.Range(-veg.randTilt, veg.randTilt);
					if (!ground.Inside(px, pz))
					{
						continue;
					}
					float py = ground.Height(px, pz, out NVector3 normal);
					Zone hm = ZoneAt(terrain, ground, zones, px, pz);
					if ((veg.biome & hm.Biome(px, pz)) == 0 || (veg.biomeArea & hm.Area) == 0)
					{
						continue;
					}
					float altitude = py - TerrainService.WaterLevel;
					if (altitude < veg.minAltitude || altitude > veg.maxAltitude)
					{
						continue;
					}
					if (!veg.minVegetation.Equals(veg.maxVegetation))
					{
						float m = hm.VegetationMask(px, pz);
						if (m > veg.maxVegetation || m < veg.minVegetation)
						{
							continue;
						}
					}
					if (!veg.minOceanDepth.Equals(veg.maxOceanDepth))
					{
						float d = hm.Ocean(px, pz);
						if (d < veg.minOceanDepth || d > veg.maxOceanDepth)
						{
							continue;
						}
					}
					if (normal.Y < maxTiltCos || normal.Y > minTiltCos)
					{
						continue;
					}
					if (veg.terrainDeltaRadius > 0f)
					{
						float hi = -999999f, lo = 999999f;
						for (int k = 0; k < 10; k++)
						{
							var c = Rnd.insideUnitCircle;
							float gh = ground.Height(px + c.x * veg.terrainDeltaRadius, pz + c.y * veg.terrainDeltaRadius);
							lo = MathF.Min(lo, gh);
							hi = MathF.Max(hi, gh);
						}
						float delta = hi - lo;
						if (delta > veg.maxTerrainDelta || delta < veg.minTerrainDelta)
						{
							continue;
						}
					}
					if (veg.minDistanceFromCenter > 0f || veg.maxDistanceFromCenter > 0f)
					{
						float dist = MathF.Sqrt(px * px + pz * pz);
						if ((veg.minDistanceFromCenter > 0f && dist < veg.minDistanceFromCenter) || (veg.maxDistanceFromCenter > 0f && dist > veg.maxDistanceFromCenter))
						{
							continue;
						}
					}
					if (veg.inForest != 0)
					{
						float forest = WorldGenerator.GetForestFactor(new ValheimGen.Vector3(px, py, pz));
						if (forest < veg.forestTresholdMin || forest > veg.forestTresholdMax)
						{
							continue;
						}
					}
					if (veg.snapToWater != 0)
					{
						py = TerrainService.WaterLevel;
					}
					py += veg.groundOffset;
					NVector3 euler;
					if (veg.chanceToUseGroundTilt > 0f && Rnd.value <= veg.chanceToUseGroundTilt)
					{
						// Quaternion.LookRotation(Cross(normal, Euler(0, y, 0) * forward), normal).
						float a = ry * MathF.PI / 180f;
						NVector3 fwd = new(MathF.Sin(a), 0f, MathF.Cos(a));
						euler = BlueprintFormats.ToEuler(LookRotation(NVector3.Cross(normal, fwd), normal));
					}
					else
					{
						euler = new NVector3(rx, ry, rz);
					}
					spots.Add(new Spot(veg.prefab, px, py, pz, euler.X, euler.Y, euler.Z, scale));
					for (int k = veg.draws; k > 0; k--)
					{
						Rnd.NextUInt();
					}
					placed = true;
				}
				if (placed)
				{
					placedGroups++;
				}
				if (placedGroups >= count)
				{
					break;
				}
			}
		}
	}

	// Unity's Quaternion.LookRotation(forward, up).
	private static NQuaternion LookRotation(NVector3 forward, NVector3 up)
	{
		NVector3 z = NVector3.Normalize(forward);
		NVector3 x = NVector3.Normalize(NVector3.Cross(up, z));
		NVector3 y = NVector3.Cross(z, x);
		// Columns x, y, z of the rotation matrix.
		float m00 = x.X, m01 = y.X, m02 = z.X, m10 = x.Y, m11 = y.Y, m12 = z.Y, m20 = x.Z, m21 = y.Z, m22 = z.Z;
		float trace = m00 + m11 + m22;
		if (trace > 0f)
		{
			float s = MathF.Sqrt(trace + 1f) * 2f;
			return new NQuaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s);
		}
		if (m00 > m11 && m00 > m22)
		{
			float s = MathF.Sqrt(1f + m00 - m11 - m22) * 2f;
			return new NQuaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
		}
		if (m11 > m22)
		{
			float s = MathF.Sqrt(1f + m11 - m00 - m22) * 2f;
			return new NQuaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s);
		}
		float s2 = MathF.Sqrt(1f + m22 - m00 - m11) * 2f;
		return new NQuaternion((m02 + m20) / s2, (m12 + m21) / s2, 0.25f * s2, (m10 - m01) / s2);
	}
}
