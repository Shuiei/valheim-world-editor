using System.Text.Json;
using TerrainEditor.Save;
using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

/// <summary>
/// Where the game will lay out a seed's locations (start temple, traders, bosses, dungeons...) when its
/// world first loads, before the world exists: ZoneSystem.GenerateLocations replayed with the game's
/// rules on the editor's copy of its generator. The rules are read from the game's files (GameLocations,
/// then Use); until then, or when they cannot be read, there are none (Available) and nothing is placed.
/// </summary>
/// <remarks>
/// The game picks each try's zone from a biome map of the world (AltBiomeWorldData: 2048 x 2048 points
/// 12 m apart, each biome's points listed in the order its flood fill met them), then checks the spot
/// as ZoneSystem does. Each location kind starts Unity's Random afresh from the seed and its name, so
/// the result is the game's, with one exception: alt biomes (Dark Meadows, Lox Plains...) are given to
/// biome areas in an order the game shuffles with an unseeded random, different at each load. The few
/// kinds an alt biome adds or blocks cannot be known (Uncertain), and are left out; the prioritized
/// kinds (start, bosses, traders...) come first and never depend on them.
/// </remarks>
public static class SeedLocations
{
#pragma warning disable IDE1006 // named as the JSON keys they are read from and written as
    /// <summary>
    /// One location kind's rule (the game's ZoneSystem.ZoneLocation), as locations.json keeps it. Flags are 0 or 1.
    /// </summary>
    public sealed class Rule
    {
        /// <summary>
        /// The location's prefab name (the kind; also what seeds its random draws).
        /// </summary>
        public string prefab { get; set; } = "";
        /// <summary>
        /// The location's name, as alt biomes block it.
        /// </summary>
        public string name { get; set; } = "";
        /// <summary>
        /// Non-zero when the game lays it out.
        /// </summary>
        public int enable { get; set; }
        /// <summary>
        /// The biomes it goes in (Heightmap.Biome flags).
        /// </summary>
        public int biome { get; set; }
        /// <summary>
        /// Where in those biomes (Heightmap.BiomeArea flags: edge, middle).
        /// </summary>
        public int biomeArea { get; set; }
        /// <summary>
        /// How many the game tries to lay out.
        /// </summary>
        public int quantity { get; set; }
        /// <summary>
        /// Non-zero for the kinds laid out first (start, bosses, traders...), which also get more tries.
        /// </summary>
        public int prioritized { get; set; }
        /// <summary>
        /// Non-zero for kinds laid out from the middle out (the start temple).
        /// </summary>
        public int centerFirst { get; set; }
        /// <summary>
        /// Non-zero when one is all there may be.
        /// </summary>
        public int unique { get; set; }
        /// <summary>
        /// Its group, for minDistanceFromSimilar (empty: none).
        /// </summary>
        public string group { get; set; } = "";
        /// <summary>
        /// No instance of the same prefab or group within this many metres (0: no check).
        /// </summary>
        public float minDistanceFromSimilar { get; set; }
        /// <summary>
        /// Its "max" group, for maxDistanceFromSimilar (empty: none).
        /// </summary>
        public string groupMax { get; set; } = "";
        /// <summary>
        /// An instance of the same prefab or "max" group needed within this many metres (0: no check).
        /// </summary>
        public float maxDistanceFromSimilar { get; set; }
        /// <summary>
        /// Its inner radius, metres; the larger of it and exteriorRadius is kept from the zone's edges.
        /// </summary>
        public float interiorRadius { get; set; }
        /// <summary>
        /// Its outer radius, metres: the circle the slope is measured on (see also interiorRadius).
        /// </summary>
        public float exteriorRadius { get; set; }
        /// <summary>
        /// The least rise across its size, metres.
        /// </summary>
        public float minTerrainDelta { get; set; }
        /// <summary>
        /// The most rise across its size, metres.
        /// </summary>
        public float maxTerrainDelta { get; set; }
        /// <summary>
        /// The ground's vegetation (mask alpha) must be above this (0: no check).
        /// </summary>
        public float minimumVegetation { get; set; }
        /// <summary>
        /// The ground's vegetation must be below this (1: no check).
        /// </summary>
        public float maximumVegetation { get; set; }
        /// <summary>
        /// Non-zero for kinds that want the greenest spots (see StandsOut).
        /// </summary>
        public int surroundCheckVegetation { get; set; }
        /// <summary>
        /// How far the vegetation around the spot is read, metres.
        /// </summary>
        public float surroundCheckDistance { get; set; }
        /// <summary>
        /// On how many rings.
        /// </summary>
        public int surroundCheckLayers { get; set; }
        /// <summary>
        /// The share of the way from the average to the best a spot must reach.
        /// </summary>
        public float surroundBetterThanAverage { get; set; }
        /// <summary>
        /// Non-zero when the forest factor must lie between the two thresholds.
        /// </summary>
        public int inForest { get; set; }
        /// <summary>
        /// The least forest factor (lower: denser forest).
        /// </summary>
        public float forestTresholdMin { get; set; }
        /// <summary>
        /// The most forest factor.
        /// </summary>
        public float forestTresholdMax { get; set; }
        /// <summary>
        /// The least flat distance from the middle on the ground, metres (0: none).
        /// </summary>
        public float minDistanceFromCenter { get; set; }
        /// <summary>
        /// The most flat distance from the middle on the ground, metres (0: none).
        /// </summary>
        public float maxDistanceFromCenter { get; set; }
        /// <summary>
        /// The least distance from the middle, metres (0: none); where centerFirst kinds start.
        /// </summary>
        public float minDistance { get; set; }
        /// <summary>
        /// The most distance from the middle, metres (0: none).
        /// </summary>
        public float maxDistance { get; set; }
        /// <summary>
        /// The least height above the sea (30 m), metres; 0 or more draws only points above the sea.
        /// </summary>
        public float minAltitude { get; set; }
        /// <summary>
        /// The most height above the sea, metres.
        /// </summary>
        public float maxAltitude { get; set; }
        /// <summary>
        /// The alt biome that adds it; null for the game's own kinds.
        /// </summary>
        public string? altBiome { get; set; }
    }

