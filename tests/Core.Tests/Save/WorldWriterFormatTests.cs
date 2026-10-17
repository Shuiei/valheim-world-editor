using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.WorldWriterTestHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// The save formats the world writer keeps (41 and 42, and a world the game migrated) and refuses (newer than the editor knows,
/// or older than it writes).
/// </summary>
public class WorldWriterFormatTests
{
    /// <summary>
    /// The save format a file of a save names: a short at the head of chunk files and the index, an int at the head of
    /// the zone file and the marker, and after the length in the metadata.
    /// </summary>
    private static int Format(string path)
    {
        byte[] b = File.ReadAllBytes(path);
        return Path.GetExtension(path) switch
        {
            ".chunk" or ".chunks" => BitConverter.ToInt16(b),
            ".fwl2" => BitConverter.ToInt32(b, 4),
            _ => BitConverter.ToInt32(b),
        };
    }

    [Theory]
    [InlineData("CITest", 41)]
    [InlineData("CITest42", 42)]
    public void AWorldIsSavedInTheFormatItWasReadIn(string fixture, int format)
    {
        // The writer took only format 41 (Valheim 1.0.20 saves 42) and wrote 41 into the index and the marker.
        using var w = new TempWorld(fixture);
        var world = w.Load();
        var t = world.TerrainZones[0];
        var (id, prefab, position, _, _) = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
        var tree = new NewObject(-1, prefab, position + new Vector3(2, 0, 2), Vector3.Zero, 0f);

        var r = WorldWriter.Save(world, [Raised(t.ZoneX, t.ZoneZ, 1.5f, 500)], deleted: [id], added: [tree]);

        Assert.True(r.Saved, r.Message);
        Assert.All(r.Files, f => Assert.Equal(format, Format(f)));
        var again = WorldSave.Load(w.Dir);
        Assert.Equal((format, world.SaveNumber + 1), (again.SaveVersion, again.SaveNumber));
        Assert.All(again.Chunks, c => Assert.Equal(format, c.WorldVersion));
        Assert.Equal(1.5f, again.TerrainZones.Single(q => q.ZoneX == t.ZoneX && q.ZoneZ == t.ZoneZ).LevelDelta[500], 4);
        Assert.DoesNotContain(again.Objects, o => o.Prefab == prefab && Vector3.Distance(o.Position, position) < 0.01f);
        Assert.Contains(again.Objects, o => o.Prefab == prefab && Vector3.Distance(o.Position, tree.Position) < 0.01f);
    }

    [Fact]
    public void EachChunkFileKeepsItsFormatInAWorldTheGameMigrated()
    {
        // Valheim 1.0.20 saves a format 41 world with a 42 index and leaves the chunk files it does not write again in
        // 41: here the test world's ground chunk as the game left it, the rest as it wrote it.
        using var w = new TempWorld("CITest42");
        ChunkFile ground = w.Load().TerrainZones[0].Source!.File;
        File.Copy(Path.Combine(Fixtures.World, "20_20__1_1.chunk"), Path.Combine(w.Dir, ground.FileName), overwrite: true);
        var world = w.Load();
        var t = world.TerrainZones[0];
        Assert.Equal(41, t.Source!.File.WorldVersion);

        var r = WorldWriter.Save(world, [Raised(t.ZoneX, t.ZoneZ, 1.5f, 500)]);

        Assert.True(r.Saved, r.Message);
        var again = WorldSave.Load(w.Dir);
        Assert.Equal(42, again.SaveVersion);
        Assert.Equal(41, again.TerrainZones[0].Source!.File.WorldVersion);
        Assert.All(again.Chunks.Where(c => c.Chunk != ground.Chunk), c => Assert.Equal(42, c.WorldVersion));
        Assert.Equal(1.5f, again.TerrainZones[0].LevelDelta[500], 4);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ASaveFormatNewerThanTheEditorKnowsIsRefused(bool inTheIndex)
    {
        // In the index, or in one chunk file only (a newer game wrote that one again).
        using var w = new TempWorld("CITest42");
        var world = w.Load();
        if (inTheIndex)
        {
            world.SaveVersion = WorldVersion.Latest + 1;
        }
        else
        {
            world.Chunks[^1].WorldVersion = WorldVersion.Latest + 1;
        }
        var before = Files(w.Dir);

        var r = WorldWriter.Save(world, [Raised(world.TerrainZones[0].ZoneX, world.TerrainZones[0].ZoneZ, 1, 100)]);

        Assert.False(r.Saved);
        Assert.Equal("This world uses save format 43; the editor saves only formats 41 to 42.", r.Message);
        AssertSame(before, w.Dir);
    }

    [Fact]
    public void AnOlderSaveFormatIsNamedFromTheChunkThatHasIt()
    {
        // It named the first chunk file's format: "uses save format 41; the writer only supports 41".
        using var w = new TempWorld();
        var world = w.Load();
        Assert.True(world.Chunks.Count > 1);
        world.Chunks[^1].WorldVersion = 39;

        var r = WorldWriter.Save(world, [Raised(world.TerrainZones[0].ZoneX, world.TerrainZones[0].ZoneZ, 1, 100)]);

        Assert.False(r.Saved);
        Assert.StartsWith("This world uses save format 39;", r.Message);
    }

    [Fact]
    public void AnotherSaveFormatIsRefusedUntouched()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var before = Files(w.Dir);
        world.Chunks[0].WorldVersion = 40;
        var t = world.TerrainZones[0];
        var r = WorldWriter.Save(world, [Raised(t.ZoneX, t.ZoneZ, 1, 500)]);
        Assert.False(r.Saved);
        Assert.Contains("save format 40", r.Message);
        AssertSame(before, w.Dir);
    }
}
