
namespace ValheimGen;

public static partial class SeedLocations
{
    private sealed partial class Placer
    {
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
            var biome = _gen.GetBiome(spot);
            if ((biomes & biome) == 0)
            {
                return false;
            }
            spot.y = _gen.GetBiomeHeight(biome, spot.x, spot.z, out Color mask);
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
            _gen.GetTerrainDelta(spot, rule.exteriorRadius, out float rise, out _);
            return rise >= rule.minTerrainDelta && rise <= rule.maxTerrainDelta;
        }
    }
}
