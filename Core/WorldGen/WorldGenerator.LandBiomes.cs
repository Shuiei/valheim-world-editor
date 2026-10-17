
namespace ValheimGen;

public sealed partial class WorldGenerator
{
    /// <summary>
    /// The biome of a point on land (not sea, Ashlands, Deep North or Mountain): the first of Swamp, Mistlands, Plains and
    /// Black Forest whose noise field and distance from the middle fit, else Black Forest or Meadows by the distance.
    /// </summary>
    /// <param name="x">The point's x, in the generator's units.</param>
    /// <param name="z">The point's z.</param>
    /// <param name="fromMiddle">The point's distance from the middle of the world.</param>
    /// <param name="land">The point's continent height.</param>
    /// <param name="wave">The world's edge wave, in metres (added to the distance limits).</param>
    internal Heightmap.Biome LandBiome(float x, float z, float fromMiddle, float land, float wave)
    {
        if (Field(_offsetA, x, z) > 0.6f && fromMiddle > 2000f && fromMiddle < _swampMaxDistance && land > 0.05f && land < 0.25f)
        {
            return Heightmap.Biome.Swamp;
        }
        if (Field(_offsetMist, x, z) > _mistMinNoise && fromMiddle > (float)(6000.0 + wave) && fromMiddle < 10000f)
        {
            return Heightmap.Biome.Mistlands;
        }
        if (Field(_offsetB, x, z) > 0.4f && fromMiddle > (float)(3000.0 + wave) && fromMiddle < 8000f)
        {
            return Heightmap.Biome.Plains;
        }
        if (Field(_offsetForest, x, z) > 0.4f && fromMiddle > (float)(600.0 + wave) && fromMiddle < 6000f)
        {
            return Heightmap.Biome.BlackForest;
        }
        return fromMiddle > (float)(5000.0 + wave) ? Heightmap.Biome.BlackForest : Heightmap.Biome.Meadows;
    }
}
