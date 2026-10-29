using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The river and lake points' nearest-point search and merging: the nearest point within a range (not the point itself), and
/// points closer than the range joined into their midpoint.
/// </summary>
public class WorldGenPointMergeTests
{
    [Fact]
    public void NearestIsTheClosestPointWithinRange()
    {
        var points = new List<Vector2> { new(0, 0), new(3, 0), new(10, 0) };

        Assert.Equal(0, WorldGenerator.Nearest(points, new Vector2(1, 0), 5));
        Assert.Equal(-1, WorldGenerator.Nearest(points, new Vector2(20, 0), 5));
    }

    [Fact]
    public void NearestSkipsThePointItself()
    {
        var points = new List<Vector2> { new(0, 0), new(3, 0) };

        Assert.Equal(1, WorldGenerator.Nearest(points, new Vector2(0, 0), 5));
    }

    [Fact]
    public void MergeJoinsPointsWithinRangeAndKeepsTheOthers()
    {
        var points = new List<Vector2> { new(0, 0), new(1, 0), new(10, 0) };

        var merged = WorldGenerator.Merge(points, 2);

        Assert.Equal(2, merged.Count);
        Assert.Equal(new Vector2(0.5f, 0), merged[0]);
        Assert.Equal(new Vector2(10, 0), merged[1]);
    }
}
