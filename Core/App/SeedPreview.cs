using System.Security.Cryptography;
using TerrainEditor.Save;
using ValheimGen;

namespace TerrainEditor.App;

// A seed seen from above before its world exists (the New world page): the game's own generator
// (WorldGenerator) sampled on a coarse grid over the whole world, and what a player would care about:
// how much land, which biomes, the main continent, where the start is (SeedLocations: where the game
// will put its start temple), how far each biome is from there and, when asked, where the bosses and
// traders will be.
public static class SeedPreview
{
	public const float Radius = WorldGenerator.worldSize;

	// The biomes counted, in the order players meet them.
	public static readonly Heightmap.Biome[] Biomes =
	{
		Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain,
		Heightmap.Biome.Plains, Heightmap.Biome.Mistlands, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth,
	};

	// The places worth knowing about before playing: the start, the traders, the bosses' altars.
	public static readonly (string Prefab, string Name, string Kind)[] Landmarks =
	{
		("StartTemple", "The start", "start"),
		("Vendor_BlackForest", "Haldor", "trader"),
		("BogWitch_Camp", "The Bog Witch", "trader"),
		("Hildir_camp", "Hildir", "trader"),
		("Eikthyrnir", "Eikthyr", "boss"),
		("GDKing", "The Elder", "boss"),
		("Bonemass", "Bonemass", "boss"),
		("Dragonqueen", "Moder", "boss"),
		("GoblinKing", "Yagluth", "boss"),
		("Mistlands_DvergrBossEntrance1", "The Queen", "boss"),
		("FaderLocation", "Fader", "boss"),
	};

	// The bosses of the first five biomes, for the "bosses within" wish.
	public static readonly string[] EarlyBosses = { "Eikthyr", "The Elder", "Bonemass", "Moder", "Yagluth" };

	// One landmark: OneOf when the game lays out several spots and keeps only the first a player comes
	// near (the traders), the rest vanishing.
	public sealed record Landmark(string Name, string Kind, float X, float Z, bool OneOf);

	public sealed record Stats(
		float Land, // share of the world (inside its 10 km) that is land
		IReadOnlyDictionary<Heightmap.Biome, float> Shares, // share of the land in each biome
		float MainContinent, // share of the land in the largest landmass
		(float X, float Z) Start, // where the start is likely to be
		float LandNearStart, // share of land within 400 m of it
		float StartContinent, // share of the land in the start's own landmass
		IReadOnlyDictionary<Heightmap.Biome, float> Distance, // metres from the start to the nearest of each biome (PositiveInfinity: none)
		IReadOnlyDictionary<Heightmap.Biome, bool> SameLand, // whether that nearest one is on the start's landmass
		IReadOnlyDictionary<string, float>? Nearest = null, // metres from the start to the nearest of each landmark (null: not looked for)
		bool TempleStart = false); // Start is the game's start temple (else the Meadows nearest the middle: no rules from the game)

	// Size x Size cells of Cell metres over the world, row 0 the south: biome (index in Biomes, 255
	// ocean or outside), ground height.
	public sealed record Preview(string Seed, int Size, float Cell, byte[] Biome, float[] Height, Stats Stats, IReadOnlyList<Landmark>? Landmarks = null)
	{
		public (float X, float Z) CellCenter(int i, int j) => (-Radius + (i + 0.5f) * Cell, -Radius + (j + 0.5f) * Cell);
	}

	public const byte Ocean = 255;

	public static WorldGenerator Generator(string seedName) => WorldGenerator.Create(new World
	{
		m_seedName = seedName, m_seed = WorldCreator.SeedOf(seedName), m_worldGenVersion = WorldCreator.WorldGenVersion,
	});

