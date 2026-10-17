using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Python's rounding of floats for the catalogues (CatalogueJson.Round and Significant9): half to even at the decimals asked,
/// the sign of zero kept, values that are not finite left as they are, and nine significant digits.
/// </summary>
public class CatalogueJsonRoundingTests
{
    [Theory]
    [InlineData(0.125, 2, 0.12)]
    [InlineData(0.375, 2, 0.38)]
    [InlineData(2.5, 0, 2.0)]
    [InlineData(3.5, 0, 4.0)]
    public void RoundIsPythonsRoundHalfToEven(double x, int decimals, double expected) =>
        Assert.Equal(expected, CatalogueJson.Round(x, decimals));

    [Fact]
    public void RoundKeepsTheSignOfZero() => Assert.True(double.IsNegative(CatalogueJson.Round(-0.0, 3)));

    [Fact]
    public void RoundLeavesNotANumberAsItIs() => Assert.True(double.IsNaN(CatalogueJson.Round(double.NaN, 2)));

    [Fact]
    public void RoundLeavesInfinityAsItIs() => Assert.Equal(double.PositiveInfinity, CatalogueJson.Round(double.PositiveInfinity, 2));

    [Fact]
    public void Significant9KeepsNineDigits() => Assert.Equal(1.23456789, CatalogueJson.Significant9(1.23456789012345));

    [Fact]
    public void Significant9OfZeroIsZero() => Assert.Equal(0.0, CatalogueJson.Significant9(0.0));

    [Fact]
    public void Significant9OfALargeNumberRoundsToNineDigitsOfItsSize() =>
        Assert.Equal(123456789000.0, CatalogueJson.Significant9(123456789012.0));
}
