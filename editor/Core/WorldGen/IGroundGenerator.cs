namespace ValheimGen;

/// <summary>
/// A world generation mod that replaces the game's ground (Riverheim): it answers in place of the bodies of the game's
/// GetBiome, GetBiomeHeight (and so GetHeight), GetAshlandsHeight and GetForestFactor, which its prefixes skip. The
/// points come as the game's callers give them after other mods' prefixes (Expand World Size's stretch); other mods'
/// postfixes still run on the answers (<see cref="WorldGenerator"/> keeps them). Safe to ask from many threads.
/// </summary>
internal interface IGroundGenerator
{
    /// <summary>The biome at a point, as one of the game's.</summary>
    /// <param name="x">World x (stretched, as the game's GetBiome is given it).</param>
    /// <param name="z">World z (stretched).</param>
    Heightmap.Biome Biome(float x, float z);

    /// <summary>The ground's height at a point, metres, with its mask (the game's ground paint channels).</summary>
    /// <param name="x">World x (stretched, as the game's GetBiomeHeight is given it).</param>
    /// <param name="z">World z (stretched).</param>
    /// <param name="mask">The ground's mask.</param>
    float Height(float x, float z, out Color mask);

    /// <summary>The forest factor at a point (lower: denser), as the game's GetForestFactor returns it.</summary>
    /// <param name="x">World x, as the game's caller gives it.</param>
    /// <param name="z">World z.</param>
    float Forest(float x, float z);
}
