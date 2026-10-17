using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The zone a point is in (64 m squares, centred on their corner), and a zone drawn near the middle of the world.
/// </summary>
public class SeedLocationsPlacerZonesTests
{
    [Fact]
    public void APointIsInTheZoneOfItsSquare()
    {
        Assert.Equal((0, 0), SeedLocations.Placer.ZoneOf((31.9f, 0f)));
        Assert.Equal((1, 0), SeedLocations.Placer.ZoneOf((32f, 0f)));
    }

    [Fact]
    public void APointWestOfTheMiddleIsInTheZoneBeforeIt()
    {
        Assert.Equal((-1, 0), SeedLocations.Placer.ZoneOf((-32.5f, 0f)));
        Assert.Equal((0, 0), SeedLocations.Placer.ZoneOf((-32f, 0f)));
    }

    [Fact]
    public void ANoReachDrawsTheMiddleZone()
    {
        Assert.Equal((0, 0), SeedLocations.Placer.ZoneNearMiddle(0f));
    }

    [Fact]
    public void AZoneDrawnNearTheMiddleHasItsCornerWithinTenKilometres()
    {
        for (int i = 0; i < 20; i++)
        {
            var (x, z) = SeedLocations.Placer.ZoneNearMiddle(640f);

            Assert.InRange(x, -10, 10);
            Assert.InRange(z, -10, 10);
            Assert.True(MathF.Sqrt(MathF.Pow(x * 64f, 2) + MathF.Pow(z * 64f, 2)) < 10000f);
        }
    }
}
