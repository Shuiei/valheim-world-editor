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
        /// The alt biomes.
        /// </summary>
        private readonly IReadOnlyList<AltBiomeRule> _alts;
        /// <summary>
        /// Names of the kinds an alt biome blocks.
        /// </summary>
        private readonly HashSet<string> _blocked;

        /// <summary>
        /// A placer with no instances yet.
        /// </summary>
        /// <param name="gen">The seed's generator.</param>
        /// <param name="map">The biome map, built on first call.</param>
        /// <param name="alts">The alt biomes; none by default.</param>
        public Placer(WorldGenerator gen, Func<BiomeMap> map, IReadOnlyList<AltBiomeRule>? alts = null)
        {
            _gen = gen;
            _map = map;
            _alts = alts ?? [];
            _blocked = [.. _alts.SelectMany(a => a.BlockLocationNames)];
        }

        /// <summary>
        /// Whether the alt biomes let a kind stand at a spot (its own alt biome's areas only, none of the area's alt
        /// biomes blocking it); the alt biomes are laid out the first time a kind that depends on them asks.
        /// </summary>
        private bool AltBiomesLet(LocationRule rule, Vector3 spot) =>
            (rule.AltBiome == null && !_blocked.Contains(rule.Name))
            || (_alts.Count > 0 && AltBiomeLayout.Of(_gen, _alts, _map).LetsLocation(rule, spot.X, spot.Z));

        /// <summary>
        /// The instances by zone (one a zone).
        /// </summary>
        public readonly Dictionary<(int X, int Y), Placed> Instances = [];
        /// <summary>
        /// The same by prefab, by group and by "max" group, for the distance checks.
        /// </summary>
        private readonly Neighbours _byPrefab = new(), _byGroup = new(), _byGroupMax = new();
        /// <summary>
        /// The same by the game's prefab (clones of one prefab together) and by every group a kind belongs to, for
        /// Expand World Data's distance checks (<see cref="ModSpacing"/>).
        /// </summary>
        private readonly Neighbours _byBase = new(), _byGroups = new();

        /// <summary>
        /// Whether the world's rules are Expand World Data's location file: its distance checks replace the game's
        /// (clones count as their prefab, groups by membership, its "awayFrom" and "closeTo").
        /// </summary>
        private bool ModSpacing => _gen.Rules.Locations != null;

        /// <summary>The start temple's prefab, which the mod places at the middle when the layout placed none.</summary>
        internal const string StartPrefab = "StartTemple";
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
        public void Generate(LocationRule rule)
        {
            Rnd.InitState(_gen.GetSeed() + StableHash.Of(rule.Prefab)); // nosemgrep
            int have = Instances.Values.Count(p => p.Rule.Prefab == rule.Prefab);
            (_sums, _sumsTotal, _sumsBest) = (0, 0, float.NegativeInfinity);
            if (rule.Unique != 0 && have > 0)
            {
                return;
            }
            var biomes = (Heightmap.Biome)rule.Biome;
            float margin = MathF.Max(rule.ExteriorRadius, rule.InteriorRadius);
            // Kinds laid out from the middle out (the start temple): the reach grows a metre a try.
            float reach = rule.MinDistance;
            for (int left = rule.Prioritized != 0 ? 60000 : 12000; left > 0 && have < rule.Quantity; left--)
            {
                (int X, int Y) zone;
                if (rule.CenterFirst != 0)
                {
                    zone = ZoneNearMiddle(reach++);
                }
                else if (_map().RandomPoint(biomes, rule.MinAltitude >= 0f) is { } point)
                {
                    zone = ZoneOf(point);
                }
                else
                {
                    // A biome with no point on the map (or none at all): this try finds no zone.
                    continue;
                }
                if (Instances.ContainsKey(zone) || ((Heightmap.BiomeArea)rule.BiomeArea & _gen.GetBiomeArea(new Vector2s(zone.X * 64, zone.Y * 64))) == 0)
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
            if (have == 0 && rule.Prefab == StartPrefab && _gen.Rules.DataInstalled)
            {
                // Expand World Data puts the start temple in the middle zone when the layout placed none (its
                // GuaranteeLocations, once the world has loaded): at the zone's middle, give or take the few metres its
                // unseeded draw moves it by.
                Register(rule, new Vector3(0f, _gen.GetHeight(0f, 0f), 0f));
            }
        }
    }
}
