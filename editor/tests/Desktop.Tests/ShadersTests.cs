using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The plain look's ground colours: each biome's (close to the game's map), made linear; grey for one not known.
/// </summary>
public class ShadersTests
{
    [Theory]
    [InlineData(1, 0.42f, 0.60f, 0.27f)]
    [InlineData(2, 0.38f, 0.34f, 0.24f)]
    [InlineData(4, 0.86f, 0.89f, 0.93f)]
    [InlineData(8, 0.24f, 0.34f, 0.20f)]
    [InlineData(16, 0.74f, 0.69f, 0.38f)]
    [InlineData(32, 0.42f, 0.20f, 0.14f)]
    [InlineData(64, 0.90f, 0.93f, 0.97f)]
    [InlineData(256, 0.70f, 0.64f, 0.48f)]
    [InlineData(512, 0.34f, 0.32f, 0.40f)]
    [InlineData(0, 0.5f, 0.5f, 0.5f)]
    [InlineData(1024, 0.5f, 0.5f, 0.5f)]
    public void EachBiomeHasItsGroundColourMadeLinear(int biome, float r, float g, float b)
    {
        var c = Shaders.BiomeColor(biome);
        Assert.Equal(new Vector3(MathF.Pow(r, 2.2f), MathF.Pow(g, 2.2f), MathF.Pow(b, 2.2f)), c);
    }
}
