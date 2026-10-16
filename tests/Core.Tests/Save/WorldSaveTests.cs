using System.IO.Compression;
using System.Numerics;
using System.Runtime.CompilerServices;
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
    public void ADamagedTerrainObjectIsReadAsAnOrdinaryObject()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        ZdoLocation source = world.TerrainZones[0].Source!;
        string path = Path.Combine(w.Dir, source.File.FileName);
        byte[] bytes = File.ReadAllBytes(path);
        // The first byte of the gzip data, after the value's length.
        bytes[(int)source.DataStart + 4] ^= 0xFF;
        File.WriteAllBytes(path, bytes);
        var damaged = WorldSave.Load(w.Dir);
        Assert.Empty(damaged.TerrainZones);
        Assert.Equal(world.ObjectCount, damaged.ObjectCount);
        Assert.Contains(damaged.ObjectRefs, o => o.Start == source.Start && o.File.FileName == source.File.FileName && !o.IsTerrain);
    }

    // Valid gzip whose ground data stops one byte short: reading runs past its end.
    [Fact]
    public void ATruncatedTerrainObjectIsReadAsAnOrdinaryObject()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        ZdoLocation source = world.TerrainZones[0].Source!;
        string path = Path.Combine(w.Dir, source.File.FileName);
        byte[] bytes = File.ReadAllBytes(path);
        byte[] raw = Gunzip(bytes[((int)source.DataStart + 4)..(int)source.DataEnd]);
        byte[] truncated = Gzip(raw[..^1]);
        using var replaced = new MemoryStream();
        replaced.Write(bytes, 0, (int)source.DataStart);
        replaced.Write(BitConverter.GetBytes(truncated.Length));
        replaced.Write(truncated);
        replaced.Write(bytes, (int)source.DataEnd, bytes.Length - (int)source.DataEnd);
        File.WriteAllBytes(path, replaced.ToArray());
        var damaged = WorldSave.Load(w.Dir);
        Assert.Empty(damaged.TerrainZones);
        Assert.Equal(world.ObjectCount, damaged.ObjectCount);
        Assert.Contains(damaged.ObjectRefs, o => o.Start == source.Start && o.File.FileName == source.File.FileName && !o.IsTerrain);
    }

    // Ground data claiming far more points than a terrain grid has is refused before its arrays are made.
    [Fact]
    public void TerrainDataWithTooManyPointsIsRefused()
    {
        const int Points = 257 * 257 + 1;
        using var raw = new MemoryStream();
        using (var bw = new BinaryWriter(raw, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            bw.Write(1);            // version
            bw.Write(0);            // operations
            bw.Write(0f);           // last operation position
            bw.Write(0f);
            bw.Write(0f);
            bw.Write(0f);           // last operation radius
            bw.Write(Points);       // height points, none modified
            bw.Write(new byte[Points]);
            bw.Write(0);            // paint points
        }
        var ex = Assert.Throws<InvalidDataException>(() => TerrainZone.Decode(Vector3.Zero, 0, Gzip(raw.ToArray())));
        Assert.Contains("height points", ex.Message);
    }

    /// <summary>
    /// Unpacks gzip data.
    /// </summary>
    private static byte[] Gunzip(byte[] compressed)
    {
        using var gzip = new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>
    /// Packs data as gzip.
    /// </summary>
    private static byte[] Gzip(byte[] raw)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(raw);
        }
        return output.ToArray();
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
    public void LiveBytesReadAgainAreLetGoOnceNoObjectIsInThem()
    {
        // A creature moving in the game is read again at every check: the bytes of each older read are let go once no
        // object of the world is in them any more, instead of being kept for the session.
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        int id = world.Objects.First(o => o.Prefab == Fixtures.Hash("Beech1")).Id;
        Vector3 at = world.ObjectRefs[id].Position;
        var firstRead = MergeMoved(world, id, at + new Vector3(1, 0, 0));
        MergeMoved(world, id, at + new Vector3(2, 0, 0));
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(firstRead.IsAlive);
        Assert.Equal(at + new Vector3(2, 0, 0), world.ObjectRefs[id].Position);
        var read = ZdoData.Parse(world.ObjectBytes(id), world.ObjectRefs[id].File.WorldVersion);
        Assert.Equal(at + new Vector3(2, 0, 0), read.Position);
    }

    /// <summary>
    /// The game sends the object's zone with the object moved to <paramref name="to"/>; the bytes it is read again
    /// from, held weakly (not inlined, so no reference to them is left on the caller's stack).
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference MergeMoved(WorldSave world, int id, Vector3 to)
    {
        var zone = world.ObjectRefs[id].Zone;
        var z = ZdoData.Parse(world.ObjectBytes(id), world.ObjectRefs[id].File.WorldVersion);
        z.Position = to;
        List<(long, uint, byte[])> objects = [.. Enumerable.Range(0, world.ObjectRefs.Count)
            .Where(i => world.ObjectRefs[i].Zone == zone && !world.Vanished.Contains(i))
            .Select(i => (world.ObjectRefs[i].LiveId.User, world.ObjectRefs[i].LiveId.Id, i == id ? z.Serialize() : world.ObjectBytes(i)))];
        var merged = world.MergeLive([zone], objects, _ => false, _ => false)!;
        Assert.Equal([id], merged.Updated);
        return new WeakReference(world.LiveSource(world.ObjectRefs[id].File));
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

    // A hold (the history written in the background after Apply live) keeps merges out until every hold is let go;
    // letting one go twice counts once.
    [Fact]
    public void LiveAMergeWaitsWhileTheWorldIsHeld()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        var first = world.Hold();
        var second = world.Hold();
        Assert.Null(world.MergeLive([(0, 0)], [], _ => false, _ => false));
        first.Dispose();
        first.Dispose();
        Assert.Null(world.MergeLive([(0, 0)], [], _ => false, _ => false));
        second.Dispose();
        Assert.NotNull(world.MergeLive([(0, 0)], [], _ => false, _ => false));
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

    [Fact]
    public void LiveAPlayersPieceAndAPlacedLocationComeAndGo()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        var at = new Vector3(640, 30, 640);
        // ZDOIDs of a user the snapshot has none of.
        // The piece is of a kind the world has none of (a mod's).
        List<(long, uint, byte[])> made =
        [
            (4242, 1, MadeObjects.Piece("mod_live_wall", at, 777, "Astrid")),
            (4242, 2, MadeObjects.Location(at + new Vector3(5, 0, 5), MadeObjects.MadeUpLocation)),
        ];
        int templates = world.TemplatesGeneration;
        Assert.False(world.Templates.ContainsKey(Fixtures.Hash("mod_live_wall")));
        var came = world.MergeLive([(10, 10)], made, _ => false, _ => false)!;
        Assert.Equal(2, came.Added.Count);
        // A kind the world had none of: its first object is its template, and the templates' generation moved on;
        // the same objects again do not move it.
        Assert.True(world.Templates.ContainsKey(Fixtures.Hash("mod_live_wall")));
        Assert.True(world.TemplatesGeneration > templates);
        templates = world.TemplatesGeneration;
        world.MergeLive([(10, 10)], made, _ => false, _ => false);
        Assert.Equal(templates, world.TemplatesGeneration);
        // A placed location shapes the ground as the game runs.
        Assert.True(came.Modifiers);
        Assert.Equal(1, world.Creators[777]);
        Assert.Equal(777, world.TopBuilder);
        Assert.Equal("Astrid", world.PlayerNames[777]);
        Assert.Contains(world.Pieces, p => p.Prefab == Fixtures.Hash("mod_live_wall"));
        Assert.Contains(world.Locations, l => l.Location == MadeObjects.MadeUpLocation);
        Assert.Contains(world.Placed, p => p.Location == MadeObjects.MadeUpLocation);
        // Gone from the game: out of every list, its builder's count down.
        var gone = world.MergeLive([(10, 10)], [], _ => false, _ => false)!;
        Assert.Equal(2, gone.Vanished.Count);
        Assert.True(gone.Modifiers);
        // A builder with no pieces left is no builder: kept at zero, TopBuilder could still name them.
        Assert.False(world.Creators.ContainsKey(777));
        Assert.NotEqual(777, world.TopBuilder);
        Assert.DoesNotContain(world.Pieces, p => p.Prefab == Fixtures.Hash("mod_live_wall"));
        Assert.DoesNotContain(world.Locations, l => l.Location == MadeObjects.MadeUpLocation);
        Assert.DoesNotContain(world.Placed, p => p.Location == MadeObjects.MadeUpLocation);
    }

    [Fact]
    public void LiveAZdoidListedTwiceIsOneObjectWithItsLastBytes()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        var at = new Vector3(640, 30, 640);
        var moved = at + new Vector3(1, 0, 0);
        // New to the editor: one id, not two (the first would never vanish).
        var came = world.MergeLive([(10, 10)],
            [(4242, 1, MadeObjects.Piece("woodwall", at, 777, "Astrid")), (4242, 1, MadeObjects.Piece("woodwall", moved, 777, "Astrid"))],
            _ => false, _ => false)!;
        int id = Assert.Single(came.Added);
        Assert.Equal(moved, world.ObjectRefs[id].Position);
        // Known: read again once, not twice.
        var changed = world.MergeLive([(10, 10)],
            [(4242, 1, MadeObjects.Piece("woodwall", at, 777, "Astrid")), (4242, 1, MadeObjects.Piece("woodwall", at, 777, "Astrid"))],
            _ => false, _ => false)!;
        Assert.Equal([id], changed.Updated);
        Assert.Single(world.Pieces, p => p.Id == id);
        Assert.Equal(1, world.Creators[777]);
        // Gone: the one object vanishes and nothing stays behind.
        var gone = world.MergeLive([(10, 10)], [], _ => false, _ => false)!;
        Assert.Equal([id], gone.Vanished);
        Assert.False(world.Creators.ContainsKey(777));
    }

    // A vehicle's root and part (the example mod's): their links read (the root's
    // whole turn too), read again when the game changes them, gone with them.
    [Fact]
    public void LiveLinksComeChangeAndGoWithTheirObjects()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        var at = new Vector3(640, 30, 640);
        byte[] root = MadeObjects.VehicleRoot("ValheimVehicles_WaterVehicleShip", at, new Vector3(10, 90, 0), 7);
        // The part also keeps the turn as older versions of the mod did.
        var part = ZdoData.Parse(MadeObjects.VehiclePart("wood_floor", at, 7, new Vector3(1, 2, 3), new Vector3(0, 45, 0), 8));
        part.QuatList.Add((StableHash.Of(ExampleMod.TurnQuatKey), new Quaternion(0.1f, 0.2f, 0.3f, 0.9f)));
        var came = world.MergeLive([(10, 10)], [(4242, 1, root), (4242, 2, part.Serialize())], _ => false, _ => false)!;
        var (link, rotation) = world.Links[came.Added[0]];
        Assert.Equal(7, link.Id);
        Assert.Equal(new Vector3(10, 90, 0), rotation);
        link = world.Links[came.Added[1]].Link;
        Assert.Equal((0, new Vector3(1, 2, 3), new Vector3(0, 45, 0)), (link.Id, link.Offset, link.Euler));
        Assert.Equal([8, 7], link.Parents);
        Assert.Equal(new Quaternion(0.1f, 0.2f, 0.3f, 0.9f), link.Quat);
        // The part taken off the vehicle in the game; then both gone.
        var changed = world.MergeLive([(10, 10)], [(4242, 1, root), (4242, 2, MadeObjects.Zdo("wood_floor", at))], _ => false, _ => false)!;
        Assert.Equal([came.Added[1]], changed.Updated);
        Assert.False(world.Links.ContainsKey(came.Added[1]));
        world.MergeLive([(10, 10)], [], _ => false, _ => false);
        Assert.Empty(world.Links);
    }

    [Fact]
    public void LiveANewObjectIsBuiltFromTheBytesOfAModelReadSinceTheSnapshot()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        var at = new Vector3(640, 30, 640);
        var came = world.MergeLive([(10, 10)], [(4242, 1, MadeObjects.Piece("woodwall", at, 777, "Astrid"))], _ => false, _ => false)!;
        int model = came.Added[0];
        ObjectRef o = world.ObjectRefs[model];
        // The model's offsets are in its own read, not in the snapshot.
        Assert.Equal(world.ObjectBytes(model), world.SourceReader()(o)[(int)o.Start..(int)o.End]);
        byte[]? copy = world.NewObjectBytes(new NewObject(-1, o.Prefab, at + new Vector3(2, 0, 0), Vector3.Zero, 0f, model), world.SourceReader());
        Assert.NotNull(copy);
        var z = ZdoData.Parse(copy);
        Assert.Equal((o.Prefab, at + new Vector3(2, 0, 0)), (z.Prefab, z.Position));
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
}
