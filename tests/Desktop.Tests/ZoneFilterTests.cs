using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map's zone filter on made-up zone rows: each of its tests (biome, buildings near or none at all, ground edits,
/// generated, least and greatest distance from the middle) leaving out a zone on its own.
/// </summary>
public class ZoneFilterTests
{
    // x, z, biome, pieces, edited, objects, distance to buildings (-1: none), generated.
    private static readonly int[] Rows =
    [
        2, 0, 1, 0, 0, 3, -1, 1,    // kept: no buildings anywhere, 128 m out
        3, 0, 2, 0, 0, 1, -1, 1,    // Swamp: not a biome asked for
        4, 0, 1, 1, 0, 1, 0, 1,     // buildings in it
        5, 0, 1, 0, 0, 1, 1, 1,     // a zone from buildings
        6, 0, 8, 0, 0, 2, 5, 1,     // kept: Black Forest, five zones from buildings
        7, 0, 1, 0, 1, 1, -1, 1,    // ground edited
        8, 0, 1, 0, 0, 1, -1, 0,    // never generated
        1, 0, 1, 0, 0, 1, -1, 1,    // 64 m out: too near the middle
        20, 0, 1, 0, 0, 1, -1, 1,   // 1280 m out: too far
        9, 9, 9,                    // a row cut short: not a zone
    ];

    [Fact]
    public void EachTestLeavesOutItsOwnZones()
    {
        var strict = new ZoneFilter(new HashSet<int> { 1, 8 }, NoBuildings: true, Distance: 2, NoEdits: true, OnlyGenerated: true, MinRadius: 100, MaxRadius: 1000);
        var (zones, objects) = strict.Match(Rows);
        Assert.Equal(new[] { (2, 0), (6, 0) }, zones);
        Assert.Equal(5, objects);
    }
}
