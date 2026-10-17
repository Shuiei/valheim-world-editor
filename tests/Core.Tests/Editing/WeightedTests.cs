using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Weighted.Pick: each item takes as much of the draw's range as its weight; a draw that float rounding carries past
/// the last weight still gives the last item.
/// </summary>
public class WeightedTests
{
    [Theory]
    [InlineData(0.0, "a")]
    [InlineData(0.2, "a")]
    [InlineData(0.3, "b")]
    [InlineData(0.9, "c")]
    public void EachItemTakesItsWeightsShareOfTheDraw(double draw, string picked)
    {
        (string Name, float Weight)[] items = [("a", 1), ("b", 2), ("c", 1)];

        Assert.Equal(picked, Weighted.Pick(items, i => i.Weight, draw).Name);
    }

    [Fact]
    public void ADrawLeftOverByRoundingGivesTheLastItem()
    {
        // 0.1 + 0.2 + 0.15 in floats: a draw rounding to 1 keeps 1.5e-8 after the last weight (First() threw).
        (string Name, float Weight)[] items = [("a", 0.1f), ("b", 0.2f), ("c", 0.15f)];

        Assert.Equal("c", Weighted.Pick(items, i => i.Weight, 0.9999999999).Name);
    }
}
