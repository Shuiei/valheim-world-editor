namespace ValheimGen;

public static partial class SeedLocations
{
    internal sealed partial class AltBiomeLayout
    {
        /// <summary>
        /// One biome area (the game's BiomeSector) and what the alt biomes are given by: its edge, measured.
        /// </summary>
        /// <param name="biome">Its biome.</param>
        internal sealed class Sector(Heightmap.Biome biome)
        {
            /// <summary>Its biome.</summary>
            public Heightmap.Biome Biome { get; } = biome;
            /// <summary>Its edge points (a point with a neighbour in another area).</summary>
            public int EdgeCount;
            /// <summary>The edge points' map coordinates added up (as floats, in the game's order), then its middle in world metres.</summary>
            public float CenterX, CenterY;
            /// <summary>The lowest and highest height along its edge.</summary>
            public float HeightMin = 99999f, HeightMax = -99999f;
            /// <summary>The middle of the two.</summary>
            public float HeightAvg;
            /// <summary>Its middle's distance from the world's middle, metres (0 for an area without an edge).</summary>
            public float DistanceFromCenter;
            /// <summary>The areas its edge touches (indices), each once, in the order met.</summary>
            public readonly List<int> Neighbors = new(4);
            /// <summary>The alt biomes given to it, in the order given.</summary>
            public readonly List<AltBiomeRule> Alts = [];
        }

        /// <summary>
        /// A seed's biome areas (GenerateSectors): every point's area, each area measured along its edge.
        /// </summary>
        internal sealed class Areas
        {
            /// <summary>The areas, in the order the game makes them.</summary>
            public List<Sector> Sectors { get; } = [];

            /// <summary>Per point (x + y * size): its area's index.</summary>
            public int[] SectorOf { get; }

            /// <summary>Each biome's areas (indices) in the order made: what the alt biomes shuffle.</summary>
            public Dictionary<Heightmap.Biome, List<int>> ByBiome { get; } = [];

            /// <summary>
            /// The biomes in the order the game goes through them (its Biomes dictionary, filled from the biome enum's
            /// values: by value, then Expand World Data's added biomes in the biome file's order when it is installed).
            /// </summary>
            public List<Heightmap.Biome> Order { get; }

            /// <summary>The map's grid.</summary>
            private readonly BiomeGrid _grid;

            /// <summary>
            /// Cuts the map into areas and measures them.
            /// </summary>
            /// <param name="map">The biome point map.</param>
            /// <param name="biomes">The world's biomes.</param>
            /// <param name="data">Whether Expand World Data is installed (its added biomes take part).</param>
            /// <param name="cancel">Stops the work (throws).</param>
            public Areas(BiomeMap map, BiomeTable biomes, bool data, CancellationToken cancel)
            {
                _grid = map.Grid;
                Order = [Heightmap.Biome.None, .. BiomeTable.GamesBiomes.Select(b => b.Value).Order()];
                if (data)
                {
                    Order.AddRange(biomes.Biomes.Select(b => b.Value).Where(b => !Order.Contains(b)));
                }
                SectorOf = new int[_grid.Size * _grid.Size];
                Label(map, cancel);
                Measure(map, cancel);
            }

            /// <summary>
            /// A new area of a biome, listed under it.
            /// </summary>
            private int Add(Heightmap.Biome biome)
            {
                Sectors.Add(new Sector(biome));
                if (!ByBiome.TryGetValue(biome, out var list))
                {
                    ByBiome[biome] = list = [];
                }
                list.Add(Sectors.Count - 1);
                return Sectors.Count - 1;
            }

            /// <summary>
            /// Every point's area: the Ashlands', the Deep North's and the ocean's points one area each (made first,
            /// joined or not), then each connected area of one biome, made at its first point in rows.
            /// </summary>
            private void Label(BiomeMap map, CancellationToken cancel)
            {
                int ash = Add(Heightmap.Biome.AshLands), north = Add(Heightmap.Biome.DeepNorth), sea = Add(Heightmap.Biome.Ocean);
                int ashIndex = map.IndexOf(Heightmap.Biome.AshLands), northIndex = map.IndexOf(Heightmap.Biome.DeepNorth), seaIndex = map.IndexOf(Heightmap.Biome.Ocean);
                for (int k = 0; k < SectorOf.Length; k++)
                {
                    int index = map.IndexAt(k);
                    SectorOf[k] = index == ashIndex ? ash : index == northIndex ? north : index == seaIndex ? sea : -1;
                }
                var stack = new Stack<int>(1024);
                for (int k = 0; k < SectorOf.Length; k++)
                {
                    if (k % _grid.Size == 0)
                    {
                        cancel.ThrowIfCancellationRequested();
                    }
                    if (SectorOf[k] < 0)
                    {
                        Fill(map, k, Add(map.BiomeAt(k)), stack);
                    }
                }
            }