    /// <summary>
    /// An alt biome (Dark Meadows, Lox Plains...) and the location kinds it keeps out of its areas.
    /// </summary>
    public sealed class AltBiomeRule
    {
        /// <summary>
        /// The alt biome's name.
        /// </summary>
        public string name { get; set; } = "";
        /// <summary>
        /// Names (Rule.name) of the location kinds it blocks.
        /// </summary>
        public List<string> blockLocationNames { get; set; } = [];
    }

    /// <summary>
    /// The game's rules: its locations in its order (ZoneSystem's list, the location lists, the alt
    /// biomes' additions) and the alt biomes.
    /// </summary>
    public sealed class RuleSet
    {
        /// <summary>
        /// The location kinds in the game's order.
        /// </summary>
        public List<Rule> locations { get; set; } = [];
        /// <summary>
        /// The alt biomes.
        /// </summary>
        public List<AltBiomeRule> altBiomes { get; set; } = [];

        /// <summary>
        /// As locations.json keeps them: one location or alt biome a line (nulls left out).
        /// </summary>
        public string ToJson() =>
            "{\"locations\": [\n" + string.Join(",\n", locations.Select(l => JsonSerializer.Serialize(l, JsonOptions))) +
            "\n],\n\"altBiomes\": [\n" + string.Join(",\n", altBiomes.Select(a => JsonSerializer.Serialize(a, JsonOptions))) + "\n]}\n";

        /// <summary>
        /// Reads locations.json; throws <see cref="JsonException"/> when it holds no rules.
        /// </summary>
        public static RuleSet FromJson(string json) => JsonSerializer.Deserialize<RuleSet>(json) ?? throw new JsonException("no rules");
    }
#pragma warning restore IDE1006

    /// <summary>
    /// How ToJson writes each line: nulls (a game kind's altBiome) left out.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    /// <summary>
    /// The rules in use and what follows from them, swapped whole (a search running keeps its own).
    /// </summary>
    /// <param name="rules">The rules.</param>
    private sealed class State(RuleSet rules)
    {
        /// <summary>
        /// The rules as given.
        /// </summary>
        public RuleSet Rules { get; } = rules;
        /// <summary>
        /// The game's order: prioritized kinds first, otherwise as listed; disabled and empty kinds left out.
        /// </summary>
        public List<Rule> Ordered { get; } = [.. rules.locations.OrderByDescending(l => l.prioritized != 0).Where(l => l.enable != 0 && l.quantity != 0)];
        /// <summary>
        /// Names of the kinds some alt biome blocks.
        /// </summary>
        public HashSet<string> Blocked { get; } = [.. rules.altBiomes.SelectMany(a => a.blockLocationNames)];
    }

