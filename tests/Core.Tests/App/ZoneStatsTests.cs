using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The map's zone filter: generated zones with their buildings, edits and distance to buildings.
/// </summary>
public class ZoneStatsTests
{
    [Fact]
    public void GeneratedZonesInTheWorldAreListed()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        int[] data = ZoneStats.Compute(world, edits, (x, z) => 1);
        int n = data.Length / ZoneStats.Stride;
        Assert.True(n > 0);
        var rows = Enumerable.Range(0, n).Select(i => data.Skip(i * ZoneStats.Stride).Take(ZoneStats.Stride).ToArray()).ToList();
        // Zones generated far outside the world (around an empty server's reference point, as in this
        // test world) are left out; zones with objects are in, generated or not.
        Assert.All(rows, r => Assert.True(Math.Abs(r[0]) < 170 && Math.Abs(r[1]) < 170));
        Assert.True(world.Zones!.Generated.All(g => Math.Abs(g.X) > 170));
        Assert.All(rows, r => Assert.Equal(0, r[7]));
        // The edited zone is marked, and every zone counts its objects.
        Assert.Contains(rows, r => r[4] == 1);
        Assert.Equal(world.ObjectRefs.Count(o => !o.IsTerrain && Math.Abs(o.Zone.X) < 170), rows.Sum(r => r[5]));
    }

    [Fact]
    public void DistanceToBuildingsCountsZones()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        int[] data = ZoneStats.Compute(world, edits, (x, z) => 1);
        var rows = Enumerable.Range(0, data.Length / ZoneStats.Stride).Select(i => data.Skip(i * ZoneStats.Stride).Take(ZoneStats.Stride).ToArray()).ToList();
        if (world.Pieces.Count == 0)
        {
            Assert.All(rows, r => Assert.Equal(-1, r[6]));
            return;
        }
        foreach (int[]? r in rows)
        {
            int expected = world.Pieces.Select(p => { var (x, z) = world.ObjectRefs[p.Id].Zone; return Math.Max(Math.Abs(x - r[0]), Math.Abs(z - r[1])); }).Min();
            Assert.Equal(expected, r[6]);
        }
    }
}
