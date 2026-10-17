using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The save reader on the test world: it loads the world as the game saves it (format 41 and 42), and the chunk files
/// and the save numbers read as the writer expects them.
/// </summary>
public class WorldSaveLoadTests
{
    [Fact]
    public void LoadsTheTestWorld()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        Assert.Equal("CITest", world.Name);
        Assert.Equal("GbXfypqU3G", world.SeedName);
        Assert.Equal(2, world.SaveNumber);
        Assert.Equal(41, world.SaveVersion);
        Assert.Equal(122, world.ObjectCount);
        Assert.Single(world.TerrainZones);
        Assert.True(world.Templates.ContainsKey(Fixtures.Hash("Beech1")));
        Assert.Contains(world.Objects, o => o.Prefab == Fixtures.Hash("piece_chest_wood"));
    }

    [Fact]
    public void ReadsTheTestWorldAsTheGameSavesItInFormat42()
    {
        // Valheim 1.0.20 saves worlds in format 42: the same objects, ground and names as format 41 come back.
        WorldSave old = WorldSave.Load(Fixtures.World), world = WorldSave.Load(Fixtures.World42);
        Assert.Equal((42, 3), (world.SaveVersion, world.SaveNumber));
        Assert.All(world.Chunks, c => Assert.Equal(42, c.WorldVersion));
        Assert.Equal((old.Name, old.SeedName, old.Seed, old.WorldGenVersion), (world.Name, world.SeedName, world.Seed, world.WorldGenVersion));
        Assert.Equal(old.Objects, world.Objects);
        Assert.Equal(old.Zones!.Generated, world.Zones!.Generated);
        Assert.Equal(42, world.Zones.FileVersion);
        var (a, b) = (Assert.Single(old.TerrainZones), Assert.Single(world.TerrainZones));
        Assert.Equal((a.ZoneX, a.ZoneZ), (b.ZoneX, b.ZoneZ));
        Assert.Equal(a.LevelDelta, b.LevelDelta);
        Assert.Equal(a.Paint, b.Paint);
    }

    [Fact]
    public void SourceReaderReadsEachChunkFileOnce()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        // Two objects of one chunk file share its bytes; an object of the other file has its own.
        var byFile = world.ObjectRefs.GroupBy(r => r.File).OrderByDescending(g => g.Count()).ToList();
        Assert.True(byFile.Count >= 2 && byFile[0].Count() >= 2);
        ObjectRef o = byFile[0].First(), sameFile = byFile[0].Skip(1).First(), otherFile = byFile[1].First();
        Func<ObjectRef, byte[]> read = world.SourceReader();
        byte[] first = read(o);
        Assert.Same(first, read(o));
        Assert.Same(first, read(sameFile));
        Assert.NotSame(first, read(otherFile));
        Assert.Equal(world.ObjectBytes(world.ObjectRefs.IndexOf(o)), first[(int)o.Start..(int)o.End]);
        Assert.Equal(world.ObjectBytes(world.ObjectRefs.IndexOf(otherFile)), read(otherFile)[(int)otherFile.Start..(int)otherFile.End]);
    }

    [Fact]
    public void ObjectBytesCutFromAReaderAreTheSavedOnes()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        Func<ObjectRef, byte[]> read = world.SourceReader();
        Assert.All(Enumerable.Range(0, world.ObjectRefs.Count), id => Assert.Equal(world.ObjectBytes(id), world.ObjectBytes(id, read)));
    }

    [Fact]
    public void SaveNumbersAreReadOneWay()
    {
        // The writer's next number read "+7" as 7 where the loader skipped it: the two could disagree.
        string dir = Path.Combine(Path.GetTempPath(), "vwe-savenumbers-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            foreach (string f in new[] { "_main.2.chunks", "_main.2.ok", "_main.3.chunks", "_main.+7.chunks", "_main.x.chunks" })
            {
                File.WriteAllBytes(Path.Combine(dir, f), []);
            }
            Assert.Equal((3, 2), WorldSave.SaveNumbers(dir));
            Assert.Equal(2, WorldSave.CommittedSave(dir));
            Assert.Equal(3, WorldWriter.LatestNumber(dir));
            File.Delete(Path.Combine(dir, "_main.2.ok"));
            Assert.Equal(3, WorldSave.CommittedSave(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
