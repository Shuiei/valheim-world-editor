using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The save reader and writer on the test world: what is saved must read back exactly, and nothing else may change.
/// </summary>
public class WorldSaveTests
{
    [Fact]
    public void LoadsTheTestWorld()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        Assert.Equal("CITest", world.Name);
        Assert.Equal("GbXfypqU3G", world.SeedName);
        Assert.Equal(2, world.SaveNumber);
        Assert.Equal(122, world.ObjectCount);
        Assert.Single(world.TerrainZones);
        Assert.True(world.Templates.ContainsKey(Fixtures.Hash("Beech1")));
        Assert.Contains(world.Objects, o => o.Prefab == Fixtures.Hash("piece_chest_wood"));
    }

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

    [Fact]
    public void ZoneResetRemovesGeneratedObjects()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var (id, prefab, position, rotation, scale) = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
        int zx = (int)MathF.Floor((position.X + 32f) / 64f), zz = (int)MathF.Floor((position.Z + 32f) / 64f);
        int inZone = world.Objects.Count(o => (int)MathF.Floor((o.Position.X + 32f) / 64f) == zx && (int)MathF.Floor((o.Position.Z + 32f) / 64f) == zz);
        Assert.True(inZone > 0);
        WorldWriter.Result r = WorldWriter.Save(world, [], resets: [new ZoneReset(zx, zz, KeepBuildings: true, Ground: true)]);
        Assert.True(r.Saved, r.Message);
        var again = WorldSave.Load(w.Dir);
        Assert.DoesNotContain(again.Objects, o => (int)MathF.Floor((o.Position.X + 32f) / 64f) == zx && (int)MathF.Floor((o.Position.Z + 32f) / 64f) == zz && o.Prefab == prefab);
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

    [Fact]
    public void AZoneResetKeepsPlayersTombstones()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var edits = new EditStore(world);
        var (id, prefab, position, rotation, scale) = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1"));
        edits.AddObjects([new NewObject(-1, Fixtures.Hash("Player_tombstone"), position + new Vector3(1, 0, 1), Vector3.Zero, 0f)]);
        Assert.True(WorldWriter.Save(world, [], edits.Deleted, edits.Added).Saved);
        var saved = WorldSave.Load(w.Dir);
        var (x, z) = saved.ObjectRefs.First(o => o.Prefab == Fixtures.Hash("Player_tombstone")).Zone;
        var r = WorldWriter.Save(saved, [], null, null, [new ZoneReset(x, z, true, false)]);
        Assert.True(r.Saved, r.Message);
        var after = WorldSave.Load(w.Dir);
        Assert.Contains(after.ObjectRefs, o => o.Prefab == Fixtures.Hash("Player_tombstone"));
        Assert.DoesNotContain(after.Objects, o => o.Prefab == Fixtures.Hash("Beech1") && (int)MathF.Floor((o.Position.X + 32) / 64) == x && (int)MathF.Floor((o.Position.Z + 32) / 64) == z);
    }

    [Fact]
    public void LiveAnObjectMadeAgainIsFollowedUnderItsNewZdoid()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        int id = Enumerable.Range(0, world.ObjectRefs.Count).First(i => !world.ObjectRefs[i].IsTerrain);
        var zone = world.ObjectRefs[id].Zone;
        var ids = Enumerable.Range(0, world.ObjectRefs.Count).Where(i => world.ObjectRefs[i].Zone == zone).ToList();
        List<(long, uint, byte[])> Zone() => [.. ids.Select(i => (world.ObjectRefs[i].LiveId.User, world.ObjectRefs[i].LiveId.Id, world.ObjectBytes(i)))];
        // The zone as the game has it: nothing new, nothing gone.
        var same = world.MergeLive([zone], Zone(), _ => false, _ => false)!;
        Assert.Empty(same.Added);
        Assert.Empty(same.Vanished);
        // The editor made it again in the game (an applied delete undone): the game sends it under its new ZDOID, and
        // it is the same object, not a new one.
        int count = world.ObjectRefs.Count;
        world.SetLiveId(id, (999, 7));
        Assert.Equal((999L, 7u), world.ObjectRefs[id].LiveId);
        var again = world.MergeLive([zone], Zone(), _ => false, _ => false)!;
        Assert.Empty(again.Added);
        Assert.Empty(again.Vanished);
        Assert.Equal(count, world.ObjectRefs.Count);
        // Gone from the game, then made again: its new ZDOID kept.
        world.MergeLive([zone], [.. Zone().Where(o => o.Item2 != 7u)], _ => false, _ => false);
        Assert.Contains(id, world.Vanished);
        world.SetLiveId(id, (5, 5));
        Assert.Equal((5L, 5u), world.ObjectRefs[id].LiveId);
    }

    [Fact]
    public async Task LiveAMergeWaitsWhileTheWorldsListsAreReadElsewhere()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        using var held = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var reader = Task.Run(() =>
        {
            lock (world.Sync)
            {
                held.Set();
                release.Wait(TestContext.Current.CancellationToken);
            }
        }, TestContext.Current.CancellationToken);
        held.Wait(TestContext.Current.CancellationToken);
        try
        {
            // Busy: no merge now (the caller tries again later).
            Assert.Null(world.MergeLive([(0, 0)], [], _ => false, _ => false));
        }
        finally
        {
            release.Set();
        }
        await reader;
        Assert.NotNull(world.MergeLive([(0, 0)], [], _ => false, _ => false));
    }

    [Fact]
    public void LiveAPlayersPieceAndAPlacedLocationComeAndGo()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        var at = new Vector3(640, 30, 640);
        // ZDOIDs of a user the snapshot has none of.
        List<(long, uint, byte[])> made =
        [
            (4242, 1, MadeObjects.Piece("woodwall", at, 777, "Astrid")),
            (4242, 2, MadeObjects.Location(at + new Vector3(5, 0, 5), MadeObjects.MadeUpLocation)),
        ];
        var came = world.MergeLive([(10, 10)], made, _ => false, _ => false)!;
        Assert.Equal(2, came.Added.Count);
        // A placed location shapes the ground as the game runs.
        Assert.True(came.Modifiers);
        Assert.Equal(1, world.Creators[777]);
        Assert.Equal(777, world.TopBuilder);
        Assert.Equal("Astrid", world.PlayerNames[777]);
        Assert.Contains(world.Pieces, p => p.Prefab == Fixtures.Hash("woodwall"));
        Assert.Contains(world.Locations, l => l.Location == MadeObjects.MadeUpLocation);
        Assert.Contains(world.Placed, p => p.Location == MadeObjects.MadeUpLocation);
        // Gone from the game: out of every list, its builder's count down.
        var gone = world.MergeLive([(10, 10)], [], _ => false, _ => false)!;
        Assert.Equal(2, gone.Vanished.Count);
        Assert.True(gone.Modifiers);
        Assert.Equal(0, world.Creators[777]);
        Assert.DoesNotContain(world.Pieces, p => p.Prefab == Fixtures.Hash("woodwall"));
        Assert.DoesNotContain(world.Locations, l => l.Location == MadeObjects.MadeUpLocation);
        Assert.DoesNotContain(world.Placed, p => p.Location == MadeObjects.MadeUpLocation);
    }
}