	// The seed's preview on size x size cells (128: a quick look, 256: a clearer one); landmarks: where the
	// bosses and traders will be too (a fraction of a second more, a few seconds on one core).
	public static Preview Make(string seedName, int size = 160, bool parallel = false, bool landmarks = false, CancellationToken cancel = default)
	{
		var gen = Generator(seedName);
		float cell = 2 * Radius / size;
		var biome = new byte[size * size];
		var height = new float[size * size];
		// Rows side by side on every core but one when asked (the page's preview); one at a time in a
		// search, which runs seeds side by side instead.
		var options = new ParallelOptions { CancellationToken = cancel, MaxDegreeOfParallelism = parallel ? Math.Max(1, Environment.ProcessorCount - 1) : 1 };
		Parallel.For(0, size, options, j =>
		{
			for (int i = 0; i < size; i++)
			{
				float x = -Radius + (i + 0.5f) * cell, z = -Radius + (j + 0.5f) * cell;
				int k = j * size + i;
				if (x * x + z * z > Radius * Radius)
				{
					biome[k] = Ocean;
					height[k] = 0;
					continue;
				}
				float h = gen.GetHeight(new Vector2(x, z));
				height[k] = h;
				var b = gen.GetBiome(x, z);
				int index = Array.IndexOf(Biomes, b);
				// Land: ground a player can stand on or wade through (at most 1 m under water; 3 m in a
				// Swamp, whose pools are part of it). The game names much of the shallow sea after the land
				// biome near it (and the southern sea Ashlands): deeper than that, it is sea here.
				bool swamp = b == Heightmap.Biome.Swamp;
				biome[k] = index < 0 || h < TerrainService.WaterLevel - (swamp ? 3 : 1) ? Ocean : (byte)index;
			}
		});
		var placed = SeedLocations.Place(gen, landmarks ? SeedLocations.Which.Prioritized : SeedLocations.Which.Start, parallel, cancel);
		var marks = new List<Landmark>();
		foreach (var p in placed)
		{
			int at = Array.FindIndex(Landmarks, l => l.Prefab == p.Rule.prefab);
			if (at >= 0)
			{
				marks.Add(new Landmark(Landmarks[at].Name, Landmarks[at].Kind, p.X, p.Z, p.Rule.unique != 0 && p.Rule.quantity > 1));
			}
		}
		var start = marks.FirstOrDefault(m => m.Kind == "start");
		var stats = Measure(biome, size, cell, start is null ? null : (start.X, start.Z)) with { TempleStart = start != null };
		landmarks &= SeedLocations.Available;
		if (landmarks)
		{
			stats = stats with
			{
				Nearest = Landmarks.Select(l => l.Name).Distinct().ToDictionary(n => n, n => marks.Where(m => m.Name == n)
					.Select(m => MathF.Sqrt((m.X - stats.Start.X) * (m.X - stats.Start.X) + (m.Z - stats.Start.Z) * (m.Z - stats.Start.Z)))
					.DefaultIfEmpty(float.PositiveInfinity).Min()),
			};
		}
		return new Preview(seedName, size, cell, biome, height, stats, landmarks ? marks : null);
	}