            /// <summary>
            /// The connected points of one biome from a first one (above, below, left and right), given an area.
            /// </summary>
            private void Fill(BiomeMap map, int first, int sector, Stack<int> stack)
            {
                int size = _grid.Size, index = map.IndexAt(first);
                SectorOf[first] = sector;
                stack.Push(first);
                while (stack.Count > 0)
                {
                    int c = stack.Pop(), cx = c % size;
                    ReadOnlySpan<int> next = [cx + 1 < size ? c + 1 : -1, cx > 0 ? c - 1 : -1, c + size < SectorOf.Length ? c + size : -1, c - size];
                    foreach (int f in next)
                    {
                        if (f >= 0 && SectorOf[f] < 0 && map.IndexAt(f) == index)
                        {
                            SectorOf[f] = sector;
                            stack.Push(f);
                        }
                    }
                }
            }

            /// <summary>
            /// Each area's edge: its points (inside the map's border) with a neighbour in another area, met in rows,
            /// the neighbours tested left, right, below, above; the first other area met is a neighbour. Then each
            /// area's middle (the edge points' average, in world metres), its edge's average height and its distance
            /// from the world's middle. The heights are those of the edge points only, read again from the generator.
            /// </summary>
            private void Measure(BiomeMap map, CancellationToken cancel)
            {
                var edge = EdgePoints(cancel);
                int size = _grid.Size;
                float[] heights = new float[edge.Count];
                Parallel.For(0, edge.Count, new ParallelOptions { CancellationToken = cancel, MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1) },
                    i => heights[i] = map.HeightAt(edge[i].K % size, edge[i].K / size));
                for (int i = 0; i < edge.Count; i++)
                {
                    var (k, other) = edge[i];
                    var sector = Sectors[SectorOf[k]];
                    sector.EdgeCount++;
                    sector.CenterX += k % size;
                    sector.CenterY += k / size;
                    if (!sector.Neighbors.Contains(other))
                    {
                        sector.Neighbors.Add(other);
                    }
                    sector.HeightMin = Math.Min(sector.HeightMin, heights[i]);
                    sector.HeightMax = Math.Max(sector.HeightMax, heights[i]);
                }
                foreach (var sector in Sectors)
                {
                    Finish(sector);
                }
            }

            /// <summary>
            /// The edge points in rows (inside the map's border), each with the first other area met among its
            /// neighbours left, right, below and above.
            /// </summary>
            private List<(int K, int Other)> EdgePoints(CancellationToken cancel)
            {
                int size = _grid.Size;
                List<(int K, int Other)> edge = [];
                for (int y = 1; y < size - 1; y++)
                {
                    cancel.ThrowIfCancellationRequested();
                    for (int x = 1; x < size - 1; x++)
                    {
                        int k = x + y * size, other = OtherNeighbour(k, size);
                        if (other >= 0)
                        {
                            edge.Add((k, other));
                        }
                    }
                }
                return edge;
            }

            /// <summary>The first neighbour of a point (left, right, below, above) in another area; -1 for none.</summary>
            private int OtherNeighbour(int k, int size)
            {
                int s = SectorOf[k];
                ReadOnlySpan<int> next = [k - 1, k + 1, k - size, k + size];
                foreach (int n in next)
                {
                    if (SectorOf[n] != s)
                    {
                        return SectorOf[n];
                    }
                }
                return -1;
            }

            /// <summary>
            /// An area's middle in world metres, its edge's average height and its distance from the world's middle;
            /// an area without an edge keeps the middle (0, 0), as the game sets none.
            /// </summary>
            private void Finish(Sector sector)
            {
                if (sector.EdgeCount > 0)
                {
                    sector.CenterX = Middle(sector.CenterX / sector.EdgeCount);
                    sector.CenterY = Middle(sector.CenterY / sector.EdgeCount);
                    sector.HeightAvg = (float)(((double)sector.HeightMin + sector.HeightMax) / 2.0);
                }
                else
                {
                    (sector.CenterX, sector.CenterY) = (0f, 0f);
                }
                sector.DistanceFromCenter = (float)Math.Sqrt((double)sector.CenterX * sector.CenterX + (double)sector.CenterY * sector.CenterY);
            }

            /// <summary>
            /// A map coordinate in world metres as the game works out an area's middle (MapSpaceToWorldSpace of a
            /// fraction): in double, rounded once.
            /// </summary>
            private float Middle(float x) => (float)(((double)x - _grid.Middle) * _grid.Pixel + _grid.Half);
        }
    }
}
