using System.Numerics;
using System.Runtime.CompilerServices;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Live merges of the game's objects into a world loaded live: objects keep their identity, bytes read again are let go,
/// new objects are built from what the game has, and links to vehicle roots and parts come and go with the objects.
/// </summary>
public class WorldSaveLiveMergeTests
{
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

    // The editor's own objects (those it made in the game) are not the world's: they are given back as the game has
    // them, for the editor's copies to take, and never come in as new objects.
    [Fact]
    public void LiveTheEditorsOwnObjectsAreGivenBackAsTheGameHasThem()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        var at = new Vector3(640, 30, 640);
        byte[] moved = MadeObjects.Zdo("Deer", at + new Vector3(0.5f, 0, 0), z => z.FloatList.Add((StableHash.Of("max_health"), 10f)));
        int count = world.ObjectRefs.Count;

        var merged = world.MergeLive([(10, 10)], [(4242, 1, moved), (4242, 2, MadeObjects.Zdo("woodwall", at))], _ => false, zdo => zdo == (4242, 1))!;

        var (user, id, bytes) = Assert.Single(merged.Made);
        Assert.Equal((4242L, 1u), (user, id));
        Assert.Equal(moved, bytes);
        Assert.Single(merged.Added);
        Assert.Empty(merged.Updated);
        Assert.Equal(count + 1, world.ObjectRefs.Count);
    }

    // The kinds that move on their own are read alone when only they changed: their zone is not among those read whole,
    // so what the read leaves out does not vanish, while what it brings comes in or changes.
    [Fact]
    public void LiveObjectsOfAZoneNotReadWholeDoNotVanish()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        var at = new Vector3(640, 30, 640);
        var came = world.MergeLive([(10, 10)], [(4242, 1, MadeObjects.Zdo("woodwall", at)), (4242, 2, MadeObjects.Zdo("Deer", at))], _ => false, _ => false)!;
        int wall = came.Added[0], deer = came.Added[1];

        var walked = world.MergeLive([], [(4242, 2, MadeObjects.Zdo("Deer", at + new Vector3(3, 0, 0))), (4242, 3, MadeObjects.Zdo("Boar", at))], _ => false, _ => false)!;

        Assert.Empty(walked.Vanished);
        Assert.Equal([deer], walked.Updated);
        Assert.Single(walked.Added);
        Assert.Equal(at + new Vector3(3, 0, 0), world.ObjectRefs[deer].Position);
        Assert.DoesNotContain(wall, world.Vanished);
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
}
