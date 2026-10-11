using System.Reflection;
using System.Text.Json;
using TerrainEditor.Save;
using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

// Where the game will lay out a seed's locations (start temple, traders, bosses, dungeons...) when its
// world first loads, before the world exists: ZoneSystem.GenerateLocations replayed with the game's
// rules on the editor's copy of its generator. The rules are read from the game's files when Valheim
// is found (GameLocations, then Use); WorldGen/locations.json is a copy for when it is not.
//
// The game picks each try's zone from a biome map of the world (AltBiomeWorldData: 2048 x 2048 points
// 12 m apart, each biome's points listed in the order its flood fill met them), then checks the spot
// as ZoneSystem does. Each location kind starts Unity's Random afresh from the seed and its name, so
// the result is the game's, with one exception: alt biomes (Dark Meadows, Lox Plains...) are given to
// biome areas in an order the game shuffles with an unseeded random, different at each load. The few
// kinds an alt biome adds or blocks cannot be known (Uncertain), and are left out; the prioritized
// kinds (start, bosses, traders...) come first and never depend on them.
public static class SeedLocations
{
	public sealed class Rule
	{
		public string prefab { get; set; } = "";
		public string name { get; set; } = "";
		public int enable { get; set; }
		public int biome { get; set; }
		public int biomeArea { get; set; }
		public int quantity { get; set; }
		public int prioritized { get; set; }
		public int centerFirst { get; set; }
		public int unique { get; set; }
		public string group { get; set; } = "";
		public float minDistanceFromSimilar { get; set; }
		public string groupMax { get; set; } = "";
		public float maxDistanceFromSimilar { get; set; }
		public int iconAlways { get; set; }
		public int iconPlaced { get; set; }
		public float interiorRadius { get; set; }
		public float exteriorRadius { get; set; }
		public float minTerrainDelta { get; set; }
		public float maxTerrainDelta { get; set; }
		public float minimumVegetation { get; set; }
		public float maximumVegetation { get; set; }
		public int surroundCheckVegetation { get; set; }
		public float surroundCheckDistance { get; set; }
		public int surroundCheckLayers { get; set; }
		public float surroundBetterThanAverage { get; set; }
		public int inForest { get; set; }
		public float forestTresholdMin { get; set; }
		public float forestTresholdMax { get; set; }
		public float minDistanceFromCenter { get; set; }
		public float maxDistanceFromCenter { get; set; }
		public float minDistance { get; set; }
		public float maxDistance { get; set; }
		public float minAltitude { get; set; }
		public float maxAltitude { get; set; }
		public string? altBiome { get; set; }
	}

	public sealed class AltBiomeRule
	{
		public string name { get; set; } = "";
		public List<string> blockLocationNames { get; set; } = new();
	}

	// The game's rules: its locations in its order (ZoneSystem's list, the location lists, the alt
	// biomes' additions) and the alt biomes.
	public sealed class RuleSet
	{
		public List<Rule> locations { get; set; } = new();
		public List<AltBiomeRule> altBiomes { get; set; } = new();

		// As locations.json keeps them: one location or alt biome a line.
		public string ToJson() =>
			"{\"locations\": [\n" + string.Join(",\n", locations.Select(l => JsonSerializer.Serialize(l, JsonOptions))) +
			"\n],\n\"altBiomes\": [\n" + string.Join(",\n", altBiomes.Select(a => JsonSerializer.Serialize(a, JsonOptions))) + "\n]}\n";

		public static RuleSet FromJson(string json) => JsonSerializer.Deserialize<RuleSet>(json) ?? throw new JsonException("no rules");
	}

	private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

	// The rules in use and what follows from them, swapped whole (a search running keeps its own).
	private sealed class State(RuleSet rules)
	{
		public RuleSet Rules { get; } = rules;
		public List<Rule> Ordered { get; } = rules.locations.OrderByDescending(l => l.prioritized != 0).Where(l => l.enable != 0 && l.quantity != 0).ToList();
		public HashSet<string> Blocked { get; } = rules.altBiomes.SelectMany(a => a.blockLocationNames).ToHashSet();
	}

	public static RuleSet Embedded { get; } = LoadEmbedded();

	private static RuleSet LoadEmbedded()
	{
		using Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("TerrainEditor.locations.json")
			?? throw new InvalidOperationException("locations.json is not embedded");
		using var reader = new StreamReader(s);
		return RuleSet.FromJson(reader.ReadToEnd());
	}

	private static volatile State _state = new(Embedded);

