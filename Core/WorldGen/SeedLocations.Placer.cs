using TerrainEditor.Save;
using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public static partial class SeedLocations
{
    /// <summary>
    /// Lays out kinds one after another as the game does when a world first loads (no zone made yet):
    /// for each kind, zones drawn at random (near the middle, or from the biome map), a few spots tried
    /// in each, the first spot that passes every check kept, until the kind has its number or its tries
    /// run out. The checks and the random draws come in the game's order: a draw more or less, and every
    /// later spot of the kind moves.
    /// </summary>
    internal sealed partial class Placer
    {
        /// <summary>
        /// The seed's generator.
        /// </summary>
        private readonly WorldGenerator _gen;
        /// <summary>
        /// The biome map, built on first call.
        /// </summary>
        private readonly Func<BiomeMap> _map;

        /// <summary>
        /// A placer with no instances yet.
        /// </summary>
        /// <param name="gen">The seed's generator.</param>
        /// <param name="map">The biome map, built on first call.</param>
        public Placer(WorldGenerator gen, Func<BiomeMap> map)
        {
            _gen = gen;
            _map = map;
        }

        /// <summary>
        /// The instances by zone (one a zone).
        /// </summary>
        public readonly Dictionary<(int X, int Y), Placed> Instances = [];
        /// <summary>
        /// The same by prefab, by group and by "max" group, for the distance checks.
        /// </summary>
        private readonly Neighbours _byPrefab = new(), _byGroup = new(), _byGroupMax = new();
        /// <summary>
        /// How many of the kind's spots so far had their vegetation summed (the "stands out" check), the total of
        /// those sums (in double, as an average of them is taken) and the best of them.
        /// </summary>
        private int _sums;
        /// <summary>See <see cref="_sums"/>.</summary>
        private double _sumsTotal;
        /// <summary>See <see cref="_sums"/>.</summary>
        private float _sumsBest;

        /// <summary>
        /// Spots tried in each zone drawn.
        /// </summary>
        private const int SpotsAZone = 6;

        /// <summary>
        /// Lays out one kind, starting Unity's Random from the seed and the kind's prefab name.
        /// </summary>
        public void Generate(Rule rule)
        {
            Rnd.InitState(_gen.GetSeed() + StableHash.Of(rule.prefab));
            int have = Instances.Values.Count(p => p.Rule.prefab == rule.prefab);
            (_sums, _sumsTotal, _sumsBest) = (0, 0, float.NegativeInfinity);
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
                (int X, int Y) zone;
                if (rule.centerFirst != 0)
                {
                    zone = ZoneNearMiddle(reach++);
                }
                else if (_map().RandomPoint(biomes, rule.minAltitude >= 0f) is { } point)
                {
                    zone = ZoneOf(point);
                }
                else
                {
                    // A biome with no point on the map (or none at all): this try finds no zone.
                    continue;
                }
                if (Instances.ContainsKey(zone) || ((Heightmap.BiomeArea)rule.biomeArea & _gen.GetBiomeArea(new Vector2s(zone.X * 64, zone.Y * 64))) == 0)
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
    }
}
