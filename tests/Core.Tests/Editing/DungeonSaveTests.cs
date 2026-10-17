using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Dungeons found in a save: the dungeon objects of a live world with their room lists read back, and a dungeon read in
/// the files of a world version the save does not hold.
/// </summary>
public class DungeonSaveTests
{
    [Fact]
    public void TheDungeonsOfASaveAreFoundWithTheirRoomCounts()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        Assert.Empty(Dungeons.In(world));
        var at = new Vector3(640, 30, 640);
        List<Dungeons.Placed> rooms =
        [
            new(StableHash.Of("cave_new_entrance02"), at + new Vector3(0, 5000, 0), Quaternion.Identity),
            new(StableHash.Of("cave_new_endcap02"), at + new Vector3(0, 5000, 8), Quaternion.Identity),
        ];
        world.MergeLive([(10, 10)],
        [
            (4242, 1, MadeObjects.Dungeon("DG_Cave", at, rooms)),
            // A room list too short to hold its count: no rooms read, the dungeon still found.
            (4242, 2, MadeObjects.Dungeon("DG_Cave", at + new Vector3(10, 0, 0), [1, 2])),
            // Not a dungeon.
            (4242, 3, MadeObjects.Zdo("Beech1", at + new Vector3(20, 0, 0))),
        ], _ => false, _ => false);
        var found = Dungeons.In(world).OrderBy(f => f.Position.X).ToList();
        Assert.Equal(2, found.Count);
        Assert.All(found, f => Assert.Equal("DG_Cave", f.Kind.Name));
        Assert.Equal(at, found[0].Position);
        Assert.Equal(2, found[0].Rooms);
        Assert.Equal(0, found[1].Rooms);
    }

    [Fact]
    public void ADungeonIsReadInItsFilesWorldVersion()
    {
        var world = WorldSave.LoadLive(File.ReadAllBytes(Fixtures.Snapshot), "live");
        // Objects read from now on are of version 39: each carries its sector after its flags.
        int loaded = world.Chunks[0].WorldVersion;
        world.Chunks[0].WorldVersion = 39;
        var at = new Vector3(640, 30, 640);
        List<Dungeons.Placed> rooms =
        [
            new(StableHash.Of("cave_new_entrance02"), at + new Vector3(0, 5000, 0), Quaternion.Identity),
            new(StableHash.Of("cave_new_endcap02"), at + new Vector3(0, 5000, 8), Quaternion.Identity),
        ];
        byte[] current = MadeObjects.Dungeon("DG_Cave", at, rooms);
        world.MergeLive([(10, 10)], [(4242, 1, [.. current[..2], 0, 0, 0, 0, .. current[2..]])], _ => false, _ => false);
        // Back to the loaded version, so only the object's own file says 39.
        world.Chunks[0].WorldVersion = loaded;
        var found = Assert.Single(Dungeons.In(world));
        Assert.NotEqual(39, loaded);
        Assert.Equal(39, world.ObjectRefs[found.Id].File.WorldVersion);
        Assert.Equal(2, found.Rooms);
    }
}