    /// <summary>
    /// The rules in use; none until Use.
    /// </summary>
    private static volatile State _state = new(new RuleSet());

    /// <summary>
    /// The rules read from the game (or the tests' copy, tests/fixtures/locations.json).
    /// </summary>
    public static void Use(RuleSet rules) => _state = new State(rules);

    /// <summary>
    /// Whether there are rules to place with.
    /// </summary>
    public static bool Available => _state.Rules.locations.Count > 0;

    /// <summary>
    /// The kinds laid out first (start, bosses, traders...), as the tests' copy keeps them: what the
    /// editor shows, and all they need.
    /// </summary>
    public static RuleSet Prioritized(RuleSet rules) => new()
    {
        locations = [.. rules.locations.Where(l => l.prioritized != 0 && l.enable != 0 && l.quantity != 0)],
    };

    /// <summary>
    /// The rules in use.
    /// </summary>
    public static RuleSet Current => _state.Rules;

    /// <summary>
    /// One location instance the game will register: its kind and where.
    /// </summary>
    /// <param name="Rule">Its kind.</param>
    /// <param name="X">World x.</param>
    /// <param name="Y">The generator's ground height there, metres.</param>
    /// <param name="Z">World z.</param>
    public sealed record Placed(Rule Rule, float X, float Y, float Z);

    /// <summary>
    /// The kinds whose spots depend on the alt biomes (added by one, or blocked by one somewhere).
    /// </summary>
    public static bool Uncertain(Rule r) => Uncertain(_state, r);

    /// <summary>
    /// The game's order: prioritized kinds first, otherwise as listed; disabled and empty kinds left out.
    /// </summary>
    public static IReadOnlyList<Rule> Ordered => _state.Ordered;

    /// <summary>
    /// How far to lay out.
    /// </summary>
    public enum Which
    {
        /// <summary>
        /// The start temple only: the first kind laid out, near the middle (no biome map needed).
        /// </summary>
        Start,
        /// <summary>
        /// The kinds laid out first: start, bosses, traders, Hildir's dungeons... (about 0.3 s).
        /// </summary>
        Prioritized,
        /// <summary>
        /// Every kind (several seconds).
        /// </summary>
        All,
    }

    /// <summary>
    /// The seed's location instances, the uncertain kinds left out (a kind's instances are the same
    /// whichever is asked).
    /// </summary>
    /// <param name="gen">The seed's generator.</param>
    /// <param name="which">How far to lay out.</param>
    /// <param name="parallel">The biome map on every core but one.</param>
    /// <param name="cancel">Stops the layout (throws).</param>
    public static List<Placed> Place(WorldGenerator gen, Which which = Which.Prioritized, bool parallel = false, CancellationToken cancel = default) =>
        new Layout(gen, parallel).Place(which, cancel);

    /// <summary>
    /// The instances of these kinds (prefab names), the game's kinds before them laid out too (a few
    /// seconds when it goes past the prioritized ones). A kind an alt biome blocks somewhere is given
    /// everywhere (close to the game: an alt biome covers a few areas only).
    /// </summary>
    public static List<Placed> PlaceKinds(WorldGenerator gen, IReadOnlyCollection<string> prefabs, bool parallel = false, CancellationToken cancel = default) =>
        new Layout(gen, parallel).PlaceKinds(prefabs, cancel);

    /// <summary>
    /// Whether a kind depends on the alt biomes under these rules.
    /// </summary>
    private static bool Uncertain(State state, Rule r) => r.altBiome != null || state.Blocked.Contains(r.name);

