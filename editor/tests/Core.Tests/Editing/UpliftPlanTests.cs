using System.Numerics;
using TerrainEditor.Editing;
using Xunit;
using static WorldEditor.Tests.UpliftWorld;

namespace WorldEditor.Tests;

/// <summary>
/// The plan of an uplift (No limit ground): the lifts in zones the game has not generated are dropped, the zones planned
/// are the ones near the lifts, and two lifts far apart are planned as each alone.
/// </summary>
[Collection("World files")]
public class UpliftPlanTests
{
    [Fact]
    public void LiftInZonesTheGameHasNotGeneratedIsDropped()
    {
        using var w = new TempWorld();
        var o = Of(w.Load());
        // On the edge of the saved zones: zone 15 is generated, 16 is not.
        const float Cx = 15 * 64 + 32, Cz = 300;
        o.Lift(Cx, Cz, Bell(Cx, Cz, 20, 40));
        var plan = Uplift.Make(o.World, o.Terrain, o.Edits, o.NextId)!;
        Assert.True(plan.Ungenerated > 0, plan.Describe().English);
        Assert.Contains(plan.Zones, e => e.ZoneX >= 16);
        Assert.All(plan.Zones.Where(e => e.ZoneX >= 16), e => Assert.False(e.HasLift));
        Assert.Contains("not generated yet", plan.Describe().English);
    }

    [Fact]
    public void AChainOfHillsIsPlannedOnTheZonesNearThem()
    {
        // Three hills along a diagonal of the test world, three zones apart: one group, worked only near the hills.
        using var w = new TempWorld();
        var o = Of(w.Load());
        var hills = new[] { (X: 2 * 64f, Z: 2 * 64f), (X: 5 * 64f, Z: 5 * 64f), (X: 8 * 64f, Z: 8 * 64f) };
        foreach (var (x, z) in hills)
        {
            o.Lift(x, z, Bell(x, z, 20, 40));
        }
        var lifted = o.Edits.All().Where(e => e.HasLift).Select(e => (e.ZoneX, e.ZoneZ)).ToList();
        Assert.Single(Uplift.Clusters(lifted));
        var near = Uplift.WorkingZones(lifted).ToHashSet();
        var plan = Uplift.Make(o.World, o.Terrain, o.Edits, o.NextId)!;
        Assert.All(plan.Zones, e => Assert.Contains((e.ZoneX, e.ZoneZ), near));
        // Every hill gets its discs.
        Assert.All(hills, h => Assert.Contains(plan.Add, n => Vector2.Distance(new(n.Position.X, n.Position.Z), new(h.X, h.Z)) < 40));
    }

    [Fact]
    public void TwoLiftsFarApartArePlannedAsEachAlone()
    {
        using var w = new TempWorld();
        var (a, b) = ((X: 40f, Z: 30f), (X: 13 * 64f, Z: 13 * 64f));
        var both = Of(w.Load());
        both.Lift(a.X, a.Z, Bell(a.X, a.Z, 20, 40));
        both.Lift(b.X, b.Z, Bell(b.X, b.Z, 20, 40));
        var plan = Uplift.Make(both.World, both.Terrain, both.Edits, both.NextId)!;
        var alone = new List<Uplift.Plan>();
        foreach (var (x, z) in new[] { a, b })
        {
            var o = Of(w.Load());
            o.Lift(x, z, Bell(x, z, 20, 40));
            alone.Add(Uplift.Make(o.World, o.Terrain, o.Edits, o.NextId)!);
        }
        Assert.All(alone, p => Assert.True(p.Discs > 0, p.Describe().English));
        Assert.Equal(alone.Sum(p => p.Discs), plan.Discs);
        Assert.Equal(alone.SelectMany(p => p.Add).Select(n => n.Position).Order(Comparer<Vector3>.Create((u, v) => (u.X, u.Z).CompareTo((v.X, v.Z)))),
            plan.Add.Select(n => n.Position).Order(Comparer<Vector3>.Create((u, v) => (u.X, u.Z).CompareTo((v.X, v.Z)))));
        Assert.Equal(alone.Sum(p => p.Zones.Count), plan.Zones.Count);
        Assert.Equal(plan.Zones.Count, plan.Zones.Select(e => (e.ZoneX, e.ZoneZ)).Distinct().Count());
        Assert.Equal(alone.Max(p => p.Worst), plan.Worst);
    }

    [Fact]
    public void AGroupStillAsItsDiscsWereWorkedOutIsNotPlannedAgain()
    {
        using var w = new TempWorld();
        var (a, b) = ((X: 40f, Z: 30f), (X: 13 * 64f, Z: 13 * 64f));
        var both = Of(w.Load());
        both.Lift(a.X, a.Z, Bell(a.X, a.Z, 20, 40));
        both.Lift(b.X, b.Z, Bell(b.X, b.Z, 20, 40));
        var near = Uplift.WorkingZones([((int)MathF.Floor((b.X + 32) / 64), (int)MathF.Floor((b.Z + 32) / 64))]).ToHashSet();
        var plan = Uplift.Make(both.World, both.Terrain, both.Edits, both.NextId, asPlanned: (x, z) => near.Contains((x, z)))!;
        Assert.True(plan.Discs > 0, plan.Describe().English);
        Assert.All(plan.Add, n => Assert.True(Vector2.Distance(new(n.Position.X, n.Position.Z), new(a.X, a.Z)) < 100));
        Assert.DoesNotContain(plan.Zones, e => near.Contains((e.ZoneX, e.ZoneZ)));
        // Both groups as planned: nothing to do.
        Assert.Null(Uplift.Make(both.World, both.Terrain, both.Edits, both.NextId, asPlanned: (_, _) => true));
    }

    [Fact]
    public void TreesAlreadyOnTheGroundAskedForStay()
    {
        // The hill of UpliftSaveTests, over the edge of the test world's trees: saved, the tree under it goes.
        using var w = new TempWorld();
        const float Cx = 40, Cz = 30;
        var o = Of(w.Load());
        o.Lift(Cx, Cz, Bell(Cx, Cz, 30, 60));
        Assert.True(Uplift.Make(o.World, o.Terrain, o.Edits, o.NextId)!.Cleared > 0);
        // The world's objects already standing on that ground (the lift only makes up for discs under it): none goes.
        var stay = Uplift.Make(o.World, o.Terrain, o.Edits, o.NextId, standing: o.Edits.Get)!;
        Assert.Equal(0, stay.Cleared);
    }
}
