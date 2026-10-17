using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using Xunit;

using static WorldEditor.Tests.WorldWriterTestHelpers;

namespace WorldEditor.Tests;

/// <summary>
/// Zone resets in a save: the ground and objects a reset clears, what a reset keeps, and its message count.
/// </summary>
public class WorldWriterResetTests
{
    [Fact]
    public void TheMessageCountsOnlyWhatTheResetCleared()
    {
        // The message took the deleted ids' count from the reset's: an id in the reset zone, a repeated id or one past
        // the list made "objects cleared" too low, or negative.
        using var w = new TempWorld();
        var world = w.Load();
        var t = world.TerrainZones[0];
        int[] inZone = [.. Enumerable.Range(0, world.ObjectRefs.Count).Where(i => world.ObjectRefs[i].Zone == (t.ZoneX, t.ZoneZ) && world.ObjectRefs[i].Prefab != WorldWriter.TombstonePrefab)];
        Assert.True(inZone.Length > 1);

        var r = WorldWriter.Save(world, [], deleted: [inZone[0], inZone[0], world.ObjectRefs.Count + 5], resets: [new ZoneReset(t.ZoneX, t.ZoneZ, false, true)]);

        Assert.True(r.Saved, r.Message);
        Assert.Contains("1 deleted object(s)", r.Message);
        Assert.Contains($"({inZone.Length - 1} objects cleared)", r.Message);
    }

    /// <summary>
    /// Saves the test world with ground raised in its first zone and that zone reset (its ground, its objects, or both):
    /// the zone and the save's result.
    /// </summary>
    private static (TerrainZone Zone, WorldWriter.Result Result) SaveRaisedZoneWithReset(TempWorld w, bool ground, bool objects)
    {
        WorldSave world = w.Load();
        var t = world.TerrainZones[0];
        var r = WorldWriter.Save(world, [Raised(t.ZoneX, t.ZoneZ, 3, 500)], resets: [new ZoneReset(t.ZoneX, t.ZoneZ, ground, objects)]);
        return (t, r);
    }

    [Fact]
    public void AGroundResetDropsThatZonesGroundEdits()
    {
        using var w = new TempWorld();
        var (t, r) = SaveRaisedZoneWithReset(w, ground: true, objects: true);
        Assert.True(r.Saved, r.Message);
        Assert.Equal(0, r.ZonesWritten);
        var again = WorldSave.Load(w.Dir);
        Assert.DoesNotContain(again.TerrainZones, q => q.ZoneX == t.ZoneX && q.ZoneZ == t.ZoneZ);
        Assert.DoesNotContain(((short)t.ZoneX, (short)t.ZoneZ), again.Zones!.Generated);
    }

    [Fact]
    public void AResetThatKeepsTheGroundKeepsItsEdits()
    {
        using var w = new TempWorld();
        var (t, r) = SaveRaisedZoneWithReset(w, ground: true, objects: false);
        Assert.True(r.Saved, r.Message);
        Assert.Equal(1, r.ZonesWritten);
        var again = WorldSave.Load(w.Dir);
        Assert.Equal(3f, again.TerrainZones.Single(q => q.ZoneX == t.ZoneX && q.ZoneZ == t.ZoneZ).LevelDelta[500], 4);
    }

    [Fact]
    public void ZonesCannotBeResetWithoutTheWorldsZoneList()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        typeof(WorldSave).GetProperty(nameof(WorldSave.Zones))!.SetValue(world, null);
        var before = Files(w.Dir);
        var r = WorldWriter.Save(world, [], resets: [new ZoneReset(0, 0, true, false)]);
        Assert.False(r.Saved);
        Assert.Contains(".db2", r.Message);
        AssertSame(before, w.Dir);
    }

    [Fact]
    public void GroundObjectsAndResetsInOneSave()
    {
        using var w = new TempWorld();
        WorldSave world = w.Load();
        var t = world.TerrainZones[0];
        var (fx, fz) = FreshZone(world);
        var trees = world.Objects.Where(o => o.Prefab == Fixtures.Hash("Beech1")).Take(2).ToList();
        var planted = new NewObject(-1, Fixtures.Hash("Beech1"), trees[0].Position + new Vector3(2, 0, 2), new Vector3(0, 45, 0), 1.2f);
        // A reset far from the rest, in a generated zone without buildings.
        var resetZone = world.Zones!.Generated.Select(g => ((int)g.X, (int)g.Z)).First(g => Math.Abs(g.Item1 - t.ZoneX) > 3 && world.ObjectRefs.All(o => o.Zone != g || !o.IsPiece));
        int inReset = world.ObjectRefs.Count(o => o.Zone == resetZone && !o.IsTerrain && o.Prefab != WorldWriter.TombstonePrefab);
        var r = WorldWriter.Save(world, [Raised(t.ZoneX, t.ZoneZ, 1, 600), Raised(fx, fz, -2, 700)], [trees[1].Id], [planted],
            [new ZoneReset(resetZone.Item1, resetZone.Item2, true, false)]);
        Assert.True(r.Saved, r.Message);
        Assert.Equal(1, r.ZonesWritten);
        Assert.Equal(1, r.ZonesCreated);
        Assert.Equal(1, r.ObjectsAdded);
        Assert.Equal(1 + inReset, r.ObjectsDeleted);
        var again = WorldSave.Load(w.Dir);
        Assert.Equal(world.ObjectCount + 1 + 1 - 1 - inReset, again.ObjectCount);
        Assert.Contains(again.Objects, o => o.Prefab == planted.Prefab && Vector3.Distance(o.Position, planted.Position) < 0.01f);
        Assert.DoesNotContain(again.Objects, o => Vector3.Distance(o.Position, trees[1].Position) < 0.001f && o.Prefab == trees[1].Prefab);
        Assert.Equal(-2f, again.TerrainZones.Single(q => q.ZoneX == fx && q.ZoneZ == fz).LevelDelta[700], 4);
    }
}
