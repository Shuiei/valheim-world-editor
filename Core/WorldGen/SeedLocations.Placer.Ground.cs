
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
            // The game's literal 30, whatever the rules put the water at.
            float aboveSea = (float)(spot.Y - (double)WorldGenerator.LiteralWaterLevel);
            if (aboveSea < ScaledAltitude(rule.MinAltitude, biome) || aboveSea > ScaledAltitude(rule.MaxAltitude, biome))
            {
                return false;
            }
            if (rule.InForest != 0)
            {
                float forest = _gen.GetForestFactor(spot);
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
        /// A kind's altitude limit scaled by the biome's height transform (Expand World Data's "Scale location altitude
        /// requirement", with the biome file in use, for a biome the file gives settings to): the multiplier, the delta,
        /// then the depth multiplier under the water. The limit itself otherwise.
        /// </summary>
        private float ScaledAltitude(float limit, Heightmap.Biome biome)
        {
            var rules = _gen.Rules;
            if (!rules.ScaleLocationAltitude || !rules.BiomeData || rules.Biomes.Of(biome) is not { HasData: true } info)
            {
                return limit;
            }
            float h = (float)((double)limit * info.Height.Multiplier);
            h = (float)((double)h + info.Height.Delta);
            return h < 0f ? (float)((double)h * info.Height.DepthMultiplier) : h;
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
