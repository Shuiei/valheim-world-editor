using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The zone a point is in (64 m squares, centred on their corner), and a zone drawn near the middle of the world (inside
/// the world's radius: the game's 10 km, or the rules').
/// </summary>
public class SeedLocationsPlacerZonesTests
{
    private static SeedLocations.Placer Placer(WorldRules? rules = null) =>
        new(WorldGenerator.Create(new World { Seed = 12345, WorldGenVersion = 2, Rules = rules ?? WorldRules.Vanilla }), () => throw new InvalidOperationException("no biome map needed"));

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
        Assert.Equal((0, 0), Placer().ZoneNearMiddle(0f));
    }

    [Fact]
    public void AZoneDrawnNearTheMiddleHasItsCornerWithinTenKilometres()
    {
        var placer = Placer();
        for (int i = 0; i < 20; i++)
        {
            var (x, z) = placer.ZoneNearMiddle(640f);

            Assert.InRange(x, -10, 10);
            Assert.InRange(z, -10, 10);
            Assert.True(MathF.Sqrt(MathF.Pow(x * 64f, 2) + MathF.Pow(z * 64f, 2)) < 10000f);
        }
    }

    [Fact]
    public void AWiderWorldDrawsZonesPastTenKilometres()
    {
        // With the game's radius a reach of 15 km only ever gives zones within 10 km; with a 20 km world, some lie further.
        // Unity's random is seeded first: its unseeded state on a fresh thread draws the same zone forever.
        ValheimGen.UnityEngine.Random.InitState(4242); // nosemgrep
        var plain = Placer();
        var wide = Placer(new WorldRules { Radius = 20000f });
        static float Metres((int X, int Y) zone) => MathF.Sqrt(MathF.Pow(zone.X * 64f, 2) + MathF.Pow(zone.Y * 64f, 2));
        Assert.All(Enumerable.Range(0, 50).Select(_ => plain.ZoneNearMiddle(15000f)), zone => Assert.True(Metres(zone) < 10000f));
        var far = Enumerable.Range(0, 200).Select(_ => wide.ZoneNearMiddle(15000f)).ToList();
        Assert.All(far, zone => Assert.True(Metres(zone) < 20000f));
        Assert.Contains(far, zone => Metres(zone) >= 10000f);
    }
}
