using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The setup of the seed location tests: the generator of seed 5DCcdIcuYJ and the check that a location was placed.
/// </summary>
public static class SeedLocationsSupport
{
    /// <summary>
    /// The generator of seed 5DCcdIcuYJ.
    /// </summary>
    public static WorldGenerator Seed() => TerrainEditor.App.SeedPreview.Generator("5DCcdIcuYJ");

    /// <summary>
    /// Asserts a location of this prefab was placed at x, z (within 1 cm).
    /// </summary>
    public static void Has(List<SeedLocations.Placed> placed, string prefab, float x, float z) =>
        Assert.Contains(placed, p => p.Rule.prefab == prefab && MathF.Abs(p.X - x) < 0.01f && MathF.Abs(p.Z - z) < 0.01f);
}
