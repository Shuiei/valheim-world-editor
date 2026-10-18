using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The spots kept where nothing is too near: nothing within 1 m, the same kind within 3 m, a building within 4 m.
/// </summary>
public sealed class GrowthClearanceTests
{
    /// <summary>A spot of the given kind at a place on the ground.</summary>
    private static Regrow.Spot Spot(string name, float x, float z) => new(name, x, 0, z, 0, 0, 0, 1);

    [Fact]
    public void ASpotWithNothingNearbyStays()
    {
        var kept = new List<Regrow.Spot> { Spot("Birch", 0, 0) };
        Assert.Equal(kept, Growth.Unobstructed(kept, []));
    }

    [Fact]
    public void AnythingWithinOneMetreRefusesTheSpot()
    {
        var kept = new List<Regrow.Spot> { Spot("Birch", 0, 0) };
        var standing = new List<(float X, float Z, string? Name, ObjectKind Kind)> { (0.5f, 0, "Rock", ObjectKind.Rocks) };
        Assert.Empty(Growth.Unobstructed(kept, standing));
    }

    [Fact]
    public void TheSameKindWithinThreeMetresRefusesTheSpot()
    {
        var kept = new List<Regrow.Spot> { Spot("Birch", 0, 0) };
        var standing = new List<(float X, float Z, string? Name, ObjectKind Kind)> { (2, 0, "Birch", ObjectKind.Trees) };
        Assert.Empty(Growth.Unobstructed(kept, standing));
    }

    [Fact]
    public void ADifferentNameWithinThreeMetresKeepsTheSpot()
    {
        var kept = new List<Regrow.Spot> { Spot("Birch", 0, 0) };
        var standing = new List<(float X, float Z, string? Name, ObjectKind Kind)> { (2, 0, "Oak", ObjectKind.Trees) };
        Assert.Equal(kept, Growth.Unobstructed(kept, standing));
    }

    [Fact]
    public void ABuildingWithinTheFarthestTestRefusesTheSpot()
    {
        var kept = new List<Regrow.Spot> { Spot("Birch", 0, 0) };
        var standing = new List<(float X, float Z, string? Name, ObjectKind Kind)> { (3.5f, 0, "wood_wall", ObjectKind.Buildings) };
        Assert.Empty(Growth.Unobstructed(kept, standing));
    }

    [Fact]
    public void ABuildingBeyondTheFarthestTestKeepsTheSpot()
    {
        var kept = new List<Regrow.Spot> { Spot("Birch", 0, 0) };
        var standing = new List<(float X, float Z, string? Name, ObjectKind Kind)> { (5, 0, "wood_wall", ObjectKind.Buildings) };
        Assert.Equal(kept, Growth.Unobstructed(kept, standing));
    }

    [Fact]
    public void TheSpotsThatStayKeepTheirOrder()
    {
        var a = Spot("Birch", 0, 0);
        var b = Spot("Birch", 100, 0);
        var c = Spot("Birch", 200, 0);
        var standing = new List<(float X, float Z, string? Name, ObjectKind Kind)> { (101, 0, "Birch", ObjectKind.Trees) };
        Assert.Equal([a, c], Growth.Unobstructed([a, b, c], standing));
    }
}
