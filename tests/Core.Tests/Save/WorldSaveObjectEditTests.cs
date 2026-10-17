using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Objects deleted, placed and copied in the test world: what is saved reads back where it was put, and a moved object keeps its data.
/// </summary>
public class WorldSaveObjectEditTests
{
    [Fact]
    public void DeletedObjectsAreGone()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var (id, prefab, position, rotation, scale) = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
        WorldWriter.Result r = WorldWriter.Save(world, [], deleted: [id]);
        Assert.True(r.Saved, r.Message);
        var again = WorldSave.Load(w.Dir);
        Assert.Equal(world.ObjectCount - 1, again.ObjectCount);
        Assert.DoesNotContain(again.Objects, o => o.Prefab == prefab && Vector3.Distance(o.Position, position) < 0.01f);
    }

    [Fact]
    public void NewObjectsAreSavedWhereTheyWerePlaced()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        Vector3 near = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1")).Position;
        var copy = new NewObject(-1, Fixtures.Hash("Beech1"), near + new Vector3(2, 0, 2), new Vector3(0, 90, 0), 1.2f);
        // A kind the world has none of: built blank from the prefab catalogue.
        var blank = new NewObject(-2, Fixtures.Hash("sapling_turnip"), near + new Vector3(-2, 0, 2), Vector3.Zero, 0f);
        Assert.False(world.Templates.ContainsKey(blank.Prefab));
        Assert.True(world.CanCreate(blank.Prefab));
        WorldWriter.Result r = WorldWriter.Save(world, [], added: [copy, blank]);
        Assert.True(r.Saved, r.Message);
        Assert.Equal(2, r.ObjectsAdded);

        var again = WorldSave.Load(w.Dir);
        Assert.Equal(world.ObjectCount + 2, again.ObjectCount);
        var (id, prefab, position, rotation, scale) = again.Objects.Single(o => o.Prefab == copy.Prefab && Vector3.Distance(o.Position, copy.Position) < 0.01f);
        Assert.Equal(90f, rotation.Y, 0);
        Assert.Equal(1.2f, scale.X, 2);
        // A sapling is placed with the cultivator: with a builder chosen it is a player-built piece.
        Assert.Contains(again.Objects.Select(o => (o.Prefab, o.Position)).Concat(again.Pieces.Select(p => (p.Prefab, p.Position))),
            o => o.Prefab == blank.Prefab && Vector3.Distance(o.Position, blank.Position) < 0.01f);
    }

    [Fact]
    public void CopiesAreFreshButMovesKeepTheirData()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        int chest = world.Objects.First(o => o.Prefab == Fixtures.Hash("piece_chest_wood")).Id;
        ObjectRef source = world.ObjectRefs[chest];
        byte[] src = File.ReadAllBytes(Path.Combine(world.Directory, source.File.FileName));
        byte[] moved = ZdoBuilder.Build(src, source, source.File.WorldVersion, new Vector3(1, 50, 1), Vector3.Zero, 0f, fresh: false);
        byte[] fresh = ZdoBuilder.Build(src, source, source.File.WorldVersion, new Vector3(1, 50, 1), Vector3.Zero, 0f, fresh: true);
        byte[] original = src[(int)source.DataStart..(int)source.End];
        Assert.True(moved.Length - DataStart(moved) >= original.Length, "a move keeps all of the object's data");
        Assert.True(fresh.Length <= moved.Length, "a copy keeps no more than the builder and scale");
    }

    /// <summary>
    /// Bytes before the data sections: flags (2), position (12), prefab (4), rotation (0, 2 or 4).
    /// </summary>
    private static int DataStart(byte[] zdo)
    {
        ushort flags = BitConverter.ToUInt16(zdo, 0);
        int p = 2 + ((flags & 0x2000) != 0 ? 4 : 12) + 4;
        if ((flags & 0x1000) != 0)
        {
            p += (BitConverter.ToUInt16(zdo, p) & 0x8000) != 0 ? 2 : 4;
        }
        return p;
    }

    // A new piece made from what was taken earlier gets the builder taken then, not the one chosen now.
    [Fact]
    public void ANewPieceTakesTheBuilderGiven()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var wall = new NewObject(-1, StableHash.Of("woodwall"), new Vector3(1, 2, 3), Vector3.Zero, 0f);
        long? Creator(long builder) => world.NewObjectBytes(wall, world.SourceReader(), builder) is { } b
            ? ZdoData.Parse(b).LongList.Where(i => i.Key == StableHash.Of("creator")).Select(i => (long?)i.Value).SingleOrDefault()
            : null;
        Assert.Equal(4242L, Creator(4242));
        Assert.Equal(99L, Creator(99));
    }
}