	// The rules read from the game in place of the copy (or the copy again: Use(Embedded)).
	public static void Use(RuleSet rules) => _state = new State(rules);

	public static RuleSet Current => _state.Rules;

	// One location instance the game will register: its kind and where (y: the generator's ground height).
	public sealed record Placed(Rule Rule, float X, float Y, float Z);

	// The kinds whose spots depend on the alt biomes (added by one, or blocked by one somewhere).
	public static bool Uncertain(Rule r) => r.altBiome != null || _state.Blocked.Contains(r.name);

	// The game's order: prioritized kinds first, otherwise as listed; disabled and empty kinds left out.
	public static IReadOnlyList<Rule> Ordered => _state.Ordered;

	public enum Which
	{
		Start, // the start temple only: the first kind laid out, near the middle (no biome map needed)
		Prioritized, // the kinds laid out first: start, bosses, traders, Hildir's dungeons... (about 0.3 s)
		All, // every kind (several seconds)
	}

	// The seed's location instances, the uncertain kinds left out (a kind's instances are the same
	// whichever is asked). parallel: the biome map on every core but one.
	public static List<Placed> Place(WorldGenerator gen, Which which = Which.Prioritized, bool parallel = false, CancellationToken cancel = default)
	{
		var state = _state;
		var rules = state.Ordered.Take(which switch
		{
			Which.Start => 1,
			Which.Prioritized => state.Ordered.Count(r => r.prioritized != 0),
			_ => state.Ordered.Count,
		}).ToList();
		// The biomes these kinds can draw a zone from (RandomBiome's slips included).
		var needed = Heightmap.Biome.None;
		foreach (Rule r in rules.Where(r => r.centerFirst == 0))
		{
			var b = (Heightmap.Biome)r.biome;
			needed |= b;
			if ((b & (b - 1)) != 0)
			{
				needed |= ((b & Heightmap.Biome.Meadows) != 0 ? Heightmap.Biome.Ocean : 0) | ((b & Heightmap.Biome.Plains) != 0 ? Heightmap.Biome.BlackForest : 0);
			}
		}
		var placer = new Placer(gen, new Lazy<BiomeMap>(() => new BiomeMap(gen, needed, parallel, cancel)));
		Rnd.State saved = Rnd.state;
		try
		{
			foreach (Rule rule in rules)
			{
				cancel.ThrowIfCancellationRequested();
				placer.Generate(rule);
			}
		}
		finally
		{
			Rnd.state = saved;
		}
		return placer.Instances.Values.Where(p => p.Rule.altBiome == null && !state.Blocked.Contains(p.Rule.name)).ToList();
	}

	// AltBiomeWorldData's points (GenerateBiomePoints) and the order its flood fill lists them in
	// (GenerateSectors): each biome's points, and those at or above sea level.
	internal sealed class BiomeMap
	{
		public const int Size = 2048;

		// Heightmap.BiomeIndex order.
		private static readonly Heightmap.Biome[] ByIndex =
		{
			Heightmap.Biome.None, Heightmap.Biome.Meadows, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain, Heightmap.Biome.BlackForest,
			Heightmap.Biome.Plains, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth, Heightmap.Biome.Ocean, Heightmap.Biome.Mistlands,
		};

		private const byte Ash = 6, North = 7, Sea = 8, BiomeBits = 0x0F, Above = 0x10, Visited = 0x20;

		// Per biome index: points as x | y << 16, in the game's order.
		public readonly int[][] All = new int[ByIndex.Length][];
		public readonly int[][] AboveSea = new int[ByIndex.Length][];

		public static float MapToWorld(float x) => (x - 1024f) * 12f + 6f;

		public static int Index(Heightmap.Biome b) => Array.IndexOf(ByIndex, b);

