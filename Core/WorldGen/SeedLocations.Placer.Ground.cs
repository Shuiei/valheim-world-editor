
namespace ValheimGen;

public static partial class SeedLocations
{
    internal sealed partial class Placer
    {
        /// <summary>
        /// From the middle of the world: the kind's ring.
        /// </summary>
        internal static bool AtRightDistance(Rule rule, Vector3 spot)
        {
            float fromMiddle = spot.Magnitude;
            return !(rule.MinDistance != 0f && fromMiddle < rule.MinDistance) && !(rule.MaxDistance != 0f && fromMiddle > rule.MaxDistance);
        }

        /// <summary>
        /// The biome, the height above the sea, the forest and the distance from the middle on the ground
        /// (spot.y set to the ground; vegetation: how green the ground is there).
        /// </summary>
        internal bool OnRightGround(Rule rule, Heightmap.Biome biomes, ref Vector3 spot, out float vegetation)
        {
            vegetation = 0;
            var biome = _gen.GetBiome(spot);
            if ((biomes & biome) == 0)
            {
                return false;
            }
            spot.Y = _gen.GetBiomeHeight(biome, spot.X, spot.Z, out Color mask);
            vegetation = mask.A;
            float aboveSea = (float)(spot.Y - 30.0);
            if (aboveSea < rule.MinAltitude || aboveSea > rule.MaxAltitude)
            {
                return false;
            }
            if (rule.InForest != 0)
            {
                float forest = WorldGenerator.GetForestFactor(spot);
                if (forest < rule.ForestTresholdMin || forest > rule.ForestTresholdMax)
                {
                    return false;
                }
            }
            if (rule.MinDistanceFromCenter <= 0f && rule.MaxDistanceFromCenter <= 0f)
            {
                return true;
            }
            float flat = MathF.Sqrt(spot.X * spot.X + spot.Z * spot.Z);
            return !(rule.MinDistanceFromCenter > 0f && flat < rule.MinDistanceFromCenter) && !(rule.MaxDistanceFromCenter > 0f && flat > rule.MaxDistanceFromCenter);
        }

        /// <summary>
        /// The ground's rise across the kind's size (the generator samples it at random points).
        /// </summary>
        internal bool FlatEnough(Rule rule, Vector3 spot)
        {
            _gen.GetTerrainDelta(spot, rule.ExteriorRadius, out float rise, out _);
            return rise >= rule.MinTerrainDelta && rise <= rule.MaxTerrainDelta;
        }
    }
}
