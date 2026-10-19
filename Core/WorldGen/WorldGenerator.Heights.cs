using Rnd = ValheimGen.UnityEngine.Random;

namespace ValheimGen;

public sealed partial class WorldGenerator
{
    /// <summary>
    /// The height the game uses while it lays out its streams (before the Ashlands' and Deep North's
    /// own shapes; riverPreGen: the Deep North as it is for rivers).
    /// </summary>
    public float GetPregenerationHeight(float x, float z, bool riverPreGen) =>
        GetBiomeHeight(GetBiome(x, z), x, z, out _, preGeneration: true, riverPreGen);

    /// <summary>
    /// The ground's height (metres) in a given biome, and its mask (the ground's colour channels, as
    /// the game's heightmap keeps them; black for biomes without one).
    /// </summary>
    /// <param name="biome">The biome whose shape to use (zone edges blend the shapes of the corners' biomes).</param>
    /// <param name="x">World x.</param>
    /// <param name="z">World z.</param>
    /// <param name="mask">The ground's mask.</param>
    /// <param name="preGeneration">The rough shapes the game uses while laying out water: no edge gaps, no
    /// Mistlands, Ashlands or Deep North detail.</param>
    /// <param name="riverPreDN">With <paramref name="preGeneration"/>: the Deep North as it is for rivers.</param>
    internal float GetBiomeHeight(Heightmap.Biome biome, float x, float z, out Color mask, bool preGeneration = false, bool riverPreDN = true)
    {
        mask = Color.Black;
        if (_world.Menu)
        {
            float shape = biome == Heightmap.Biome.Mountain ? MountainShape(x, z, menu: true) : MenuShape(x, z);
            return Scaled(shape, preGeneration ? HeightScale : (float)(HeightScale * AshlandsGap(x, z) * CreateDeepNorthGap(x, z)));
        }
        // Expand World Size: the world stretched (its prefix on the game's GetBiomeHeight divides the point).
        x = Stretched(x);
        z = Stretched(z);
        // Expand World Data: a biome's ground is its terrain biome's (its prefix, with the biome or territory file).
        Heightmap.Biome ground = _v.AnyBiomeData ? _v.Biomes.Terrain(biome) : biome;
        // The ground flattens towards the sea along the Ashlands' and Deep North's edges.
        float scale = preGeneration ? HeightScale : (float)(HeightScale * AshlandsGap(x, z) * CreateDeepNorthGap(x, z));
        float h;
        if (GenMath.Length(x, z) > _v.EdgeOfWorld)
        {
            h = -2f * HeightScale;
        }
        else
        {
            h = ground switch
            {
                Heightmap.Biome.Meadows => Scaled(MeadowsShape(x, z), scale),
                Heightmap.Biome.BlackForest => Scaled(ForestShape(x, z), scale),
                Heightmap.Biome.Swamp => Scaled(SwampShape(x, z), scale),
                Heightmap.Biome.Mountain => Scaled(MountainShape(x, z, menu: false), scale),
                Heightmap.Biome.Plains => Scaled(PlainsShape(x, z), scale),
                Heightmap.Biome.Mistlands => Scaled(preGeneration ? ForestShape(x, z) : MistlandsShape(x, z, out mask), scale),
                Heightmap.Biome.AshLands => Scaled(preGeneration ? AshlandsRoughShape(x, z) : GetAshlandsHeight(x, z, out mask), scale),
                Heightmap.Biome.DeepNorth => Scaled(preGeneration ? DeepNorthRoughShape(x, z, riverPreDN) : DeepNorthShape(x, z, out mask), scale),
                Heightmap.Biome.Ocean => Scaled(Continent(x, z), scale),
                _ => 0f,
            };
        }
        h = _v.Depth(h);
        // Expand World Data's postfix (taken to run after Expand World Size's depth, patched earlier).
        return _v.AnyBiomeData ? DataHeight(biome, ground, x, z, h, ref mask) : h;
    }

    // ---- The ground around a point.
    /// <summary>
    /// The rise across a circle (the highest minus the lowest of ten random points in it) and the way
    /// down, as the game samples it (from Unity's random sequence).
    /// </summary>
    /// <param name="center">The circle's middle.</param>
    /// <param name="radius">Its radius, metres.</param>
    /// <param name="delta">The highest point's height minus the lowest's.</param>
    /// <param name="slopeDirection">From the highest point to the lowest, unit length (zero when they are the
    /// same).</param>
    internal void GetTerrainDelta(Vector3 center, float radius, out float delta, out Vector3 slopeDirection)
    {
        float highest = -999999f, lowest = 999999f;
        Vector3 high = center, low = center;
        for (int i = 0; i < 10; i++)
        {
            var offset = Rnd.InsideUnitCircle * radius;
            var p = center + new Vector3(offset.X, 0f, offset.Y);
            float h = GetHeight(p.X, p.Z);
            if (h < lowest)
            {
                lowest = h;
                low = p;
            }
            if (h > highest)
            {
                highest = h;
                high = p;
            }
        }
        delta = (float)((double)highest - lowest);
        slopeDirection = Vector3.Normalize(low - high);
    }
}
