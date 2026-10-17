using System.Numerics;
using TerrainEditor.App;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.AppDataPrefabs;

namespace WorldEditor.Tests;

/// <summary>
/// Zone statistics on a copy of the test world, with player-built pieces: buildings per zone and the distance to them.
/// </summary>
[Collection("World files")]
public class AppDataZoneStatsTests
{
    private static readonly int Wall = StableHash.Of("wood_wall_half");

    /// <summary>
    /// Zone statistics cut into rows of ZoneStats.Stride values, one per zone.
    /// </summary>
    private static int[][] Rows(int[] data) => [.. Enumerable.Range(0, data.Length / ZoneStats.Stride).Select(i => data.Skip(i * ZoneStats.Stride).Take(ZoneStats.Stride).ToArray())];

    [Fact]
    public void ZoneStatsCountBuildingsAndTheDistanceToThem()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var (id, prefab, position, rotation, scale) = world.Objects.First(o => o.Prefab == Beech);
        // Two player-built walls in the tree's zone (built by player 777).
        long before = WorldSave.Builder;
        try
        {
            WorldSave.Builder = 777;
            var r = WorldWriter.Save(world, [], added:
            [
                new NewObject(-1, Wall, position + new Vector3(2, 0, 2), Vector3.Zero, 0f),
                new NewObject(-2, Wall, position + new Vector3(4, 0, 2), Vector3.Zero, 0f),
            ]);
            Assert.True(r.Saved, r.Message);
        }
        finally
        {
            WorldSave.Builder = before;
        }
        world = w.Load();
        Assert.Equal(2, world.Pieces.Count);
        var (zoneX, zoneZ) = world.ObjectRefs[world.Pieces[0].Id].Zone;
        var edits = new EditStore(world);
        int[][] rows = Rows(ZoneStats.Compute(world, edits, (x, z) => x == zoneX && z == zoneZ ? 8 : 1));
        int[] at = rows.Single(r => r[0] == zoneX && r[1] == zoneZ);
        Assert.Equal(8, at[2]);
        Assert.Equal(2, at[3]);
        Assert.Equal(0, at[6]);
        // Every other zone: as many zones away as the larger of the x and z steps.
        Assert.All(rows.Where(r => r != at), r => Assert.Equal(Math.Max(Math.Abs(r[0] - zoneX), Math.Abs(r[1] - zoneZ)), r[6]));
        // Deleted walls are not buildings any more.
        edits.SetDeleted(world.Pieces.Select(p => p.Id), true);
        int[][] after = Rows(ZoneStats.Compute(world, edits, (_, _) => 1));
        Assert.All(after, r => Assert.Equal(0, r[3]));
        Assert.All(after, r => Assert.Equal(-1, r[6]));
    }

    [Fact]
    public void ZoneStatsNeedTheWorldsZoneList()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        typeof(WorldSave).GetProperty(nameof(WorldSave.Zones))!.SetValue(world, null);
        Assert.Empty(ZoneStats.Compute(world, new EditStore(world), (_, _) => 1));
    }
}
