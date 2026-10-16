using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The world writer's remaining edges: a world with no terrain object to copy for new ground, a chunk file name already
/// taken, terrain templates in an unexpected layout, a template that cannot be read, and a value that cannot read back
/// the same (the read-back check restores the backup).
/// </summary>
public class WorldWriterTests
{
    /// <summary>
    /// Every file of the folder, by name.
    /// </summary>
    private static Dictionary<string, byte[]> Files(string dir) => Directory.GetFiles(dir).ToDictionary(f => Path.GetFileName(f), File.ReadAllBytes);

    /// <summary>
    /// Asserts the folder holds the same files with the same bytes as before.
    /// </summary>
    private static void AssertSame(Dictionary<string, byte[]> before, string dir)
    {
        var after = Files(dir);
        Assert.Equal(before.Keys.Order(), after.Keys.Order());
        foreach (var (name, bytes) in before)
        {
            Assert.True(bytes.AsSpan().SequenceEqual(after[name]), $"{name} changed");
        }
    }

    /// <summary>
    /// A zone that has world data (a chunk file) but no ground edits yet.
    /// </summary>
    private static (int X, int Z) FreshZone(WorldSave world) => FreshZones(world).First();

    /// <summary>
    /// Zones that have world data (a chunk file) but no ground edits yet, nearest the first edited zone first.
    /// </summary>
    private static IEnumerable<(int X, int Z)> FreshZones(WorldSave world)
    {
        var t = world.TerrainZones[0];
        for (int r = 1; r < 6; r++)
        {
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = t.ZoneX + dx, z = t.ZoneZ + dz;
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) == r && ChunkMath.Find(world.Chunks, x, z) != null && world.TerrainZones.All(q => q.ZoneX != x || q.ZoneZ != z))
                    {
                        yield return (x, z);
                    }
                }
            }
        }
    }

    /// <summary>
    /// A ground edit of this zone raising one point by this many metres.
    /// </summary>
    private static ZoneEdit Raised(int zx, int zz, float by, int point)
    {
        var e = new ZoneEdit(zx, zz);
        e.Modified[point] = true;
        e.Level[point] = by;
        return e;
    }

    [Fact]
    public void NewGroundNeedsATerrainObjectToCopy()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var (x, z) = FreshZone(world);
        // As in a world nobody has dug in yet.
        world.TerrainZones.Clear();
        var before = Files(w.Dir);
        var r = WorldWriter.Save(world, [Raised(x, z, 1, 100)]);
        Assert.False(r.Saved);
        Assert.Contains(r.Skipped, s => s.Contains("no terrain object to copy yet"));
        AssertSame(before, w.Dir);
    }

    [Fact]
    public void ATakenChunkFileNameIsSkipped()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var t = world.TerrainZones[0];
        var file = t.Source!.File;
        // Something already uses the next version's name (a crashed save, say): the writer goes past it.
        string taken = Path.Combine(w.Dir, ChunkFile.NameFor(file.Chunk, file.Size, file.Version + 1));
        File.WriteAllText(taken, "not ours");
        var r = WorldWriter.Save(world, [Raised(t.ZoneX, t.ZoneZ, 1.5f, 500)]);
        Assert.True(r.Saved, r.Message);
        Assert.True(File.Exists(Path.Combine(w.Dir, ChunkFile.NameFor(file.Chunk, file.Size, file.Version + 2))));
        Assert.Equal("not ours", File.ReadAllText(taken));
        Assert.Equal(1.5f, WorldSave.Load(w.Dir).TerrainZones.Single(q => q.ZoneX == t.ZoneX && q.ZoneZ == t.ZoneZ).LevelDelta[500], 4);
    }

    [Fact]
    public void TerrainTemplatesInAnUnexpectedLayoutAreRefusedBeforeWriting()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var (x, z) = FreshZone(world);
        foreach (var t in world.TerrainZones.Where(t => t.Source != null))
        {
            t.Source = t.Source! with { Flags = (ushort)(t.Source.Flags & ~0x2000) };
        }
        var before = Files(w.Dir);
        var r = WorldWriter.Save(world, [Raised(x, z, 1, 100)]);
        Assert.False(r.Saved);
        Assert.Contains(r.Skipped, s => s.Contains("unexpected layout"));
        AssertSame(before, w.Dir);
    }

    // New ground in several zones copies the template object's own bytes, read once, not its whole chunk file per zone.
    [Fact]
    public void NewGroundReadsTheTemplateOnce()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var zones = FreshZones(world).Take(3).ToList();
        Assert.Equal(3, zones.Count);
        int before = WorldWriter.TemplateReads;
        var r = WorldWriter.Save(world, [.. zones.Select(q => Raised(q.X, q.Z, 1.5f, 100))]);
        Assert.True(r.Saved, r.Message);
        Assert.Equal(1, WorldWriter.TemplateReads - before);
        var saved = WorldSave.Load(w.Dir);
        Assert.All(zones, q => Assert.Equal(1.5f, saved.TerrainZones.Single(t => t.ZoneX == q.X && t.ZoneZ == q.Z).LevelDelta[100], 4));
    }

    // A terrain object in another layout ahead of a usable one: the usable one is copied. The fixture has one terrain
    // object, so a byte copy of it is saved first; then the first of the two is marked as in another layout.
    [Fact]
    public void NewGroundCopiesATerrainObjectInTheShortLayout()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var t = world.TerrainZones[0];
        int id = world.ObjectRefs.FindIndex(o => o.IsTerrain && o.File == t.Source!.File && o.Start == t.Source.Start);
        Assert.True(id >= 0);
        var copy = new NewObject(-1, t.Prefab, t.Center, System.Numerics.Vector3.Zero, 0f, Raw: world.ObjectBytes(id));
        var first = WorldWriter.Save(world, [], added: [copy]);
        Assert.True(first.Saved, first.Message);
        world = w.Load();
        Assert.Equal(2, world.TerrainZones.Count(q => q.Source != null));
        var (x, z) = FreshZone(world);
        var other = world.TerrainZones.First(q => q.Source != null);
        other.Source = other.Source! with { Flags = (ushort)(other.Source.Flags & ~0x2000) };
        var r = WorldWriter.Save(world, [Raised(x, z, 1.5f, 100)]);
        Assert.True(r.Saved, r.Message);
        Assert.Equal(1.5f, WorldSave.Load(w.Dir).TerrainZones.Single(q => q.ZoneX == x && q.ZoneZ == z).LevelDelta[100], 4);
    }

    // The template's chunk file cannot be read (gone since the world was read): a result saying so, nothing written.
    [Fact]
    public void ATemplateThatCannotBeReadIsAResult()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var (x, z) = FreshZone(world);
        File.Delete(Path.Combine(w.Dir, world.TerrainZones.First(t => t.Source != null).Source!.File.FileName));
        var before = Files(w.Dir);
        var r = WorldWriter.Save(world, [Raised(x, z, 1, 100)]);
        Assert.False(r.Saved);
        Assert.StartsWith("Saving failed", r.Message);
        AssertSame(before, w.Dir);
    }

    [Fact]
    public void AValueThatDoesNotReadBackTheSameRestoresTheBackup()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var t = world.TerrainZones[0];
        var e = new EditStore(world).Get(t.ZoneX, t.ZoneZ)!.Clone();
        // Not a number never equals itself: the read-back check finds the zone different.
        e.Modified[500] = true;
        e.Level[500] = float.NaN;
        var before = Files(w.Dir);
        var r = WorldWriter.Save(world, [e]);
        Assert.False(r.Saved);
        Assert.StartsWith("The saved world did not read back correctly (zone", r.Message);
        Assert.Contains("differs at point 500", r.Message);
        Assert.Contains("Nothing changed", r.Message);
        AssertSame(before, w.Dir);
    }

    [Fact]
    public void PaintThatDoesNotReadBackTheSameRestoresTheBackup()
    {
        using var w = new TempWorld();
        var world = w.Load();
        var t = world.TerrainZones[0];
        var e = new EditStore(world).Get(t.ZoneX, t.ZoneZ)!.Clone();
        e.PaintModified[600] = true;
        e.Paint[600 * 4] = float.NaN;
        var before = Files(w.Dir);
        var r = WorldWriter.Save(world, [e]);
        Assert.False(r.Saved);
        Assert.Contains("paint differs at point 600", r.Message);
        AssertSame(before, w.Dir);
    }
}
