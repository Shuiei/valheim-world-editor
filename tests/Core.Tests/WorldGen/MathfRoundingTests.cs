using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Mathf's rounding helpers: a value rounds up (Ceil, CeilToInt) or down (FloorToInt) to the whole number on that side,
/// including for negative values, and a whole number stays as it is.
/// </summary>
public class MathfRoundingTests
{
    [Fact]
    public void CeilRoundsAFractionUpToTheNextWholeNumber()
    {
        Assert.Equal(2f, Mathf.Ceil(1.2f));
    }

    [Fact]
    public void CeilRoundsANegativeFractionTowardZero()
    {
        Assert.Equal(-1f, Mathf.Ceil(-1.2f));
    }

    [Fact]
    public void CeilToIntLeavesAWholeNumberAlone()
    {
        Assert.Equal(4, Mathf.CeilToInt(4f));
    }

    [Fact]
    public void CeilToIntRoundsAnyFractionUp()
    {
        Assert.Equal(3, Mathf.CeilToInt(2.0001f));
        Assert.Equal(0, Mathf.CeilToInt(-0.5f));
    }

    [Fact]
    public void FloorToIntRoundsAFractionDown()
    {
        Assert.Equal(2, Mathf.FloorToInt(2.9f));
    }

    [Fact]
    public void FloorToIntRoundsANegativeFractionAwayFromZero()
    {
        Assert.Equal(-1, Mathf.FloorToInt(-0.5f));
    }
}
