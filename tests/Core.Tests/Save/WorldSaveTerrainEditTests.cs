using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Terrain edits saved to the test world: what is raised and painted reads back, the ground stays within the game's limit,
/// and zones the game has not generated are skipped.
/// </summary>
public class WorldSaveTerrainEditTests
{
    [Fact]
    public void TerrainEditsRoundTrip()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        TerrainZone zone = world.TerrainZones[0];
        ZoneEdit edit = edits.Get(zone.ZoneX, zone.ZoneZ)!.Clone();
        // Raise a block of points by 1.5 m and paint one paved.
        var cells = Enumerable.Range(0, EditStore.Cells).Where(i => i % 65 > 20 && i % 65 < 30 && i / 65 > 20 && i / 65 < 30).ToList();
        foreach (int i in cells)
        {
            edit.Modified[i] = true;
            edit.Level[i] += 1.5f;
        }
        edit.PaintModified[cells[0]] = true;
        edit.Paint[cells[0] * 4 + 2] = 1f;
        edits.Put(edit);
        WorldWriter.Result r = WorldWriter.Save(world, [.. edits.All().Where(e => e.Changed)]);
        Assert.True(r.Saved, r.Message);
        Assert.Equal(1, r.ZonesWritten);
        // No backup folder: the old save goes only once the new one has read back right.
        Assert.Null(r.BackupDirectory);
        Assert.Empty(Directory.GetDirectories(Path.GetDirectoryName(w.Dir)!, "*_backup_*"));

        var again = WorldSave.Load(w.Dir);
        Assert.Equal(3, again.SaveNumber);
        Assert.Equal(world.ObjectCount, again.ObjectCount);
        TerrainZone z2 = again.TerrainZones.Single(t => t.ZoneX == zone.ZoneX && t.ZoneZ == zone.ZoneZ);
        foreach (int i in cells)
        {
            Assert.True(z2.ModifiedHeight[i]);
            Assert.Equal(edit.Level[i], z2.LevelDelta[i], 3);
        }
        Assert.Equal(1f, z2.Paint[cells[0]].Z, 2);
    }

    [Fact]
    public void GroundIsClampedToTheGameLimit()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        TerrainZone zone = world.TerrainZones[0];
        ZoneEdit edit = edits.Get(zone.ZoneX, zone.ZoneZ)!.Clone();
        edit.Modified[100] = true;
        edit.Level[100] = 50f;
        edits.Put(edit);
        Assert.InRange(edits.Get(zone.ZoneX, zone.ZoneZ)!.Level[100], -8f, 8f);
    }

    [Fact]
    public void ZonesThatAreNotGeneratedAreSkipped()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        var edit = new ZoneEdit(60, 60);
        edit.Modified[5] = true;
        edit.Level[5] = 1f;
        edits.Put(edit);
        WorldWriter.Result r = WorldWriter.Save(world, [.. edits.All().Where(e => e.Changed)]);
        Assert.False(r.Saved);
        Assert.Contains(r.Skipped, s => s.Contains("not generated"));
        Assert.Equal(2, WorldSave.Load(w.Dir).SaveNumber);
    }
}
