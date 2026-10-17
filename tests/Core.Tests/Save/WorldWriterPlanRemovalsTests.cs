using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Which objects a save takes out before anything is written: the deleted ids, and the objects a zone reset clears.
/// </summary>
public class WorldWriterPlanRemovalsTests
{
    private static readonly ChunkFile File = new() { Chunk = 0x0101, Size = 0, Version = 1 };

    /// <summary>A world holding these objects in one chunk file, in this order.</summary>
    private static WorldSave World(params ObjectRef[] objects)
    {
        var w = new WorldSave { Directory = "none", SaveNumber = 1 };
        w.ObjectRefs.AddRange(objects);
        return w;
    }

    /// <summary>An object at x, z (its zone is the 64 m square it stands in) with this prefab.</summary>
    private static ObjectRef Obj(long start, float x, float z, int prefab = 1, bool piece = false) => new()
    {
        File = File,
        Start = start,
        Position = new Vector3(x, 0, z),
        Prefab = prefab,
        IsPiece = piece,
    };

    [Fact]
    public void DeletedIdsLeaveTheirObjectsOut()
    {
        var world = World(Obj(10, 0, 0), Obj(20, 0, 0), Obj(30, 0, 0));
        var plan = new WorldWriter.Planned([], [], []);

        WorldWriter.PlanRemovals(world, [0, 2], [], plan);

        Assert.Equal(new HashSet<long>([10, 30]), plan.Removals[File]);
        Assert.Equal(2, plan.DeletedCount);
        Assert.Equal(2, plan.Removed);
    }

    [Fact]
    public void AnIdPastTheListAndARepeatedIdTakeNoneOrOnce()
    {
        var world = World(Obj(10, 0, 0));
        var plan = new WorldWriter.Planned([], [], []);

        WorldWriter.PlanRemovals(world, [0, 0, 5, -1], [], plan);

        Assert.Equal(new HashSet<long>([10]), plan.Removals[File]);
        Assert.Equal(1, plan.DeletedCount);
        Assert.Equal(1, plan.Removed);
    }

    [Fact]
    public void AZoneResetClearsItsObjectsButNotTheZonesOthersNorTombstones()
    {
        var world = World(
            Obj(10, 0, 0),
            Obj(20, 0, 0, prefab: WorldWriter.TombstonePrefab),
            Obj(30, 100, 0));
        var plan = new WorldWriter.Planned([], [], []);

        WorldWriter.PlanRemovals(world, [], [new ZoneReset(0, 0, KeepBuildings: false, Ground: false)], plan);

        Assert.Equal(new HashSet<long>([10]), plan.Removals[File]);
        Assert.Equal(0, plan.DeletedCount);
        Assert.Equal(1, plan.Removed);
    }

    [Fact]
    public void AResetThatKeepsBuildingsKeepsItsPieces()
    {
        var world = World(Obj(10, 0, 0, piece: true), Obj(20, 0, 0));
        var plan = new WorldWriter.Planned([], [], []);

        WorldWriter.PlanRemovals(world, [], [new ZoneReset(0, 0, KeepBuildings: true, Ground: false)], plan);

        Assert.Equal(new HashSet<long>([20]), plan.Removals[File]);
        Assert.Equal(1, plan.Removed);
    }
}
