using System.Numerics;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Blending a zone's heights towards a modifier's height: the point takes the height, its neighbours beyond the reach stay,
/// and a player-made blend is held to the levelled ground within 1 m.
/// </summary>
public class TerrainModifiersSmoothTests
{
    private const int N = 65;

    /// <summary>A zone's heights, all zero.</summary>
    private static float[] Zero() => new float[N * N];

    [Fact]
    public void ANoReachChangesNothing()
    {
        float[] h = Zero();

        TerrainModifiers.SmoothTerrain2(0, 0, h, new Vector3(0, 10, 0), 0f, null, 1f, false);

        Assert.Equal(0, h.Count(v => v != 0f));
    }

    [Fact]
    public void ThePointTakesTheModifiersHeightAndItsNeighboursStay()
    {
        float[] h = Zero();

        TerrainModifiers.SmoothTerrain2(0, 0, h, new Vector3(0, 10, 0), 1f, null, 1f, false);

        Assert.Equal(10f, h[32 * N + 32]);
        Assert.Equal(0f, h[32 * N + 33]);
    }

    [Fact]
    public void APlayerMadeBlendIsHeldWithin1MetreOfTheLevelledGround()
    {
        float[] h = Zero();
        float[] levelOnly = Zero();

        TerrainModifiers.SmoothTerrain2(0, 0, h, new Vector3(0, 10, 0), 1f, levelOnly, 1f, true);

        Assert.Equal(1f, h[32 * N + 32]);
    }
}