		// needed: the biomes whose points are listed (the others' lists stay empty).
		public BiomeMap(WorldGenerator gen, Heightmap.Biome needed, bool parallel, CancellationToken cancel)
		{
			var listed = new bool[ByIndex.Length];
			for (int b = 0; b < ByIndex.Length; b++)
			{
				listed[b] = (needed & ByIndex[b]) != 0;
			}
			// One byte a point (many seeds are looked at side by side): the biome index, at or above sea
			// level, met by the fill.
			var cell = new byte[Size * Size];
			var options = new ParallelOptions { CancellationToken = cancel, MaxDegreeOfParallelism = parallel ? Math.Max(1, Environment.ProcessorCount - 1) : 1 };
			Parallel.For(0, Size, options, i =>
			{
				float wy = MapToWorld(i);
				for (int j = 0; j < Size; j++)
				{
					float wx = MapToWorld(j);
					int k = j + i * Size;
					if (wx * wx + wy * wy > 110250000f)
					{
						cell[k] = Sea;
						continue;
					}
					Heightmap.Biome b = gen.GetBiome(wx, wy);
					cell[k] = (byte)(Index(b) | (gen.GetBiomeHeight(b, wx, wy, out _) >= 30f ? Above : 0));
				}
			});
			// Sizes first: every point is listed once.
			var count = new int[ByIndex.Length];
			var countAbove = new int[ByIndex.Length];
			for (int k = 0; k < cell.Length; k++)
			{
				int b = cell[k] & BiomeBits;
				if (!listed[b])
				{
					continue;
				}
				count[b]++;
				countAbove[b] += (cell[k] & Above) != 0 ? 1 : 0;
			}
			var n = new int[ByIndex.Length];
			var nAbove = new int[ByIndex.Length];
			for (int b = 0; b < ByIndex.Length; b++)
			{
				All[b] = new int[count[b]];
				AboveSea[b] = new int[countAbove[b]];
			}
			void Add(int x, int y)
			{
				int k = x + y * Size, b = cell[k] & BiomeBits;
				if (!listed[b])
				{
					return;
				}
				All[b][n[b]++] = x | (y << 16);
				if ((cell[k] & Above) != 0)
				{
					AboveSea[b][nAbove[b]++] = x | (y << 16);
				}
			}
			// Ashlands, Deep North and the ocean: one sector each, in rows.
			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					int b = cell[x + y * Size] & BiomeBits;
					if (b is Ash or North or Sea)
					{
						cell[x + y * Size] |= Visited;
						Add(x, y);
					}
				}
			}
			// The rest: each area flood filled from its first point in rows. That first point is not
			// listed (the game only lists the points its fill reaches).
			var stack = new Stack<int>(1024);
			for (int y = 0; y < Size; y++)
			{
				for (int x = 0; x < Size; x++)
				{
					if ((cell[x + y * Size] & Visited) != 0)
					{
						continue;
					}
					cell[x + y * Size] |= Visited;
					stack.Push(x | (y << 16));
					while (stack.Count > 0)
					{
						int c = stack.Pop();
						int cx = c & 0xFFFF, cy = c >> 16;
						int b = cell[cx + cy * Size] & BiomeBits;
						Fill(cx + 1, cy);
						Fill(cx - 1, cy);
						Fill(cx, cy + 1);
						Fill(cx, cy - 1);
						void Fill(int fx, int fy)
						{
							if (fx >= 0 && fy >= 0 && fx < Size && fy < Size && (cell[fx + fy * Size] & Visited) == 0 && (cell[fx + fy * Size] & BiomeBits) == b)
							{
								cell[fx + fy * Size] |= Visited;
								Add(fx, fy);
								stack.Push(fx | (fy << 16));
							}
						}
					}
				}
			}
			// The first points left out: trim the lists to what was listed.
			for (int b = 0; b < ByIndex.Length; b++)
			{
				Array.Resize(ref All[b], n[b]);
				Array.Resize(ref AboveSea[b], nAbove[b]);
			}
		}

		// AltBiomeWorldData.RandomBiomeFromBiomes, slips included (Plains gives Black Forest, the ocean
		// is tested with the Meadows bit).
		public static Heightmap.Biome RandomBiome(Heightmap.Biome biome)
		{
			if ((biome & (biome - 1)) == 0)
			{
				return biome;
			}
			int count = 0;
			foreach (Heightmap.Biome b in new[] { Heightmap.Biome.Meadows, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain, Heightmap.Biome.BlackForest, Heightmap.Biome.Plains, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth, Heightmap.Biome.Ocean, Heightmap.Biome.Mistlands })
			{
				count += (biome & b) != 0 ? 1 : 0;
			}
			int pick = Rnd.Range(0, count - 1);
			if ((biome & Heightmap.Biome.Meadows) != 0 && pick-- == 0)
			{
				return Heightmap.Biome.Meadows;
			}
			if ((biome & Heightmap.Biome.Swamp) != 0 && pick-- == 0)
			{
				return Heightmap.Biome.Swamp;
			}
			if ((biome & Heightmap.Biome.Mountain) != 0 && pick-- == 0)
			{
				return Heightmap.Biome.Mountain;
			}
			if ((biome & Heightmap.Biome.BlackForest) != 0 && pick-- == 0)
			{
				return Heightmap.Biome.BlackForest;
			}
			if ((biome & Heightmap.Biome.Plains) != 0 && pick-- == 0)
			{
				return Heightmap.Biome.BlackForest;
			}
			if ((biome & Heightmap.Biome.AshLands) != 0 && pick-- == 0)
			{
				return Heightmap.Biome.AshLands;
			}
			if ((biome & Heightmap.Biome.DeepNorth) != 0 && pick-- == 0)
			{
				return Heightmap.Biome.DeepNorth;
			}
			if ((biome & Heightmap.Biome.Meadows) != 0 && pick-- == 0)
			{
				return Heightmap.Biome.Ocean;
			}
			return Heightmap.Biome.Mistlands;
		}

		// GetRandomPointByBiomes and GetRandomPointByBiomesAboveSeaLevel, in world space.
		public (float X, float Z) RandomPoint(Heightmap.Biome biomes, bool aboveSea)
		{
			int b = Index(RandomBiome(biomes));
			int[] list = aboveSea && AboveSea[b].Length > 0 ? AboveSea[b] : All[b];
			int p = list[Rnd.Range(0, list.Length)];
			return (MapToWorld(p & 0xFFFF), MapToWorld(p >> 16));
		}
	}

	// ZoneSystem's GenerateLocationsTimeSliced for one kind, on a world no zone of which is generated yet.
	private sealed class Placer(WorldGenerator gen, Lazy<BiomeMap> map)
	{
		public readonly Dictionary<(int X, int Y), Placed> Instances = new();
		private readonly Dictionary<string, List<Placed>> byPrefab = new();
		private readonly Dictionary<string, List<Placed>> byGroup = new();
		private readonly Dictionary<string, List<Placed>> byGroupMax = new();
		private readonly List<float> surround = new();

		private static (int X, int Y) Zone(float x, float z) =>
			((int)MathF.Floor((float)((x + 32.0) / 64.0)), (int)MathF.Floor((float)((z + 32.0) / 64.0)));

		public void Generate(Rule rule)
		{
			Rnd.InitState(gen.GetSeed() + StableHash.Of(rule.prefab));
			float maxRadius = MathF.Max(rule.exteriorRadius, rule.interiorRadius);
			int attempts = rule.prioritized != 0 ? 60000 : 12000;
			int placed = Instances.Values.Count(p => p.Rule.prefab == rule.prefab);
			float maxRange = 10000f;
			surround.Clear();
			if (rule.unique != 0 && placed > 0)
			{
				return;
			}
			if (rule.centerFirst != 0)
			{
				maxRange = rule.minDistance;
			}
			var biomes = (Heightmap.Biome)rule.biome;
			for (int i = 0; i < attempts && placed < rule.quantity; i++)
			{
				(int X, int Y) zone;
				if (rule.centerFirst != 0)
				{
					zone = RandomZone(maxRange);
					maxRange++;
				}
				else
				{
					var (px, pz) = map.Value.RandomPoint(biomes, rule.minAltitude >= 0f);
					zone = Zone(px, pz);
				}
				if (Instances.ContainsKey(zone))
				{
					continue;
				}
				if (((Heightmap.BiomeArea)rule.biomeArea & gen.GetBiomeArea(new Vector2s(zone.X * 64, zone.Y * 64))) == 0)
				{
					continue;
				}
				for (int j = 0; j < 6; j++)
				{
					if (TryPoint(rule, zone, maxRadius, biomes))
					{
						placed++;
						break;
					}
				}
			}
		}

		// One try at a random point of the zone: registered when every check passes.
		private bool TryPoint(Rule rule, (int X, int Y) zone, float maxRadius, Heightmap.Biome biomes)
		{
			float x = zone.X * 64f + Rnd.Range(-32f + maxRadius, 32f - maxRadius);
			float z = zone.Y * 64f + Rnd.Range(-32f + maxRadius, 32f - maxRadius);
			var p = new Vector3(x, 0f, z);
			float magnitude = p.magnitude;
			if ((rule.minDistance != 0f && magnitude < rule.minDistance) || (rule.maxDistance != 0f && magnitude > rule.maxDistance))
			{
				return false;
			}
			if ((biomes & gen.GetBiome(p)) == 0)
			{
				return false;
			}
			p.y = gen.GetHeight(p.x, p.z, out Color mask);
			float altitude = (float)(p.y - 30.0);
			if (altitude < rule.minAltitude || altitude > rule.maxAltitude)
			{
				return false;
			}
			if (rule.inForest != 0)
			{
				float forest = WorldGenerator.GetForestFactor(p);
				if (forest < rule.forestTresholdMin || forest > rule.forestTresholdMax)
				{
					return false;
				}
			}
			if (rule.minDistanceFromCenter > 0f || rule.maxDistanceFromCenter > 0f)
			{
				float d = MathF.Sqrt(p.x * p.x + p.z * p.z);
				if ((rule.minDistanceFromCenter > 0f && d < rule.minDistanceFromCenter) || (rule.maxDistanceFromCenter > 0f && d > rule.maxDistanceFromCenter))
				{
					return false;
				}
			}
			gen.GetTerrainDelta(p, rule.exteriorRadius, out float delta, out _);
			if (delta > rule.maxTerrainDelta || delta < rule.minTerrainDelta)
			{
				return false;
			}
			if (rule.minDistanceFromSimilar > 0f && InRange(rule.prefab, rule.group, p, rule.minDistanceFromSimilar, false))
			{
				return false;
			}
			if (rule.maxDistanceFromSimilar > 0f && !InRange(rule.prefab, rule.groupMax, p, rule.maxDistanceFromSimilar, true))
			{
				return false;
			}
			if ((rule.minimumVegetation > 0f && mask.a <= rule.minimumVegetation) || (rule.maximumVegetation < 1f && mask.a >= rule.maximumVegetation))
			{
				return false;
			}
			// Alt biomes: not known (see the top). A kind an alt biome adds needs one here; one it blocks is
			// refused there. Both are left out of the result; treating every spot as plain keeps the
			// others' order of draws.
			if (rule.altBiome != null)
			{
				return false;
			}
			if (rule.surroundCheckVegetation != 0)
			{
				float sum = 0f;
				for (int layer = 0; layer < rule.surroundCheckLayers; layer++)
				{
					float r = (float)(layer + 1) / rule.surroundCheckLayers * rule.surroundCheckDistance;
					for (int k = 0; k < 6; k++)
					{
						float f = k / 6f * MathF.PI * 2f;
						gen.GetHeight(p.x + MathF.Sin(f) * r, p.z + MathF.Cos(f) * r, out Color m);
						sum += m.a * ((rule.surroundCheckDistance - r) / (rule.surroundCheckDistance * 2f));
					}
				}
				surround.Add(sum);
				if (surround.Count < 10)
				{
					return false;
				}
				float max = surround.Max(), average = surround.Average();
				if (sum < average + (max - average) * rule.surroundBetterThanAverage)
				{
					return false;
				}
			}
			Register(rule, p);
			return true;
		}

		private static (int X, int Y) RandomZone(float range)
		{
			int n = (int)range / 64;
			while (true)
			{
				int x = Rnd.Range(-n, n), y = Rnd.Range(-n, n);
				float zx = x * 64f, zy = y * 64f;
				if (MathF.Sqrt(zx * zx + zy * zy) < 10000f)
				{
					return (x, y);
				}
			}
		}

		private void Register(Rule rule, Vector3 p)
		{
			var zone = Zone(p.x, p.z);
			if (!Instances.TryAdd(zone, new Placed(rule, p.x, p.y, p.z)))
			{
				return;
			}
			var placed = Instances[zone];
			Add(byPrefab, rule.prefab, placed);
			Add(byGroup, rule.group, placed);
			Add(byGroupMax, rule.groupMax, placed);
			static void Add(Dictionary<string, List<Placed>> d, string key, Placed p)
			{
				if (!d.TryGetValue(key, out var list))
				{
					d[key] = list = new();
				}
				list.Add(p);
			}
		}

		// HaveLocationInRange: one of the same prefab, or of the group, within radius.
		private bool InRange(string prefab, string group, Vector3 p, float radius, bool maxGroup)
		{
			bool Any(List<Placed> l)
			{
				foreach (Placed o in l)
				{
					float dx = o.X - p.x, dy = o.Y - p.y, dz = o.Z - p.z;
					if (dx * dx + dy * dy + dz * dz < radius * radius)
					{
						return true;
					}
				}
				return false;
			}
			if (byPrefab.TryGetValue(prefab, out var same) && Any(same))
			{
				return true;
			}
			if (group.Length > 0 && (maxGroup ? byGroupMax : byGroup).TryGetValue(group, out var g) && Any(g))
			{
				return true;
			}
			return false;
		}
	}
}
