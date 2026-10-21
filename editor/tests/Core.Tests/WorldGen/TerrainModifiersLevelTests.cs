using System.Numerics;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Levelling a zone's heights to a modifier's height: a round or square area around the point, and a player-made one
/// held to the ground within 8 m.
/// </summary>
public class TerrainModifiersLevelTests
{
    private const int N = 65;

    /// <summary>A zone's heights, all zero.</summary>
    private static float[] Zero() => new float[N * N];

    /// <summary>The number of vertices the levelling set.</summary>
    private static int Set(float[] h) => h.Count(v => v != 0f);

    [Fact]
    public void ARadiusOfNothingLevelsOnlyThePointsVertex()
    {
        float[] h = Zero();

        TerrainModifiers.LevelTerrain(0, 0, h, new Vector3(0, 5, 0), 0f, false, null, null, false);

        Assert.Equal(5f, h[32 * N + 32]);
        Assert.Equal(1, Set(h));
    }

    [Fact]
    public void ASquareAreaLevelsEveryVertexInTheSquare()
    {
        float[] h = Zero();

        TerrainModifiers.LevelTerrain(0, 0, h, new Vector3(0, 5, 0), 1f, true, null, null, false);

        Assert.Equal(9, Set(h));
    }

    [Fact]
    public void ARoundAreaLeavesTheCornersOut()
    {
        float[] h = Zero();

        TerrainModifiers.LevelTerrain(0, 0, h, new Vector3(0, 5, 0), 1f, false, null, null, false);

        Assert.Equal(5, Set(h));
        Assert.Equal(0f, h[31 * N + 31]);
    }

    [Fact]
    public void APlayerMadeLevelIsHeldWithin8MetresOfTheGround()
    {
        float[] h = Zero();
        float[] levelOnly = Zero();
        float[] ground = Zero();

        TerrainModifiers.LevelTerrain(0, 0, h, new Vector3(0, 20, 0), 0f, false, ground, levelOnly, true);

        Assert.Equal(8f, h[32 * N + 32]);
        Assert.Equal(8f, levelOnly[32 * N + 32]);
    }
}
