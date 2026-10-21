using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The rivers' points (WorldGenerator.RiverPoint): equal when the place and width are, NaN and -0 included.
/// </summary>
public class WorldGeneratorWaterTests
{
    [Theory]
    [InlineData(float.NaN, float.NaN)]
    [InlineData(-0f, 0f)]
    [InlineData(5f, 5f)]
    public void PointsWithTheSamePlaceAndWidthAreEqual(float odd, float twin)
    {
        EqualityAsserts.Same(new WorldGenerator.RiverPoint(new Vector2(odd, 1), odd), new WorldGenerator.RiverPoint(new Vector2(twin, 1), twin));
    }

    // The opposite width has the same square: the width alone tells them apart.
    [Theory]
    [InlineData(2f, 1f, 5f)]
    [InlineData(1f, 2f, 5f)]
    [InlineData(1f, 1f, -5f)]
    public void PointsDifferingInPlaceOrWidthAreNotEqual(float x, float y, float width)
    {
        EqualityAsserts.Differ(new WorldGenerator.RiverPoint(new Vector2(1, 1), 5), new WorldGenerator.RiverPoint(new Vector2(x, y), width));
    }
}
