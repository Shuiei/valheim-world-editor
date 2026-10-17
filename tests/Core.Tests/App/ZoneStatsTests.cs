using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The map's zone filter: generated zones with their buildings, edits and distance to buildings, objects added in
/// the session counted.
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

    [Fact]
    public void ObjectsAddedInTheSessionAreCounted()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        // A fence (a piece players build), a tree, and a fence given no builder (a generated ruin's): zone (78, 78).
        edits.AddObjects([
            new NewObject(-1, Fixtures.Hash("wood_fence"), new Vector3(5000, 30, 5000), Vector3.Zero, 0f),
            new NewObject(-2, Fixtures.Hash("Beech1"), new Vector3(5010, 30, 5000), Vector3.Zero, 0f),
            new NewObject(-3, Fixtures.Hash("wood_fence"), new Vector3(5000, 30, 5010), Vector3.Zero, 0f, Data: [ObjectField.NoBuilder]),
        ]);

        int[] data = ZoneStats.Compute(world, edits, (x, z) => 1);

        int[] row = Enumerable.Range(0, data.Length / ZoneStats.Stride).Select(i => data.Skip(i * ZoneStats.Stride).Take(ZoneStats.Stride).ToArray())
            .Single(r => r[0] == 78 && r[1] == 78);
        Assert.Equal(1, row[3]);
        Assert.Equal(3, row[5]);
        Assert.Equal(0, row[6]);
    }
}
