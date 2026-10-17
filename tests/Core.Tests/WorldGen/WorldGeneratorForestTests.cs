using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Whether a point is forest: the game counts a point as forest when its forest factor is under 1.15, so the answer
/// follows the factor exactly.
/// </summary>
public class WorldGeneratorForestTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1000f, -2000f)]
    [InlineData(-5000f, 3000f)]
    [InlineData(7000f, 7000f)]
    public void APointIsForestExactlyWhenItsForestFactorIsUnder115(float x, float z)
    {
        var pos = new Vector3(x, 0f, z);
        Assert.Equal(WorldGenerator.GetForestFactor(pos) < 1.15f, WorldGenerator.InForest(pos));
    }
}
