using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public static partial class SeedLocations
{
    private sealed partial class Placer
    {
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
}
