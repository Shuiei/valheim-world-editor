using ValheimGen;
using Xunit;
using TerrainService = ValheimGen.TerrainService;

namespace WorldEditor.Tests;

/// <summary>
/// A zone corner's biome area: median when the corner and the corners 64 m round it are all in one biome, edge when
/// one of them is in another biome.
/// </summary>
[Collection("World files")]
public class WorldGeneratorBiomeAreaTests
{
    /// <summary>The corners 64 m from a corner, that its area is judged by.</summary>
    private static readonly (int X, int Z)[] Round = [(-64, -64), (64, -64), (64, 64), (-64, 64), (-64, 0), (64, 0), (0, -64), (0, 64)];

    /// <summary>Corners on a square grid of the given step, out to the given reach from the origin.</summary>
    private static IEnumerable<Vector2s> Grid(int reach, int step) =>
        from x in Enumerable.Range(-reach / step, 2 * reach / step + 1)
        from z in Enumerable.Range(-reach / step, 2 * reach / step + 1)
        select new Vector2s(x * step, z * step);

    /// <summary>The biome at a corner, as the generator asks it.</summary>
    private static Heightmap.Biome BiomeOf(WorldGenerator g, int x, int z) => g.GetBiome(x, z);

    [Fact]
    public void ACornerAmongTheSameBiomeAllAroundIsMedian()
    {
        using var w = new TempWorld();
        WorldGenerator g = new TerrainService(w.Load()).Generator;
        Vector2s? median = Grid(6400, 128).Cast<Vector2s?>().FirstOrDefault(c =>
            Round.All(o => BiomeOf(g, c!.Value.x + o.X, c.Value.y + o.Z) == BiomeOf(g, c.Value.x, c.Value.y)));
        Assert.NotNull(median);
        Assert.Equal(Heightmap.BiomeArea.Median, g.GetBiomeArea(median!.Value));
    }

    [Fact]
    public void ACornerNextToAnotherBiomeIsEdge()
    {
        using var w = new TempWorld();
        WorldGenerator g = new TerrainService(w.Load()).Generator;
        Vector2s? edge = Grid(6400, 128).Cast<Vector2s?>().FirstOrDefault(c =>
            Round.Any(o => BiomeOf(g, c!.Value.x + o.X, c.Value.y + o.Z) != BiomeOf(g, c.Value.x, c.Value.y)));
        Assert.NotNull(edge);
        Assert.Equal(Heightmap.BiomeArea.Edge, g.GetBiomeArea(edge!.Value));
    }
}