	// start: where the game puts its start temple (null: the Meadows cell nearest the middle).
	internal static Stats Measure(byte[] biome, int size, float cell, (float X, float Z)? startAt = null)
	{
		int inside = 0, land = 0;
		var counts = new int[Biomes.Length];
		for (int j = 0; j < size; j++)
		{
			for (int i = 0; i < size; i++)
			{
				float x = -Radius + (i + 0.5f) * cell, z = -Radius + (j + 0.5f) * cell;
				if (x * x + z * z > Radius * Radius)
				{
					continue;
				}
				inside++;
				if (biome[j * size + i] != Ocean)
				{
					land++;
					counts[biome[j * size + i]]++;
				}
			}
		}
		// Landmasses: land cells joined side by side.
		var mass = new int[size * size];
		Array.Fill(mass, -1);
		var sizes = new List<int>();
		var queue = new Queue<int>();
		for (int seedCell = 0; seedCell < mass.Length; seedCell++)
		{
			if (biome[seedCell] == Ocean || mass[seedCell] >= 0)
			{
				continue;
			}
			int id = sizes.Count, n = 0;
			mass[seedCell] = id;
			queue.Enqueue(seedCell);
			while (queue.Count > 0)
			{
				int c = queue.Dequeue();
				n++;
				int ci = c % size, cj = c / size;
				foreach (var (di, dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
				{
					int ni = ci + di, nj = cj + dj;
					if (ni < 0 || nj < 0 || ni >= size || nj >= size)
					{
						continue;
					}
					int nk = nj * size + ni;
					if (biome[nk] != Ocean && mass[nk] < 0)
					{
						mass[nk] = id;
						queue.Enqueue(nk);
					}
				}
			}
			sizes.Add(n);
		}
		// The start: the Meadows cell nearest the middle.
		int meadows = Array.IndexOf(Biomes, Heightmap.Biome.Meadows);
		int startCell = -1;
		float best = float.MaxValue;
		for (int k = 0; k < biome.Length; k++)
		{
			if (biome[k] != meadows)
			{
				continue;
			}
			float x = -Radius + (k % size + 0.5f) * cell, z = -Radius + (k / size + 0.5f) * cell, d = x * x + z * z;
			if (d < best)
			{
				best = d;
				startCell = k;
			}
		}
		var start = startCell < 0 ? (0f, 0f) : (-Radius + (startCell % size + 0.5f) * cell, -Radius + (startCell / size + 0.5f) * cell);
		if (startAt is { } at)
		{
			// The temple's own cell, or the nearest land cell (a cell is coarse: the temple can sit on its
			// shore) for its landmass.
			start = (at.X, at.Z);
			startCell = -1;
			best = float.MaxValue;
			for (int k = 0; k < biome.Length; k++)
			{
				if (biome[k] == Ocean)
				{
					continue;
				}
				float dx = -Radius + (k % size + 0.5f) * cell - at.X, dz = -Radius + (k / size + 0.5f) * cell - at.Z, d = dx * dx + dz * dz;
				if (d < best)
				{
					best = d;
					startCell = k;
				}
			}
		}
		int startMass = startCell < 0 ? -1 : mass[startCell];
		// Around the start, and the nearest of each biome.
		int near = 0, nearLand = 0;
		var distance = Biomes.ToDictionary(b => b, _ => float.PositiveInfinity);
		var same = Biomes.ToDictionary(b => b, _ => false);
		for (int k = 0; k < biome.Length; k++)
		{
			float x = -Radius + (k % size + 0.5f) * cell, z = -Radius + (k / size + 0.5f) * cell;
			float d = MathF.Sqrt((x - start.Item1) * (x - start.Item1) + (z - start.Item2) * (z - start.Item2));
			if (d <= 400 + cell / 2)
			{
				near++;
				nearLand += biome[k] != Ocean ? 1 : 0;
			}
			if (biome[k] == Ocean)
			{
				continue;
			}
			var b = Biomes[biome[k]];
			if (d < distance[b])
			{
				distance[b] = d;
				same[b] = mass[k] == startMass;
			}
		}
		return new Stats(
			inside == 0 ? 0 : (float)land / inside,
			Biomes.Select((b, i) => (b, i)).ToDictionary(p => p.b, p => land == 0 ? 0f : (float)counts[p.i] / land),
			land == 0 || sizes.Count == 0 ? 0 : (float)sizes.Max() / land,
			start,
			near == 0 ? 0 : (float)nearLand / near,
			land == 0 || startMass < 0 ? 0 : (float)sizes[startMass] / land,
			distance,
			same);
	}

	// A random seed name as the game makes them: 10 letters and digits.
	public static string RandomSeed()
	{
		const string Letters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
		return string.Create(10, 0, (span, _) =>
		{
			for (int i = 0; i < span.Length; i++)
			{
				span[i] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
			}
		});
	}

	// What makes a seed good: each criterion adds to the score (0 when it is not asked for).
	public sealed record Wishes(
		bool BigStartLand = true, // the start on a large landmass
		bool LandAroundStart = true, // little water right around the start
		float MaxSwampDistance = 0, // the first Swamp within this many metres of the start (0: no wish)
		float MaxMountainDistance = 0,
		float MaxPlainsDistance = 0,
		bool BiomesOnStartLand = false, // the nearest of each biome on the start's landmass (no sailing needed)
		float MinLand = 0, // at least this share of land
		float MaxLand = 1, // at most this share
		float MaxHaldorDistance = 0, // the nearest of each trader's spots within this many metres (0: no wish)
		float MaxBogWitchDistance = 0,
		float MaxHildirDistance = 0,
		float MaxBossDistance = 0) // the nearest altar of each of the first five bosses within this many metres
	{
		// Whether the bosses and traders must be looked for.
		public bool NeedsLandmarks => MaxHaldorDistance > 0 || MaxBogWitchDistance > 0 || MaxHildirDistance > 0 || MaxBossDistance > 0;
	}

	// How well a preview meets the wishes, from 0 up (higher is better).
	public static float Score(Stats s, Wishes w)
	{
		float score = 0;
		if (w.BigStartLand)
		{
			score += 3 * s.StartContinent;
		}
		if (w.LandAroundStart)
		{
			score += 2 * s.LandNearStart;
		}
		static float Within(float d, float max) => max <= 0 ? 0 : float.IsPositiveInfinity(d) ? -2 : d <= max ? 1 + (max - d) / max : -(d - max) / max;
		float Near(Heightmap.Biome b, float max) => Within(s.Distance[b], max);
		score += Near(Heightmap.Biome.Swamp, w.MaxSwampDistance) + Near(Heightmap.Biome.Mountain, w.MaxMountainDistance) + Near(Heightmap.Biome.Plains, w.MaxPlainsDistance);
		if (s.Nearest is { } n)
		{
			float To(string name) => n.TryGetValue(name, out float d) ? d : float.PositiveInfinity;
			score += Within(To("Haldor"), w.MaxHaldorDistance) + Within(To("The Bog Witch"), w.MaxBogWitchDistance) + Within(To("Hildir"), w.MaxHildirDistance);
			if (w.MaxBossDistance > 0)
			{
				score += EarlyBosses.Average(b => Within(To(b), w.MaxBossDistance));
			}
		}
		if (w.BiomesOnStartLand)
		{
			score += 0.25f * s.SameLand.Count(kv => kv.Key is not (Heightmap.Biome.AshLands or Heightmap.Biome.DeepNorth) && kv.Value);
		}
		if (s.Land < w.MinLand)
		{
			score -= 5 * (w.MinLand - s.Land);
		}
		if (s.Land > w.MaxLand)
		{
			score -= 5 * (s.Land - w.MaxLand);
		}
		return MathF.Max(0, score);
	}

	// Random seeds tried in parallel (count of them), the best kept (keep), scored by the wishes; progress
	// is told after each seed (done, total). Previews of size cells (small: many seeds quickly).
	public static List<(Preview Preview, float Score)> Search(Wishes wishes, int count, int keep, int size = 96, Action<int, int>? progress = null, CancellationToken cancel = default)
	{
		var found = new List<(Preview, float)>();
		int done = 0;
		var options = new ParallelOptions { CancellationToken = cancel, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) };
		try
		{
			Parallel.For(0, count, options, _ =>
			{
				var p = Make(RandomSeed(), size, landmarks: wishes.NeedsLandmarks, cancel: cancel);
				float score = Score(p.Stats, wishes);
				lock (found)
				{
					found.Add((p, score));
					found.Sort((a, b) => b.Item2.CompareTo(a.Item2));
					if (found.Count > keep)
					{
						found.RemoveAt(found.Count - 1);
					}
				}
				progress?.Invoke(Interlocked.Increment(ref done), count);
			});
		}
		catch (OperationCanceledException)
		{
			// Stopped: what was found so far.
		}
		lock (found)
		{
			return found.ToList();
		}
	}
}
