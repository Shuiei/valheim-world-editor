namespace ValheimGen;

public sealed partial class WorldGenerator
{
    /// <summary>
    /// Expand World Data's postfix on the game's GetBiomeHeight, applied when the biome file or the territory file is in
    /// use: the biome's paint as the mask (or the Ashlands' or Mistlands' own mask when the biome borrows another's
    /// ground), its lava thinned by its lava amount, the territory's ground colour, then the height as an altitude
    /// through the biome's and the territory's transforms. In the generator's coordinates.
    /// </summary>
    /// <param name="biome">The biome asked for.</param>
    /// <param name="shape">The biome whose ground shape was used (the biome's terrain).</param>
    /// <param name="x">The point's x.</param>
    /// <param name="z">The point's z.</param>
    /// <param name="h">The height, metres.</param>
    /// <param name="mask">The ground's mask, changed in place.</param>
    private float DataHeight(Heightmap.Biome biome, Heightmap.Biome shape, float x, float z, float h, ref Color mask)
    {
        var info = _v.Biomes.Of(biome);
        if (info?.Paint is { } paint)
        {
            mask = paint.ToColor();
        }
        else if (biome == Heightmap.Biome.AshLands && biome != shape)
        {
            GetAshlandsHeight(x, z, out mask);
        }
        else if (biome == Heightmap.Biome.Mistlands && biome != shape)
        {
            MistlandsShape(x, z, out mask);
        }
        bool hasData = info is { HasData: true };
        if (hasData && info!.LavaAmount != 1f)
        {
            h = ThinLava(info, x, z, h, ref mask);
        }
        var territory = TerritoryAt(x, z);
        if (territory?.TerrainColor is { } colour)
        {
            mask = colour.ToColor();
        }
        h = (float)((double)h - _v.WaterLevel);
        if (hasData)
        {
            h = info!.Height.Apply(h);
        }
        if (territory != null)
        {
            h = territory.Height.Apply(h);
        }
        return (float)((double)h + _v.WaterLevel);
    }

    /// <summary>
    /// A biome's lava thinned by its lava amount: a noise of its own seed (the seed's first draw, as the mod draws it
    /// again) over the amount is the lava kept, none above it; the ground sinks by what is kept.
    /// </summary>
    private float ThinLava(BiomeInfo info, float x, float z, float h, ref Color mask)
    {
        float stretch = (float)((double)0.01f / info.LavaStretch);
        float seed = _offsetA;
        float lava = GenMath.Noise((float)((double)seed + (double)x * stretch), (float)((double)seed + (double)z * stretch));
        mask.A = lava > info.LavaAmount ? 0f : (float)((double)lava / info.LavaAmount);
        return (float)((double)h - mask.A);
    }
}