    /// <summary>
    /// A seed's layout under way, kept to go on later without starting again (the dungeons after the
    /// start, bosses and traders): the game's kinds laid out in its order so far, and its biome map.
    /// One caller at a time.
    /// </summary>
    public sealed class Layout
    {
        /// <summary>
        /// The seed's generator.
        /// </summary>
        private readonly WorldGenerator _gen;
        /// <summary>
        /// Whether the biome map is built on every core but one.
        /// </summary>
        private readonly bool _parallel;
        /// <summary>
        /// The rules in use when the layout began, kept even if Use swaps them.
        /// </summary>
        private readonly State _state = SeedLocations._state;
        /// <summary>
        /// Lets one caller at a time go on with the layout.
        /// </summary>
        private readonly Lock _sync = new();
        /// <summary>
        /// The biome map, built when a kind first needs it.
        /// </summary>
        private BiomeMap? _map;
        /// <summary>
        /// The instances so far; a new one after a stopped or failed layout.
        /// </summary>
        private Placer _placer;
        /// <summary>
        /// How many kinds of the game's order are laid out.
        /// </summary>
        private int _done;
        /// <summary>
        /// The running call's token, for the biome map built during it.
        /// </summary>
        private CancellationToken _cancel;

        /// <summary>
        /// A layout of a seed, nothing laid out yet.
        /// </summary>
        /// <param name="gen">The seed's generator.</param>
        /// <param name="parallel">The biome map on every core but one.</param>
        public Layout(WorldGenerator gen, bool parallel = false)
        {
            _gen = gen;
            _parallel = parallel;
            _placer = NewPlacer();
        }

        /// <summary>
        /// A placer with no instances, sharing this layout's biome map.
        /// </summary>
        private Placer NewPlacer() => new(_gen, () => _map ??= new BiomeMap(_gen, _parallel, _cancel));

        /// <summary>
        /// The instances so far, laid out at least as far as <paramref name="which"/> says, the uncertain kinds
        /// left out.
        /// </summary>
        public List<Placed> Place(Which which, CancellationToken cancel = default)
        {
            int count = which switch
            {
                Which.Start => 1,
                Which.Prioritized => _state.Ordered.Count(r => r.prioritized != 0),
                _ => _state.Ordered.Count,
            };
            return [.. Until(count, cancel).Where(p => !Uncertain(_state, p.Rule))];
        }

        /// <summary>
        /// The instances of these kinds (prefab names), laid out as far as the last of them; kinds an alt biome
        /// adds left out.
        /// </summary>
        public List<Placed> PlaceKinds(IReadOnlyCollection<string> prefabs, CancellationToken cancel = default)
        {
            int count = _state.Ordered.FindLastIndex(r => prefabs.Contains(r.prefab)) + 1;
            return [.. Until(count, cancel).Where(p => prefabs.Contains(p.Rule.prefab) && p.Rule.altBiome == null)];
        }

