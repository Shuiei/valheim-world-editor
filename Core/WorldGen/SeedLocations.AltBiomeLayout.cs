using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public static partial class SeedLocations
{
    /// <summary>
    /// Which alt biomes (Dark Meadows, Rock Black Forest...) the game gives to which biome areas of a seed, as
    /// AltBiomeWorldData does each time the world loads (GenerateSectors, then GenerateAltBiomes): the biome point map
    /// cut into areas (sectors: the Ashlands, the Deep North and the ocean one each, every other biome one per
    /// connected area), each area measured along its edge, then each alt biome given to areas of its biomes in an
    /// order shuffled from the seed and its name. The result is the game's for a server started afresh (the game keeps
    /// what an alt biome was given while it runs: a second world loaded in the same session starts from those counts).
    /// With Expand World Data's world rules naming alt biomes, the areas whose middle passes such a rule get those alt
    /// biomes instead (counted after the others), and Expand World Size's stretch widens the alt biomes' distances.
    /// </summary>
    internal sealed partial class AltBiomeLayout
    {
        /// <summary>
        /// The layouts made so far, by generator and alt biome list (a list swapped makes a new one).
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<WorldGenerator, Holder> Made = [];

        /// <summary>
        /// The grid the areas lie on.
        /// </summary>
        private readonly BiomeGrid _grid;

        /// <summary>
        /// Per point (x + y * size): the index of its area's alt biomes in <see cref="_sets"/> (0: none).
        /// </summary>
        private readonly ushort[] _setOf;

        /// <summary>
        /// The different lists of alt biomes the areas were given, the empty one first.
        /// </summary>
        private readonly List<IReadOnlyList<AltBiomeRule>> _sets = [[]];

        /// <summary>
        /// How many areas each alt biome was given (by name; the game's log line of each).
        /// </summary>
        public IReadOnlyDictionary<string, int> Given { get; }

        /// <summary>
        /// Lays the alt biomes out on a seed's biome point map.
        /// </summary>
        /// <param name="map">The seed's biome point map.</param>
        /// <param name="gen">The seed's generator.</param>
        /// <param name="alts">The alt biomes, in the game's order.</param>
        /// <param name="cancel">Stops the layout (throws).</param>
        public AltBiomeLayout(BiomeMap map, WorldGenerator gen, IReadOnlyList<AltBiomeRule> alts, CancellationToken cancel = default)
        {
            _grid = map.Grid;
            var areas = new Areas(map, gen.Rules.Biomes, gen.Rules.DataInstalled, cancel);
            Rnd.RandomState saved = Rnd.State;
            try
            {
                Given = Place(areas, gen, alts);
            }
            finally
            {
                Rnd.State = saved;
            }
            _setOf = SetsOf(areas);
        }

        /// <summary>
        /// The alt biomes of the area holding a world point (the game's GetBiomeSector, clamped to the map).
        /// </summary>
        public IReadOnlyList<AltBiomeRule> At(float x, float z)
        {
            int mx = Math.Clamp(_grid.WorldToMap(x), 0, _grid.Size - 1), mz = Math.Clamp(_grid.WorldToMap(z), 0, _grid.Size - 1);
            return _sets[_setOf[mx + mz * _grid.Size]];
        }

        /// <summary>
        /// Whether a location kind may stand at a point: an alt biome's kind only in its areas, and no kind an alt biome
        /// of the area blocks (ZoneSystem's checks after the vegetation's).
        /// </summary>
        /// <param name="rule">The kind.</param>
        /// <param name="x">The spot's x.</param>
        /// <param name="z">The spot's z.</param>
        public bool LetsLocation(Rule rule, float x, float z)
        {
            var alts = At(x, z);
            return Has(alts, rule.AltBiome) && !alts.Any(a => a.BlockLocationNames.Contains(rule.Name));
        }

        /// <summary>
        /// Whether a vegetation rule may place a group at a point: an alt biome's rule only in its areas, and no rule an
        /// alt biome of the area blocks (PlaceVegetation's check of each try).
        /// </summary>
        /// <param name="parent">The alt biome that adds the rule; null for none.</param>
        /// <param name="name">The rule's name.</param>
        /// <param name="x">The group's x.</param>
        /// <param name="z">The group's z.</param>
        public bool LetsVegetation(string? parent, string name, float x, float z)
        {
            var alts = At(x, z);
            return Has(alts, parent) && !alts.Any(a => a.BlockVegetationNames.Contains(name));
        }

        /// <summary>
        /// Whether an area's alt biomes include a kind's or rule's parent (always when it has none).
        /// </summary>
        private static bool Has(IReadOnlyList<AltBiomeRule> alts, string? parent) => parent == null || alts.Any(a => a.Name == parent);

        /// <summary>
        /// Each point's list of alt biomes as an index into <see cref="_sets"/>.
        /// </summary>
        private ushort[] SetsOf(Areas areas)
        {
            ushort[] bySector = new ushort[areas.Sectors.Count];
            for (int s = 0; s < bySector.Length; s++)
            {
                var alts = areas.Sectors[s].Alts;
                if (alts.Count == 0)
                {
                    continue;
                }
                int found = _sets.FindIndex(set => set.SequenceEqual(alts));
                if (found < 0)
                {
                    found = _sets.Count;
                    _sets.Add([.. alts]);
                }
                bySector[s] = (ushort)found;
            }
            ushort[] setOf = new ushort[areas.SectorOf.Length];
            for (int k = 0; k < setOf.Length; k++)
            {
                setOf[k] = bySector[areas.SectorOf[k]];
            }
            return setOf;
        }

        /// <summary>
        /// The layout of a generator's world for these alt biomes: made once (on a biome point map of its own when the
        /// caller has none), kept while the generator lives.
        /// </summary>
        /// <param name="gen">The world's generator.</param>
        /// <param name="alts">The alt biomes.</param>
        /// <param name="map">Gives the seed's biome point map, when one has to be made.</param>
        /// <param name="cancel">Stops the layout (throws; nothing is kept then).</param>
        public static AltBiomeLayout Of(WorldGenerator gen, IReadOnlyList<AltBiomeRule> alts, Func<BiomeMap> map, CancellationToken cancel = default)
        {
            var holder = Made.GetValue(gen, _ => new Holder());
            lock (holder.Sync)
            {
                if (holder.Layout == null || !ReferenceEquals(holder.Alts, alts))
                {
                    holder.Layout = new AltBiomeLayout(map(), gen, alts, cancel);
                    holder.Alts = alts;
                }
                return holder.Layout;
            }
        }

        /// <summary>
        /// A generator's layout and the alt biome list it was made for.
        /// </summary>
        private sealed class Holder
        {
            /// <summary>
            /// Lets one caller at a time make the layout.
            /// </summary>
            public readonly Lock Sync = new();

            /// <summary>
            /// The layout, null until made.
            /// </summary>
            public AltBiomeLayout? Layout;

            /// <summary>
            /// The alt biome list it was made for.
            /// </summary>
            public IReadOnlyList<AltBiomeRule>? Alts;
        }
    }
}
