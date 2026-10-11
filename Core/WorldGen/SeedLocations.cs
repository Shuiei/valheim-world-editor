using System.Text.Json;
using TerrainEditor.Save;
using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

// Where the game will lay out a seed's locations (start temple, traders, bosses, dungeons...) when its
// world first loads, before the world exists: ZoneSystem.GenerateLocations replayed with the game's
// rules on the editor's copy of its generator. The rules are read from the game's files (GameLocations,
// then Use); until then, or when they cannot be read, there are none (Available) and nothing is placed.
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

	private static volatile State _state = new(new RuleSet());

	// The rules read from the game (or the tests' copy, tests/fixtures/locations.json).
	public static void Use(RuleSet rules) => _state = new State(rules);

	// Whether there are rules to place with.
	public static bool Available => _state.Rules.locations.Count > 0;

	// The kinds laid out first (start, bosses, traders...), as the tests' copy keeps them: what the
	// editor shows, and all they need.
	public static RuleSet Prioritized(RuleSet rules) => new()
	{
		locations = rules.locations.Where(l => l.prioritized != 0 && l.enable != 0 && l.quantity != 0).ToList(),
	};

	public static RuleSet Current => _state.Rules;

	// One location instance the game will register: its kind and where (y: the generator's ground height).
	public sealed record Placed(Rule Rule, float X, float Y, float Z);

	// The kinds whose spots depend on the alt biomes (added by one, or blocked by one somewhere).
	public static bool Uncertain(Rule r) => Uncertain(_state, r);

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
	public static List<Placed> Place(WorldGenerator gen, Which which = Which.Prioritized, bool parallel = false, CancellationToken cancel = default) =>
		new Layout(gen, parallel).Place(which, cancel);

	// The instances of these kinds (prefab names), the game's kinds before them laid out too (a few
	// seconds when it goes past the prioritized ones). A kind an alt biome blocks somewhere is given
	// everywhere (close to the game: an alt biome covers a few areas only).
	public static List<Placed> PlaceKinds(WorldGenerator gen, IReadOnlyCollection<string> prefabs, bool parallel = false, CancellationToken cancel = default) =>
		new Layout(gen, parallel).PlaceKinds(prefabs, cancel);

	private static bool Uncertain(State state, Rule r) => r.altBiome != null || state.Blocked.Contains(r.name);

	// A seed's layout under way, kept to go on later without starting again (the dungeons after the
	// start, bosses and traders): the game's kinds laid out in its order so far, and its biome map.
	// One caller at a time.
	public sealed class Layout
	{
		private readonly WorldGenerator _gen;
		private readonly bool _parallel;
		private readonly State _state = SeedLocations._state;
		private readonly object _sync = new();
		private BiomeMap? _map;
		private Placer _placer;
		private int _done;
		private CancellationToken _cancel;

		// parallel: the biome map on every core but one.
		public Layout(WorldGenerator gen, bool parallel = false)
		{
			_gen = gen;
			_parallel = parallel;
			_placer = NewPlacer();
		}

		private Placer NewPlacer() => new(_gen, () => _map ??= new BiomeMap(_gen, _parallel, _cancel));

		public List<Placed> Place(Which which, CancellationToken cancel = default)
		{
			int count = which switch
			{
				Which.Start => 1,
				Which.Prioritized => _state.Ordered.Count(r => r.prioritized != 0),
				_ => _state.Ordered.Count,
			};
			return Until(count, cancel).Where(p => !Uncertain(_state, p.Rule)).ToList();
		}

		public List<Placed> PlaceKinds(IReadOnlyCollection<string> prefabs, CancellationToken cancel = default)
		{
			int count = _state.Ordered.FindLastIndex(r => prefabs.Contains(r.prefab)) + 1;
			return Until(count, cancel).Where(p => prefabs.Contains(p.Rule.prefab) && p.Rule.altBiome == null).ToList();
		}

		// The first count kinds of the game's order laid out (those already done kept): every instance
		// so far. Stopped half way, it starts again next time (a kind half laid out is no use).
		private List<Placed> Until(int count, CancellationToken cancel)
		{
			lock (_sync)
			{
				_cancel = cancel;
				Rnd.State saved = Rnd.state;
				try
				{
					for (; _done < Math.Min(count, _state.Ordered.Count); _done++)
					{
						cancel.ThrowIfCancellationRequested();
						_placer.Generate(_state.Ordered[_done]);
					}
				}
				catch (OperationCanceledException)
				{
					_placer = NewPlacer();
					_done = 0;
					throw;
				}
				finally
				{
					Rnd.state = saved;
				}
				return _placer.Instances.Values.ToList();
			}
		}
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

		// One byte a point: the biome index, at or above sea level, met by the fill.
		private readonly byte[] _cell = new byte[Size * Size];

		public BiomeMap(WorldGenerator gen, bool parallel, CancellationToken cancel)
		{
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
						_cell[k] = Sea;
						continue;
					}
					Heightmap.Biome b = gen.GetBiome(wx, wy);
					_cell[k] = (byte)(Index(b) | (gen.GetBiomeHeight(b, wx, wy, out _) >= 30f ? Above : 0));
				}
			});
		}

		// A biome's points listed the first time they are asked for. Its order depends on its own points
		// only (the game's fill never crosses into another biome), so each is listed alone.
		private void List(int biome)
		{
			if (All[biome] != null)
			{
				return;
			}
			var all = new List<int>();
			var above = new List<int>();
			void Add(int x, int y)
			{
				all.Add(x | (y << 16));
				if ((_cell[x + y * Size] & Above) != 0)
				{
					above.Add(x | (y << 16));
				}
			}
			bool Is(int x, int y) => (_cell[x + y * Size] & BiomeBits) == biome;
			if (biome is Ash or North or Sea)
			{
				// Ashlands, Deep North and the ocean: one sector each, in rows.
				for (int y = 0; y < Size; y++)
				{
					for (int x = 0; x < Size; x++)
					{
						if (Is(x, y))
						{
							Add(x, y);
						}
					}
				}
			}
			else
			{
				// Each area flood filled from its first point in rows. That first point is not listed (the
				// game only lists the points its fill reaches).
				var stack = new Stack<int>(1024);
				for (int y = 0; y < Size; y++)
				{
					for (int x = 0; x < Size; x++)
					{
						if (!Is(x, y) || (_cell[x + y * Size] & Visited) != 0)
						{
							continue;
						}
						_cell[x + y * Size] |= Visited;
						stack.Push(x | (y << 16));
						while (stack.Count > 0)
						{
							int c = stack.Pop();
							int cx = c & 0xFFFF, cy = c >> 16;
							Fill(cx + 1, cy);
							Fill(cx - 1, cy);
							Fill(cx, cy + 1);
							Fill(cx, cy - 1);
						}
					}
				}
				void Fill(int fx, int fy)
				{
					if (fx >= 0 && fy >= 0 && fx < Size && fy < Size && Is(fx, fy) && (_cell[fx + fy * Size] & Visited) == 0)
					{
						_cell[fx + fy * Size] |= Visited;
						Add(fx, fy);
						stack.Push(fx | (fy << 16));
					}
				}
			}
			All[biome] = all.ToArray();
			AboveSea[biome] = above.ToArray();
		}

		// How the game turns a set of biomes into one to draw a point from: a single biome is itself;
		// otherwise a random step along this walk, counting only the steps whose biome is in the set
		// (and never reaching the last one: the draw stops one short). Two of the game's steps give
		// another biome than the one they test (Plains gives Black Forest; the eighth step tests
		// Meadows and gives the ocean); past the walk, Mistlands.
		private static readonly (Heightmap.Biome Test, Heightmap.Biome Gives)[] Walk =
		{
			(Heightmap.Biome.Meadows, Heightmap.Biome.Meadows),
			(Heightmap.Biome.Swamp, Heightmap.Biome.Swamp),
			(Heightmap.Biome.Mountain, Heightmap.Biome.Mountain),
			(Heightmap.Biome.BlackForest, Heightmap.Biome.BlackForest),
			(Heightmap.Biome.Plains, Heightmap.Biome.BlackForest),
			(Heightmap.Biome.AshLands, Heightmap.Biome.AshLands),
			(Heightmap.Biome.DeepNorth, Heightmap.Biome.DeepNorth),
			(Heightmap.Biome.Meadows, Heightmap.Biome.Ocean),
		};

		private static readonly Heightmap.Biome[] Counted = ByIndex.Skip(1).ToArray();

		public static Heightmap.Biome RandomBiome(Heightmap.Biome set)
		{
			if (System.Numerics.BitOperations.IsPow2((uint)set) || set == 0)
			{
				return set;
			}
			int steps = Rnd.Range(0, Counted.Count(b => (set & b) != 0) - 1);
			foreach (var (test, gives) in Walk)
			{
				if ((set & test) == 0)
				{
					continue;
				}
				if (steps == 0)
				{
					return gives;
				}
				steps--;
			}
			return Heightmap.Biome.Mistlands;
		}

		// GetRandomPointByBiomes and GetRandomPointByBiomesAboveSeaLevel, in world space.
		public (float X, float Z) RandomPoint(Heightmap.Biome biomes, bool aboveSea)
		{
			int b = Index(RandomBiome(biomes));
			List(b);
			int[] list = aboveSea && AboveSea[b].Length > 0 ? AboveSea[b] : All[b];
			int p = list[Rnd.Range(0, list.Length)];
			return (MapToWorld(p & 0xFFFF), MapToWorld(p >> 16));
		}
	}

	// Lays out kinds one after another as the game does when a world first loads (no zone made yet):
	// for each kind, zones drawn at random (near the middle, or from the biome map), a few spots tried
	// in each, the first spot that passes every check kept, until the kind has its number or its tries
	// run out. The checks and the random draws come in the game's order: a draw more or less, and every
	// later spot of the kind moves.
	private sealed class Placer(WorldGenerator gen, Func<BiomeMap> map)
	{
		// The instances by zone (one a zone), and the same by prefab and by group, for the distance checks.
		public readonly Dictionary<(int X, int Y), Placed> Instances = new();
		private readonly Neighbours _byPrefab = new(), _byGroup = new(), _byGroupMax = new();
		// The vegetation sums of the kind's spots so far (the "stands out" check).
		private readonly List<float> _sums = new();

		private const int SpotsAZone = 6;

		public void Generate(Rule rule)
		{
			Rnd.InitState(gen.GetSeed() + StableHash.Of(rule.prefab));
			int have = Instances.Values.Count(p => p.Rule.prefab == rule.prefab);
			_sums.Clear();
			if (rule.unique != 0 && have > 0)
			{
				return;
			}
			var biomes = (Heightmap.Biome)rule.biome;
			float margin = MathF.Max(rule.exteriorRadius, rule.interiorRadius);
			// Kinds laid out from the middle out (the start temple): the reach grows a metre a try.
			float reach = rule.minDistance;
			for (int left = rule.prioritized != 0 ? 60000 : 12000; left > 0 && have < rule.quantity; left--)
			{
				var zone = rule.centerFirst != 0 ? ZoneNearMiddle(reach++) : ZoneOf(map().RandomPoint(biomes, rule.minAltitude >= 0f));
				if (Instances.ContainsKey(zone) || ((Heightmap.BiomeArea)rule.biomeArea & gen.GetBiomeArea(new Vector2s(zone.X * 64, zone.Y * 64))) == 0)
				{
					continue;
				}
				for (int s = 0; s < SpotsAZone; s++)
				{
					if (TrySpot(rule, biomes, zone, margin))
					{
						have++;
						break;
					}
				}
			}
		}

		private static (int X, int Y) ZoneOf((float X, float Z) p) =>
			((int)MathF.Floor((float)((p.X + 32.0) / 64.0)), (int)MathF.Floor((float)((p.Z + 32.0) / 64.0)));

		// A zone whose corner lies within reach of the middle (and inside the world).
		private static (int X, int Y) ZoneNearMiddle(float reach)
		{
			int n = (int)reach / 64;
			while (true)
			{
				var zone = (X: Rnd.Range(-n, n), Y: Rnd.Range(-n, n));
				float cx = zone.X * 64f, cy = zone.Y * 64f;
				if (MathF.Sqrt(cx * cx + cy * cy) < 10000f)
				{
					return zone;
				}
			}
		}

		// One spot of the zone (kept far enough from its edges for the kind's size), checked step by step.
		private bool TrySpot(Rule rule, Heightmap.Biome biomes, (int X, int Y) zone, float margin)
		{
			var spot = new Vector3(zone.X * 64f + Rnd.Range(-32f + margin, 32f - margin), 0f, zone.Y * 64f + Rnd.Range(-32f + margin, 32f - margin));
			if (!AtRightDistance(rule, spot) || !OnRightGround(rule, biomes, ref spot, out float vegetation) || !FlatEnough(rule, spot))
			{
				return false;
			}
			// The steps after the slope's (which draws from the random sequence) must stay after it.
			if (!RightNeighbours(rule, spot) || !RightVegetation(rule, vegetation) || rule.altBiome != null || !StandsOut(rule, spot))
			{
				return false;
			}
			Register(rule, spot);
			return true;
		}

		// From the middle of the world: the kind's ring.
		private static bool AtRightDistance(Rule rule, Vector3 spot)
		{
			float fromMiddle = spot.magnitude;
			return !(rule.minDistance != 0f && fromMiddle < rule.minDistance) && !(rule.maxDistance != 0f && fromMiddle > rule.maxDistance);
		}

		// The biome, the height above the sea, the forest and the distance from the middle on the ground
		// (spot.y set to the ground; vegetation: how green the ground is there).
		private bool OnRightGround(Rule rule, Heightmap.Biome biomes, ref Vector3 spot, out float vegetation)
		{
			vegetation = 0;
			var biome = gen.GetBiome(spot);
			if ((biomes & biome) == 0)
			{
				return false;
			}
			spot.y = gen.GetBiomeHeight(biome, spot.x, spot.z, out Color mask);
			vegetation = mask.a;
			float aboveSea = (float)(spot.y - 30.0);
			if (aboveSea < rule.minAltitude || aboveSea > rule.maxAltitude)
			{
				return false;
			}
			if (rule.inForest != 0)
			{
				float forest = WorldGenerator.GetForestFactor(spot);
				if (forest < rule.forestTresholdMin || forest > rule.forestTresholdMax)
				{
					return false;
				}
			}
			if (rule.minDistanceFromCenter <= 0f && rule.maxDistanceFromCenter <= 0f)
			{
				return true;
			}
			float flat = MathF.Sqrt(spot.x * spot.x + spot.z * spot.z);
			return !(rule.minDistanceFromCenter > 0f && flat < rule.minDistanceFromCenter) && !(rule.maxDistanceFromCenter > 0f && flat > rule.maxDistanceFromCenter);
		}

		// The ground's rise across the kind's size (the generator samples it at random points).
		private bool FlatEnough(Rule rule, Vector3 spot)
		{
			gen.GetTerrainDelta(spot, rule.exteriorRadius, out float rise, out _);
			return rise >= rule.minTerrainDelta && rise <= rule.maxTerrainDelta;
		}

		// Far enough from its own prefab or group, and near enough to its "max" group when it needs one.
		private bool RightNeighbours(Rule rule, Vector3 spot)
		{
			if (rule.minDistanceFromSimilar > 0f && (_byPrefab.Within(rule.prefab, spot, rule.minDistanceFromSimilar) || (rule.group.Length > 0 && _byGroup.Within(rule.group, spot, rule.minDistanceFromSimilar))))
			{
				return false;
			}
			return !(rule.maxDistanceFromSimilar > 0f && !_byPrefab.Within(rule.prefab, spot, rule.maxDistanceFromSimilar) && !(rule.groupMax.Length > 0 && _byGroupMax.Within(rule.groupMax, spot, rule.maxDistanceFromSimilar)));
		}

		private static bool RightVegetation(Rule rule, float vegetation) =>
			!(rule.minimumVegetation > 0f && vegetation <= rule.minimumVegetation) && !(rule.maximumVegetation < 1f && vegetation >= rule.maximumVegetation);

		// Kinds that want the greenest spots: the vegetation on rings around the spot (nearer counts
		// more) must beat the average of the kind's spots so far by a share of the way to the best; the
		// first ten spots only set the bar.
		private bool StandsOut(Rule rule, Vector3 spot)
		{
			if (rule.surroundCheckVegetation == 0)
			{
				return true;
			}
			float total = 0f, far = rule.surroundCheckDistance;
			for (int ring = 1; ring <= rule.surroundCheckLayers; ring++)
			{
				float r = (float)ring / rule.surroundCheckLayers * far;
				float weight = (far - r) / (far * 2f);
				for (int k = 0; k < 6; k++)
				{
					float angle = k / 6f * MathF.PI * 2f;
					gen.GetHeight(spot.x + MathF.Sin(angle) * r, spot.z + MathF.Cos(angle) * r, out Color around);
					total += around.a * weight;
				}
			}
			_sums.Add(total);
			if (_sums.Count < 10)
			{
				return false;
			}
			float best = _sums.Max(), average = _sums.Average();
			return total >= average + (best - average) * rule.surroundBetterThanAverage;
		}

		private void Register(Rule rule, Vector3 spot)
		{
			var placed = new Placed(rule, spot.x, spot.y, spot.z);
			if (!Instances.TryAdd(ZoneOf((spot.x, spot.z)), placed))
			{
				return;
			}
			_byPrefab.Add(rule.prefab, placed);
			_byGroup.Add(rule.group, placed);
			_byGroupMax.Add(rule.groupMax, placed);
		}
	}

	// Instances by a name (prefab or group), for "is one within this distance" (3D, as the game measures).
	private sealed class Neighbours
	{
		private readonly Dictionary<string, List<Placed>> _byName = new();

		public void Add(string name, Placed p)
		{
			if (!_byName.TryGetValue(name, out var list))
			{
				_byName[name] = list = new();
			}
			list.Add(p);
		}

		public bool Within(string name, Vector3 at, float distance)
		{
			if (!_byName.TryGetValue(name, out var list))
			{
				return false;
			}
			float limit = distance * distance;
			foreach (var p in list)
			{
				float dx = p.X - at.x, dy = p.Y - at.y, dz = p.Z - at.z;
				if (dx * dx + dy * dy + dz * dz < limit)
				{
					return true;
				}
			}
			return false;
		}
	}
}
