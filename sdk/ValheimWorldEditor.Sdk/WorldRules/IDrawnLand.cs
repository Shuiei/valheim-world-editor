namespace ValheimGen;

/// <summary>
/// A world's land drawn by the player (the editor's master map): it answers in place of the game's continents
/// (GetBaseHeight) and biome layout (GetBiome) only, so each biome's own shape, the rivers and lakes the game lays out on
/// that land, the forests, the vegetation and the locations all stay the game's (<see cref="GeneratorRules.Land"/>).
/// </summary>
internal interface IDrawnLand
{
    /// <summary>The continents' height at a point, in the generator's units (the game's GetBaseHeight, inside the world).</summary>
    /// <param name="x">World x (in the generator's coordinates).</param>
    /// <param name="z">World z.</param>
    float Continent(float x, float z);

    /// <summary>The biome at a point.</summary>
    /// <param name="x">World x (in the generator's coordinates).</param>
    /// <param name="z">World z.</param>
    Heightmap.Biome Biome(float x, float z);
}
