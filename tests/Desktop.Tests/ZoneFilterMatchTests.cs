using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// ZoneFilter.Match: a zone with pieces never matches, the buildings' clearance and the other filters keep or leave out
/// zones by their ZoneStats row, and the objects of the zones kept are added up.
/// </summary>
public class ZoneFilterMatchTests
{
    /// <summary>One ZoneStats row: x, z, biome, pieces, edited, objects, distance to buildings (-1 for none), generated.</summary>
    private static int[] Row(int x, int z, int biome = 3, int pieces = 0, int edited = 0, int objects = 0, int distance = -1, int generated = 1) =>
        [x, z, biome, pieces, edited, objects, distance, generated];

    /// <summary>A filter with the given settings (no biome list means every biome; no radius limit).</summary>
    private static ZoneFilter Filter(IReadOnlySet<int>? biomes = null, bool noBuildings = false, int distance = 0, bool noEdits = false, bool onlyGenerated = false, float? minRadius = null) =>
        new(biomes ?? new HashSet<int>(), noBuildings, distance, noEdits, onlyGenerated, minRadius, null);

    [Fact]
    public void AZoneWithPiecesNeverMatchesAndTheOthersCountTheirObjects()
    {
        int[] rows = [.. Row(0, 0, pieces: 1, objects: 5), .. Row(1, 0, objects: 2)];
        var (zones, objects) = Filter().Match(rows);
        Assert.Equal<(int X, int Z)>([(1, 0)], zones);
        Assert.Equal(2, objects);
    }

    [Fact]
    public void NoBuildingsLeavesOutOnlyTheZonesWithinItsDistance()
    {
        int[] rows = [.. Row(0, 0, distance: 1), .. Row(1, 0, distance: -1), .. Row(2, 0, distance: 3)];
        var (zones, _) = Filter(noBuildings: true, distance: 2).Match(rows);
        Assert.Equal<(int X, int Z)>([(1, 0), (2, 0)], zones);
    }

    [Fact]
    public void NoEditsAndOnlyGeneratedLeaveOutTheirZones()
    {
        int[] rows = [.. Row(0, 0, edited: 1), .. Row(1, 0, generated: 0), .. Row(2, 0)];
        var (zones, _) = Filter(noEdits: true, onlyGenerated: true).Match(rows);
        Assert.Equal<(int X, int Z)>([(2, 0)], zones);
    }

    [Fact]
    public void ABiomeListKeepsOnlyTheBiomesInIt()
    {
        int[] rows = [.. Row(0, 0, biome: 4), .. Row(1, 0, biome: 3)];
        var (zones, _) = Filter(biomes: new HashSet<int> { 3 }).Match(rows);
        Assert.Equal<(int X, int Z)>([(1, 0)], zones);
    }

    [Fact]
    public void AMinimumRadiusLeavesOutTheZonesNearerTheMiddle()
    {
        // A zone's distance from the middle is its metres: sqrt(x² + z²) × 64, so zone (1, 0) is 64 m and (2, 0) is 128 m.
        int[] rows = [.. Row(1, 0), .. Row(2, 0)];
        var (zones, _) = Filter(minRadius: 100f).Match(rows);
        Assert.Equal<(int X, int Z)>([(2, 0)], zones);
    }
}
