using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The height difference round a point: with no radius every sample is the point itself, so there is no difference, and
/// the difference is never negative for a real radius.
/// </summary>
public sealed class WorldGeneratorTerrainDeltaTests
{
    /// <summary>With a radius of zero the samples are all the centre, so there is no height difference.</summary>
    [Fact]
    public void ANoRadiusGivesNoHeightDifference()
    {
        var gen = SeedLocationsSupport.Seed();

        gen.GetTerrainDelta(new Vector3(100f, 0f, 100f), 0f, out float delta, out _);

        Assert.Equal(0f, delta);
    }

    /// <summary>A real radius gives a height difference that is finite and not negative.</summary>
    [Fact]
    public void AHeightDifferenceIsFiniteAndNotNegative()
    {
        var gen = SeedLocationsSupport.Seed();

        gen.GetTerrainDelta(new Vector3(100f, 0f, 100f), 40f, out float delta, out _);

        Assert.True(float.IsFinite(delta));
        Assert.True(delta >= 0f);
    }
}
