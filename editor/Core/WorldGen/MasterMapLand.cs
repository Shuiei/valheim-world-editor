using DrawnWorlds;

namespace ValheimGen;

/// <summary>
/// The land a master map draws, as the world generator asks it in place of the game's continents (GetBaseHeight) and
/// biome layout (GetBiome): everything else (each biome's own shape, the rivers and lakes the game lays out on that
/// land, the forests, the vegetation and the locations) stays the game's. The maths is <see cref="DrawnLand"/>'s, the
/// very file the game's mod compiles, so the editor and the game make the same land.
/// </summary>
/// <param name="cells">The master map's kinds, row by row from the north.</param>
/// <param name="heights">The heights of a heightmap at each cell (DrawnLand.HeightOf), or null.</param>
/// <param name="biomes">The game's biome of each kind (a river's and a lake's are never asked).</param>
internal sealed class MasterMapLand(byte[] cells, ushort[]? heights, Heightmap.Biome[] biomes) : IDrawnLand
{
    /// <summary>The land.</summary>
    private readonly DrawnLand _land = new(cells, heights, GenMath.Noise);

    /// <inheritdoc/>
    public float Continent(float x, float z) => _land.Continent(x, z);

    /// <inheritdoc/>
    public Heightmap.Biome Biome(float x, float z) => biomes[_land.BiomeKind(x, z)];
}
