using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// What ground moved under a natural object takes away (Uplift): an object on the ground, or a little above it, stands on
/// it; one high above (in a dungeon, 5000 m up) does not.
/// </summary>
public class UpliftTests
{
    [Fact]
    public void AnObjectStandsOnTheGroundUnlessItIsHighAboveIt()
    {
        Assert.True(Uplift.OnTheGround(30, 30));
        Assert.True(Uplift.OnTheGround(25, 30));
        Assert.True(Uplift.OnTheGround(30 + Uplift.OnGroundReach - 1, 30));
        Assert.False(Uplift.OnTheGround(30 + Uplift.OnGroundReach, 30));
        Assert.False(Uplift.OnTheGround(5030, 30));
    }
}
