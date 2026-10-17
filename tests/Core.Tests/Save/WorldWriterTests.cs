using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.WorldWriterTestHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// The world writer's ground: a zone the game never edited gets its ground, a new ground needs a terrain object to copy
/// (and reads it once), a terrain object in another layout is copied past, an unreadable template is a result, and an edited
/// zone whose terrain objects are deleted gets a new one.
/// </summary>
public class WorldWriterTests
{
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
    public void GroundInAZoneTheGameNeverEditedIsCreated()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var (x, z) = FreshZone(world);
        var edit = Raised(x, z, 2.5f, 100, 101, 2000);
        edit.PaintModified[300] = true;
        edit.Paint[300 * 4] = 1f;
        var r = WorldWriter.Save(world, [edit]);
        Assert.True(r.Saved, r.Message);
        Assert.Equal(0, r.ZonesWritten);
        Assert.Equal(1, r.ZonesCreated);
        var again = WorldSave.Load(w.Dir);
        Assert.Equal(world.ObjectCount + 1, again.ObjectCount);
        var t = again.TerrainZones.Single(q => q.ZoneX == x && q.ZoneZ == z);
        Assert.True(t.ModifiedHeight[100] && t.ModifiedHeight[2000]);
        Assert.Equal(2.5f, t.LevelDelta[2000], 4);
        Assert.False(t.ModifiedHeight[102]);
        Assert.True(t.ModifiedPaint[300]);
        Assert.Equal(1f, t.Paint[300].X, 3);
        // The zone that had ground before is as it was.
        var old = world.TerrainZones[0];
        Assert.Equal(old.LevelDelta, again.TerrainZones.Single(q => q.ZoneX == old.ZoneX && q.ZoneZ == old.ZoneZ).LevelDelta);
    }

    [Fact]
    public void AnEditedZoneWhoseTerrainObjectsAreDeletedGetsANewOne()
    {
        // The deleted objects were patched (and left out, removal winning) while the zone counted as written: the
        // read-back check found the zone missing and refused the save.
        using var w = new TempWorld();
        var world = w.Load();
        var t = world.TerrainZones[0];
        int[] terrain = [.. Enumerable.Range(0, world.ObjectRefs.Count).Where(i => world.ObjectRefs[i].IsTerrain && world.ObjectRefs[i].Zone == (t.ZoneX, t.ZoneZ))];
        Assert.NotEmpty(terrain);

        var r = WorldWriter.Save(world, [Raised(t.ZoneX, t.ZoneZ, 1.5f, 500)], deleted: terrain);

        Assert.True(r.Saved, r.Message);
        Assert.Equal(1.5f, SavedZoneLevels.LevelDelta(w.Dir, t, 500), 4);
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

    // New ground in several zones copies the template object's own bytes, read once, not its whole chunk file per zone;
    // a save without new ground does not read it.
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
        before = WorldWriter.TemplateReads;
        Assert.True(WorldWriter.Save(saved, [Raised(zones[0].X, zones[0].Z, 2f, 100)]).Saved);
        Assert.Equal(before, WorldWriter.TemplateReads);
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
}
