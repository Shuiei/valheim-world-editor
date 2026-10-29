
namespace ValheimGen;

public sealed partial class WorldGenerator
{
    /// <summary>
    /// The biome of a point on land (not sea, Ashlands, Deep North or Mountain): the first of Swamp, Mistlands, Plains and
    /// Black Forest whose noise field and distance from the middle fit, else Black Forest or Meadows by the distance.
    /// The distance bands are the rules' (fractions of the stretched radius; the game's 2000, 6000, 10000, 3000, 8000,
    /// 600, 6000 and 5000 m).
    /// </summary>
    /// <param name="x">The point's x, in the generator's units.</param>
    /// <param name="z">The point's z.</param>
    /// <param name="fromMiddle">The point's distance from the middle of the world.</param>
    /// <param name="land">The point's continent height.</param>
    /// <param name="wave">The world's edge wave, in metres (added to the distance limits).</param>
    internal Heightmap.Biome LandBiome(float x, float z, float fromMiddle, float land, float wave)
    {
        if (Field(_offsetA, x, z) > 0.6f && fromMiddle > _v.SwampMin && fromMiddle < _swampMaxDistance && land > 0.05f && land < 0.25f)
        {
            return Heightmap.Biome.Swamp;
        }
        if (Field(_offsetMist, x, z) > _mistMinNoise && fromMiddle > (float)(_v.MistMin + wave) && fromMiddle < _v.MistMax)
        {
            return Heightmap.Biome.Mistlands;
        }
        if (Field(_offsetB, x, z) > 0.4f && fromMiddle > (float)(_v.PlainsMin + wave) && fromMiddle < _v.PlainsMax)
        {
            return Heightmap.Biome.Plains;
        }
        if (Field(_offsetForest, x, z) > 0.4f && fromMiddle > (float)(_v.ForestMin + wave) && fromMiddle < _v.ForestMax)
        {
            return Heightmap.Biome.BlackForest;
        }
        return fromMiddle > (float)(_v.MeadowsEdge + wave) ? Heightmap.Biome.BlackForest : Heightmap.Biome.Meadows;
    }
}
