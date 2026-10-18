using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Whether a point is forest: the game counts a point as forest when its forest factor is under 1.15, so the answer
/// follows the factor exactly; and the rules' forest multiplier divides the factor (more forest with a higher value).
/// </summary>
public class WorldGeneratorForestTests
{
    private static WorldGenerator Generator(WorldRules? rules = null) =>
        WorldGenerator.Create(new World { Seed = 12345, WorldGenVersion = 2, Rules = rules ?? WorldRules.Vanilla });

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(1000f, -2000f)]
    [InlineData(-5000f, 3000f)]
    [InlineData(7000f, 7000f)]
    public void APointIsForestExactlyWhenItsForestFactorIsUnder115(float x, float z)
    {
        var g = Generator();
        var pos = new Vector3(x, 0f, z);
        Assert.Equal(g.GetForestFactor(pos) < 1.15f, g.InForest(pos));
    }

    [Fact]
    public void TheForestMultiplierDividesTheFactorAndZeroMeansOne()
    {
        var pos = new Vector3(1000f, 0f, -2000f);
        float plain = Generator().GetForestFactor(pos);
        Assert.Equal(plain / 2f, Generator(new WorldRules { ForestMultiplier = 2f }).GetForestFactor(pos));
        // The mod takes a multiplier of 0 as 1.
        Assert.Equal(plain, Generator(new WorldRules { ForestMultiplier = 0f }).GetForestFactor(pos));
    }
}
