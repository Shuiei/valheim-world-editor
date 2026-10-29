using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.WorldWriterTestHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// The world writer's refusals: what it refuses (and then leaves every file alone), a failure halfway (the backup comes back
/// byte for byte).
/// </summary>
public class WorldWriterEdgeTests
{
    /// <summary>
    /// How many folders sit next to the world's folder (the backups, with the world itself).
    /// </summary>
    private static int Backups(string worldDir) => Directory.GetDirectories(Path.GetDirectoryName(worldDir)!).Length;

    [Fact]
    public void NothingToSaveTouchesNothing()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var before = Files(w.Dir);
        int backups = Backups(w.Dir);
        var r = WorldWriter.Save(world, []);
        Assert.False(r.Saved);
        Assert.Equal("Nothing to save.", r.Message);
        Assert.Null(r.BackupDirectory);
        Assert.Equal(backups, Backups(w.Dir));
        AssertSame(before, w.Dir);
    }

    [Fact]
    public void AnEmptyEditOfAZoneWithoutGroundIsNotSaved()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var (x, z) = FreshZone(world);
        var before = Files(w.Dir);
        var r = WorldWriter.Save(world, [new ZoneEdit(x, z)]);
        Assert.False(r.Saved);
        Assert.Equal("Nothing could be saved.", r.Message);
        AssertSame(before, w.Dir);
    }

    [Fact]
    public void NewObjectsThatCannotBeSavedAreSkippedWithAReason()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var before = Files(w.Dir);
        var (id, prefab, position, rotation, scale) = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
        var unknown = new NewObject(-1, Fixtures.Hash("NoSuchThing_xyz"), position, Vector3.Zero, 1f);
        var far = new NewObject(-2, Fixtures.Hash("Beech1"), new Vector3(9000, 0, 9000), Vector3.Zero, 1f);
        var r = WorldWriter.Save(world, [], added: [unknown, far]);
        Assert.False(r.Saved);
        Assert.Contains(r.Skipped, s => s.Contains("unknown kind of object"));
        Assert.Contains(r.Skipped, s => s.Contains("is not generated yet"));
        AssertSame(before, w.Dir);
    }

    [Fact]
    public void DeletingAnIdThatDoesNotExistIsIgnored()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var before = Files(w.Dir);
        var r = WorldWriter.Save(world, [], deleted: [-5, world.ObjectRefs.Count + 10]);
        Assert.False(r.Saved);
        AssertSame(before, w.Dir);
    }

    [Fact]
    public void AFailureHalfwayRestoresTheWorldByteForByte()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        // The world's metadata file disappears after loading: copying it fails once the chunks are written.
        File.Delete(Path.Combine(w.Dir, $"_main.{world.SaveNumber}.fwl2"));
        var before = Files(w.Dir);
        var t = world.TerrainZones[0];
        var r = WorldWriter.Save(world, [Raised(t.ZoneX, t.ZoneZ, 1, 500)]);
        Assert.False(r.Saved);
        Assert.StartsWith("Saving failed", r.Message);
        Assert.Contains("Nothing changed", r.Message);
        Assert.Null(r.BackupDirectory);
        AssertSame(before, w.Dir);
    }
}
