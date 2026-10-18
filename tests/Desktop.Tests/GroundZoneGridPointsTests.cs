using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The grid points of one zone: each one's index inside the zone's 65 by 65 arrays and its index in the whole ground, row
/// by row, a zone being 64 metres across.
/// </summary>
public class GroundZoneGridPointsTests
{
    [Fact]
    public void TheFirstPointOfAZoneIsAtItsCornerInTheWholeGround()
    {
        // A 2 by 2 zone ground is 129 points across; the zone in row 1, column 0 starts at row 64 of the ground.
        var g = new Ground(129, 129, 0, 0, 2, 2);

        var first = g.ZoneGridPoints(1, 0).First();

        Assert.Equal((0, 64 * 129), first);
    }

    [Fact]
    public void ThePointsOfAZoneAreTakenAcrossARowFirst()
    {
        var g = new Ground(129, 129, 0, 0, 2, 2);

        var second = g.ZoneGridPoints(1, 0).ElementAt(1);

        Assert.Equal((1, 64 * 129 + 1), second);
    }

    [Fact]
    public void TheLastPointOfAZoneIsTheFarCornerOfItsSixtyFiveByFiveArray()
    {
        var g = new Ground(129, 129, 0, 0, 2, 2);

        var all = g.ZoneGridPoints(1, 0).ToList();

        Assert.Equal(65 * 65, all.Count);
        Assert.Equal((65 * 65 - 1, 128 * 129 + 64), all[^1]);
    }
}
