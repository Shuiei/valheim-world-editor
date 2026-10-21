using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The seeded random steps of the look's noise: the values for a known state, the state moving on, and every value in [0, 1).
/// </summary>
public class SeededRandomTests
{
    [Fact]
    public void AStateGivesTheMulberry32ValuesOfItsSequence()
    {
        // The expected values come from the mulberry32 definition, computed outside this code: the state is added to
        // 0x6D2B79F5, then t is mixed with imul(t ^ (t >>> 15), t | 1), then t ^= t + imul(t ^ (t >>> 7), t | 61), and the
        // value is (t ^ (t >>> 14)) / 2^32.
        uint state = 0;
        Assert.Equal(0.26642920868471265, SeededRandom.NextUnit(ref state), 12);
        Assert.Equal(0.0003297457005828619, SeededRandom.NextUnit(ref state), 12);
        Assert.Equal(0.2232720274478197, SeededRandom.NextUnit(ref state), 12);
    }

    [Fact]
    public void ADifferentStateGivesItsOwnValues()
    {
        // The same definition with the state 12345 at the start.
        uint state = 12345;
        Assert.Equal(0.9797282677609473, SeededRandom.NextUnit(ref state), 12);
        Assert.Equal(0.3067522644996643, SeededRandom.NextUnit(ref state), 12);
        Assert.Equal(0.484205421525985, SeededRandom.NextUnit(ref state), 12);
    }

    [Fact]
    public void EachStepAdvancesTheStateByTheGoldenIncrement()
    {
        uint state = 0;
        SeededRandom.NextUnit(ref state);
        Assert.Equal(0x6D2B79F5u, state);
        SeededRandom.NextUnit(ref state);
        Assert.Equal(0x6D2B79F5u * 2, state);
    }

    [Fact]
    public void EveryValueIsInZeroToOne()
    {
        uint state = 42;
        for (int i = 0; i < 10000; i++)
        {
            double v = SeededRandom.NextUnit(ref state);
            Assert.True(v is >= 0 and < 1, $"value {v} at step {i} is outside [0, 1)");
        }
    }
}
