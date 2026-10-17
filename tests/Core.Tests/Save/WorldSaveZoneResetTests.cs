using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Zone resets of the test world: generated objects go, and players' tombstones stay.
/// </summary>
public class WorldSaveZoneResetTests
{
    [Fact]
    public void ZoneResetRemovesGeneratedObjects()
    {
        using var w = new TempWorld();
        var (world, (id, prefab, position, rotation, scale)) = TestWorldTree.Open(w);
        int zx = (int)MathF.Floor((position.X + 32f) / 64f), zz = (int)MathF.Floor((position.Z + 32f) / 64f);
        int inZone = world.Objects.Count(o => (int)MathF.Floor((o.Position.X + 32f) / 64f) == zx && (int)MathF.Floor((o.Position.Z + 32f) / 64f) == zz);
        Assert.True(inZone > 0);
        WorldWriter.Result r = WorldWriter.Save(world, [], resets: [new ZoneReset(zx, zz, KeepBuildings: true, Ground: true)]);
        Assert.True(r.Saved, r.Message);
        var again = WorldSave.Load(w.Dir);
        Assert.DoesNotContain(again.Objects, o => (int)MathF.Floor((o.Position.X + 32f) / 64f) == zx && (int)MathF.Floor((o.Position.Z + 32f) / 64f) == zz && o.Prefab == prefab);
    }

    [Fact]
    public void AZoneResetKeepsPlayersTombstones()
    {
        using var w = new TempWorld();
        var (world, (id, prefab, position, rotation, scale)) = TestWorldTree.Open(w);
        var edits = new EditStore(world);
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
}
