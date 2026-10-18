using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public static partial class SeedLocations
{
    /// <summary>
    /// AltBiomeWorldData's points (GenerateBiomePoints) and the order its flood fill lists them in
    /// (GenerateSectors): each biome's points, and those at or above sea level.
    /// </summary>
    internal sealed partial class BiomeMap
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
        /// World metres to the map coordinate of the 12 m cell holding them (WorldSpaceToMapSpace, cut toward zero as
        /// the game casts it); <see cref="MapToWorld"/> of it snaps a point to its cell's middle.
        /// </summary>
        public static int WorldToMap(float v) => (int)((v - 6f) / 12f + 1024f);

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
                    float edge = gen.Rules.TotalRadius;
                    if (wx * wx + wy * wy > edge * edge)
                    {
                        _cell[k] = Sea;
                        continue;
                    }
                    Heightmap.Biome b = gen.GetBiome(wx, wy);
                    // "Above the sea" is the game's literal 30, whatever the rules put the water at.
                    _cell[k] = (byte)(Index(b) | (gen.GetBiomeHeight(b, wx, wy, out _) >= WorldGenerator.LiteralWaterLevel ? Above : 0));
                }
            });
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
        /// <returns>The point; null when the biome drawn is none the map knows (no biome, an unknown flag) or the map
        /// has no point of it.</returns>
        public (float X, float Z)? RandomPoint(Heightmap.Biome biomes, bool aboveSea)
        {
            int b = Index(RandomBiome(biomes));
            if (b < 0)
            {
                return null;
            }
            List(b);
            int[] list = aboveSea && AboveSea[b].Length > 0 ? AboveSea[b] : All[b];
            if (list.Length == 0)
            {
                return null;
            }
            int p = list[Rnd.Range(0, list.Length)];
            return (MapToWorld(p & 0xFFFF), MapToWorld(p >> 16));
        }
    }
}
