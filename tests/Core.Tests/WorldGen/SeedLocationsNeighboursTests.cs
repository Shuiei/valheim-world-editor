using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Instances by name, and whether one is within a distance of a point (measured in 3D, strictly closer than the distance).
/// </summary>
public class SeedLocationsNeighboursTests
{
    [Fact]
    public void AnInstanceWithinTheDistanceIsFoundUnderItsName()
    {
        var neighbours = new SeedLocations.Neighbours();
        neighbours.Add("a", new SeedLocations.Placed(new SeedLocations.Rule { prefab = "a" }, 0, 0, 0));

        Assert.True(neighbours.Within("a", new Vector3(3, 0, 4), 5.1f));
    }

    [Fact]
    public void AnInstanceExactlyTheDistanceAwayIsNotWithin()
    {
        var neighbours = new SeedLocations.Neighbours();
        neighbours.Add("a", new SeedLocations.Placed(new SeedLocations.Rule { prefab = "a" }, 0, 0, 0));

        Assert.False(neighbours.Within("a", new Vector3(3, 0, 4), 5));
    }

    [Fact]
    public void TheDistanceIsMeasuredInThreeDimensions()
    {
        var neighbours = new SeedLocations.Neighbours();
        neighbours.Add("a", new SeedLocations.Placed(new SeedLocations.Rule { prefab = "a" }, 0, 0, 0));

        Assert.True(neighbours.Within("a", new Vector3(0, 4, 0), 5));
    }

    [Fact]
    public void AnotherNameIsNotWithin()
    {
        var neighbours = new SeedLocations.Neighbours();
        neighbours.Add("a", new SeedLocations.Placed(new SeedLocations.Rule { prefab = "a" }, 0, 0, 0));

        Assert.False(neighbours.Within("b", new Vector3(0, 0, 0), 5));
    }
}
