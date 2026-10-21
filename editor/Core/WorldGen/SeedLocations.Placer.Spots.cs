using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public static partial class SeedLocations
{
    internal sealed partial class Placer
    {
        /// <summary>
        /// One spot of the zone (kept far enough from its edges for the kind's size), checked step by step;
        /// registered when it passes.
        /// </summary>
        internal bool TrySpot(Rule rule, Heightmap.Biome biomes, (int X, int Y) zone, float margin)
        {
            var spot = new Vector3(zone.X * 64f + Rnd.Range(-32f + margin, 32f - margin), 0f, zone.Y * 64f + Rnd.Range(-32f + margin, 32f - margin));
            if (margin >= 32f && _gen.Rules.DataInstalled)
            {
                // Expand World Data puts a kind wider than a zone at the zone's middle (the draws are made all the same).
                spot = new Vector3(zone.X * 64f, 0f, zone.Y * 64f);
            }
            if (!AtRightDistance(rule, spot) || !OnRightGround(rule, biomes, ref spot, out float vegetation) || !FlatEnough(rule, spot))
            {
                return false;
            }
            // The steps after the slope's (which draws from the random sequence) must stay after it.
            if (!RightNeighbours(rule, spot) || !RightVegetation(rule, vegetation) || !AltBiomesLet(rule, spot) || !StandsOut(rule, spot))
            {
                return false;
            }
            Register(rule, spot);
            return true;
        }

        /// <summary>
        /// Keeps an instance, unless its zone already has one.
        /// </summary>
        internal void Register(Rule rule, Vector3 spot)
        {
            var placed = new Placed(rule, spot.X, spot.Y, spot.Z);
            if (!Instances.TryAdd(ZoneOf((spot.X, spot.Z)), placed))
            {
                return;
            }
            _byPrefab.Add(rule.Prefab, placed);
            _byGroup.Add(rule.Group, placed);
            _byGroupMax.Add(rule.GroupMax, placed);
            _byBase.Add(rule.Base, placed);
            foreach (string group in rule.Groups ?? [])
            {
                _byGroups.Add(group, placed);
            }
        }
    }
}
