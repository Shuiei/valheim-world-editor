using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// How far each zone is from the nearest zone with pieces (ZoneDistance): the larger of the x and z steps, -1 when no
/// piece zone is reached.
/// </summary>
public class ZoneDistanceTests
{
    [Fact]
    public void WithNoPieceZonesNothingIsReached()
    {
        Assert.Equal(-1, new ZoneDistance([], 2).Of(1, 1));
    }

    [Fact]
    public void ADistanceIsTheLargerOfTheStepsInEitherDirection()
    {
        var distance = new ZoneDistance([(0, 0)], 3);
        Assert.Equal(2, distance.Of(2, 1));
    }

    [Fact]
    public void AZoneWithPiecesIsAtDistanceZero()
    {
        Assert.Equal(0, new ZoneDistance([(0, 0)], 3).Of(0, 0));
    }

    [Fact]
    public void APieceZoneOutsideTheSquareIsIgnored()
    {
        Assert.Equal(-1, new ZoneDistance([(5, 0)], 3).Of(0, 0));
    }
}
