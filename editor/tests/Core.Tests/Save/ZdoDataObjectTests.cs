using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Objects copied from templates that hold only some kinds of data: a copy keeps just what it should, and a new scale
/// replaces the one the object had.
/// </summary>
public class ZdoDataObjectTests
{
    /// <summary>
    /// A chest holding only text and its builder, saved into a copy of the test world.
    /// </summary>
    private static (WorldSave World, int Id) TextChest(TempWorld w, string scale)
    {
        var world = w.Load();
        int chest = world.Objects.First(o => o.Prefab == StableHash.Of("piece_chest_wood")).Id;
        var z = ZdoData.Parse(world.ObjectBytes(chest));
        z.Set("strings", StableHash.Of("text"), "only text");
        z.Set("longs", StableHash.Of("creator"), "99");
        if (scale != "")
        {
            z.Set("vec3", StableHash.Of("scale"), scale);
        }
        var r = WorldWriter.Save(world, [], added: [new NewObject(-1, z.Prefab, z.Position + new Vector3(4, 0, 4), Vector3.Zero, 0f, Fresh: false, Raw: z.Serialize())]);
        Assert.True(r.Saved, r.Message);
        world = w.Load();
        return (world, Enumerable.Range(0, world.ObjectRefs.Count).Single(i => ZdoData.Parse(world.ObjectBytes(i)).StringList.Any(s => s.Value == "only text")));
    }

    [Fact]
    public void ACopyOfAnObjectWithSomeDataKeepsJustThat()
    {
        using var w = new TempWorld();
        var (world, id) = TextChest(w, "");
        var o = world.ObjectRefs[id];
        byte[] src = File.ReadAllBytes(Path.Combine(world.Directory, o.File.FileName));
        var moved = ZdoData.Parse(ZdoBuilder.Build(src, o, o.File.WorldVersion, Vector3.One, Vector3.Zero, 0f));
        Assert.Equal("only text", moved.StringList.Single().Value);
        Assert.Equal(99, moved.LongList.Single().Value);
        Assert.Empty(moved.IntList);
        Assert.Empty(moved.FloatList);
        // A scale adds the vector section the chest did not have.
        var scaled = ZdoData.Parse(ZdoBuilder.Build(src, o, o.File.WorldVersion, Vector3.One, Vector3.Zero, 2f));
        Assert.Equal(new Vector3(2f), scaled.Vec3List.Single().Value);
    }

    [Fact]
    public void ANewScaleReplacesTheOneTheObjectHad()
    {
        using var w = new TempWorld();
        var (world, id) = TextChest(w, "3 3 3");
        var o = world.ObjectRefs[id];
        byte[] src = File.ReadAllBytes(Path.Combine(world.Directory, o.File.FileName));
        var keep = ZdoData.Parse(ZdoBuilder.Build(src, o, o.File.WorldVersion, Vector3.One, Vector3.Zero, 0f));
        Assert.Equal(new Vector3(3f), keep.Vec3List.Single().Value);
        var scaled = ZdoData.Parse(ZdoBuilder.Build(src, o, o.File.WorldVersion, Vector3.One, Vector3.Zero, 1.25f));
        Assert.Equal(new Vector3(1.25f), Assert.Single(scaled.Vec3List).Value);
    }
}
