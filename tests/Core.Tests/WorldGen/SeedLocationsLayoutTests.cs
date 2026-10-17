using ValheimGen;
using Xunit;

using static WorldEditor.Tests.SeedLocationsSupport;

namespace WorldEditor.Tests;

/// <summary>
/// A seed's layout as far as asked: the start temple alone, and only the kinds asked for.
/// </summary>
public class SeedLocationsLayoutTests
{
    [Fact]
    public void LayingOutTheStartPlacesOnlyTheTemple()
    {
        Fixtures.UseLocations();

        var placed = new SeedLocations.Layout(Seed()).Place(SeedLocations.Which.Start, TestContext.Current.CancellationToken);

        Assert.Equal("StartTemple", Assert.Single(placed).Rule.prefab);
    }

    [Fact]
    public void LayingOutAKindPlacesOnlyThatKind()
    {
        Fixtures.UseLocations();

        var placed = new SeedLocations.Layout(Seed()).PlaceKinds(["StartTemple"], TestContext.Current.CancellationToken);

        Assert.NotEmpty(placed);
        Assert.All(placed, p => Assert.Equal("StartTemple", p.Rule.prefab));
    }

    [Fact]
    public void AKindNoLocationHasIsNothing()
    {
        Fixtures.UseLocations();

        Assert.Empty(new SeedLocations.Layout(Seed()).PlaceKinds(["NoSuchLocation"], TestContext.Current.CancellationToken));
    }
}