        /// <summary>
        /// The first count kinds of the game's order laid out (those already done kept): every instance
        /// so far. Stopped or failed half way, it starts again next time (a kind half laid out is no use).
        /// The thread's Random state is left as it was.
        /// </summary>
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
                catch
                {
                    // Stopped or failed half way through a kind: nothing of it is kept.
                    _placer = NewPlacer();
                    _done = 0;
                    throw;
                }
                finally
                {
                    Rnd.state = saved;
                }
                return [.. _placer.Instances.Values];
            }
        }
    }

    /// <summary>
    /// AltBiomeWorldData's points (GenerateBiomePoints) and the order its flood fill lists them in
    /// (GenerateSectors): each biome's points, and those at or above sea level.
    /// </summary>
    internal sealed class BiomeMap
    {
        /// <summary>
        /// Points per side, 12 m apart.
        /// </summary>
        public const int Size = 2048;

        /// <summary>
        /// Heightmap.BiomeIndex order.
        /// </summary>
        private static readonly Heightmap.Biome[] ByIndex =
        [
            Heightmap.Biome.None, Heightmap.Biome.Meadows, Heightmap.Biome.Swamp, Heightmap.Biome.Mountain, Heightmap.Biome.BlackForest,
            Heightmap.Biome.Plains, Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth, Heightmap.Biome.Ocean, Heightmap.Biome.Mistlands,
        ];

        /// <summary>
        /// The Ashlands', Deep North's and ocean's biome indices, and the bits of a point's byte (see _cell).
        /// </summary>
        private const byte Ash = 6, North = 7, Sea = 8, BiomeBits = 0x0F, Above = 0x10, Visited = 0x20;

        /// <summary>
        /// Per biome index: points as x | y &lt;&lt; 16, in the game's order (null until listed).
        /// </summary>
        public readonly int[][] All = new int[ByIndex.Length][];
        /// <summary>
        /// The same, only the points at or above sea level.
        /// </summary>
        public readonly int[][] AboveSea = new int[ByIndex.Length][];

        /// <summary>
        /// A map coordinate to world metres (the point's middle).
        /// </summary>
        public static float MapToWorld(float x) => (x - 1024f) * 12f + 6f;

        /// <summary>
        /// A single biome's index (Heightmap.BiomeIndex); -1 for combined flags.
        /// </summary>
        public static int Index(Heightmap.Biome b) => Array.IndexOf(ByIndex, b);

        /// <summary>
        /// One byte a point: the biome index, at or above sea level, met by the fill.
        /// </summary>
        private readonly byte[] _cell = new byte[Size * Size];

        /// <summary>
        /// Reads every point's biome and whether it is at or above sea level (ocean beyond the world's edge).
        /// </summary>
        /// <param name="gen">The seed's generator.</param>
        /// <param name="parallel">On every core but one, else on one.</param>
        /// <param name="cancel">Stops the reading (throws).</param>
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

        /// <summary>
        /// A biome's points listed the first time they are asked for. Its order depends on its own points
        /// only (the game's fill never crosses into another biome), so each is listed alone.
        /// </summary>
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
            All[biome] = [.. all];
            AboveSea[biome] = [.. above];
        }

        /// <summary>
        /// How the game turns a set of biomes into one to draw a point from: a single biome is itself;
        /// otherwise a random step along this walk, counting only the steps whose biome is in the set
        /// (and never reaching the last one: the draw stops one short). Two of the game's steps give
        /// another biome than the one they test (Plains gives Black Forest; the eighth step tests
        /// Meadows and gives the ocean); past the walk, Mistlands.
        /// </summary>
        private static readonly (Heightmap.Biome Test, Heightmap.Biome Gives)[] Walk =
        [
            (Heightmap.Biome.Meadows, Heightmap.Biome.Meadows),
            (Heightmap.Biome.Swamp, Heightmap.Biome.Swamp),
            (Heightmap.Biome.Mountain, Heightmap.Biome.Mountain),
            (Heightmap.Biome.BlackForest, Heightmap.Biome.BlackForest),
            (Heightmap.Biome.Plains, Heightmap.Biome.BlackForest),
            (Heightmap.Biome.AshLands, Heightmap.Biome.AshLands),
            (Heightmap.Biome.DeepNorth, Heightmap.Biome.DeepNorth),
            (Heightmap.Biome.Meadows, Heightmap.Biome.Ocean),
        ];

        /// <summary>
        /// The biomes counted for the draw's range (every one but None).
        /// </summary>
        private static readonly Heightmap.Biome[] Counted = [.. ByIndex.Skip(1)];

        /// <summary>
        /// The biome the game draws a point from for a set of biomes (see Walk); one draw unless the set is a
        /// single biome or none.
        /// </summary>
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

        /// <summary>
        /// GetRandomPointByBiomes and GetRandomPointByBiomesAboveSeaLevel, in world space.
        /// </summary>
        /// <param name="biomes">The kind's biomes.</param>
        /// <param name="aboveSea">Only points at or above sea level, when the biome has any.</param>
        public (float X, float Z) RandomPoint(Heightmap.Biome biomes, bool aboveSea)
        {
            int b = Index(RandomBiome(biomes));
            List(b);
            int[] list = aboveSea && AboveSea[b].Length > 0 ? AboveSea[b] : All[b];
            int p = list[Rnd.Range(0, list.Length)];
            return (MapToWorld(p & 0xFFFF), MapToWorld(p >> 16));
        }
    }

    /// <summary>
    /// Lays out kinds one after another as the game does when a world first loads (no zone made yet):
    /// for each kind, zones drawn at random (near the middle, or from the biome map), a few spots tried
    /// in each, the first spot that passes every check kept, until the kind has its number or its tries
    /// run out. The checks and the random draws come in the game's order: a draw more or less, and every
    /// later spot of the kind moves.
    /// </summary>
    /// <param name="gen">The seed's generator.</param>
    /// <param name="map">The biome map, built on first call.</param>
    private sealed class Placer(WorldGenerator gen, Func<BiomeMap> map)
    {
        /// <summary>
        /// The instances by zone (one a zone).
        /// </summary>
        public readonly Dictionary<(int X, int Y), Placed> Instances = [];
        /// <summary>
        /// The same by prefab, by group and by "max" group, for the distance checks.
        /// </summary>
        private readonly Neighbours _byPrefab = new(), _byGroup = new(), _byGroupMax = new();
        /// <summary>
        /// The vegetation sums of the kind's spots so far (the "stands out" check).
        /// </summary>
        private readonly List<float> _sums = [];

        /// <summary>
        /// Spots tried in each zone drawn.
        /// </summary>
        private const int SpotsAZone = 6;

        /// <summary>
        /// Lays out one kind, starting Unity's Random from the seed and the kind's prefab name.
        /// </summary>
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

        /// <summary>
        /// The zone a world point is in.
        /// </summary>
        private static (int X, int Y) ZoneOf((float X, float Z) p) =>
            ((int)MathF.Floor((float)((p.X + 32.0) / 64.0)), (int)MathF.Floor((float)((p.Z + 32.0) / 64.0)));

        /// <summary>
        /// A zone whose corner lies within reach of the middle (and inside the world).
        /// </summary>
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

        /// <summary>
        /// One spot of the zone (kept far enough from its edges for the kind's size), checked step by step;
        /// registered when it passes.
        /// </summary>
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

        /// <summary>
        /// From the middle of the world: the kind's ring.
        /// </summary>
        private static bool AtRightDistance(Rule rule, Vector3 spot)
        {
            float fromMiddle = spot.magnitude;
            return !(rule.minDistance != 0f && fromMiddle < rule.minDistance) && !(rule.maxDistance != 0f && fromMiddle > rule.maxDistance);
        }

        /// <summary>
        /// The biome, the height above the sea, the forest and the distance from the middle on the ground
        /// (spot.y set to the ground; vegetation: how green the ground is there).
        /// </summary>
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

        /// <summary>
        /// The ground's rise across the kind's size (the generator samples it at random points).
        /// </summary>
        private bool FlatEnough(Rule rule, Vector3 spot)
        {
            gen.GetTerrainDelta(spot, rule.exteriorRadius, out float rise, out _);
            return rise >= rule.minTerrainDelta && rise <= rule.maxTerrainDelta;
        }

        /// <summary>
        /// Far enough from its own prefab or group, and near enough to its own prefab or "max" group when it
        /// needs one.
        /// </summary>
        private bool RightNeighbours(Rule rule, Vector3 spot)
        {
            if (rule.minDistanceFromSimilar > 0f && (_byPrefab.Within(rule.prefab, spot, rule.minDistanceFromSimilar) || (rule.group.Length > 0 && _byGroup.Within(rule.group, spot, rule.minDistanceFromSimilar))))
            {
                return false;
            }
            return !(rule.maxDistanceFromSimilar > 0f && !_byPrefab.Within(rule.prefab, spot, rule.maxDistanceFromSimilar) && !(rule.groupMax.Length > 0 && _byGroupMax.Within(rule.groupMax, spot, rule.maxDistanceFromSimilar)));
        }

        /// <summary>
        /// The ground's vegetation within the kind's bounds (either end left out when not set).
        /// </summary>
        private static bool RightVegetation(Rule rule, float vegetation) =>
            !(rule.minimumVegetation > 0f && vegetation <= rule.minimumVegetation) && !(rule.maximumVegetation < 1f && vegetation >= rule.maximumVegetation);

        /// <summary>
        /// Kinds that want the greenest spots: the vegetation on rings around the spot (nearer counts
        /// more) must beat the average of the kind's spots so far by a share of the way to the best; the
        /// first nine spots only set the bar.
        /// </summary>
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

        /// <summary>
        /// Keeps an instance, unless its zone already has one.
        /// </summary>
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

    /// <summary>
    /// Instances by a name (prefab or group), for "is one within this distance" (3D, as the game measures).
    /// </summary>
    private sealed class Neighbours
    {
        /// <summary>
        /// The instances under each name.
        /// </summary>
        private readonly Dictionary<string, List<Placed>> _byName = [];

        /// <summary>
        /// Adds an instance under a name.
        /// </summary>
        public void Add(string name, Placed p)
        {
            if (!_byName.TryGetValue(name, out var list))
            {
                _byName[name] = list = [];
            }
            list.Add(p);
        }

        /// <summary>
        /// Whether an instance under the name is closer than distance (metres, 3D) to at.
        /// </summary>
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
