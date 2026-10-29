using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;
using static WorldEditor.Tests.TerrainModifierTestWorld;

namespace WorldEditor.Tests;

/// <summary>
/// The game's runtime terrain modifiers (TerrainModifiers) on a flat 30 m zone: which zones a modifier reaches, the game's
/// order, the paint's height check, what does not count as a modifier, and the radius of a modifier.
/// </summary>
public class TerrainModifiersRulesTests
{
    [Fact]
    public void ThePaintsHeightCheckLeavesTheGroundAboveIt()
    {
        // The coast rock's two dirt patches paint only ground no higher than themselves: the first sits 0.36 m up
        // (the 30 m ground is painted), the second 0.25 m down (it is not).
        var m = new TerrainModifiers(World(Net("rock4_coast", 0, 30, 0)));
        float[] mask = Mask(0f, 0f, 0f, 1f);
        m.Apply(0, 0, Flat(), mask);
        Assert.Equal([1f, 0f, 0f, 1f], PaintAt(mask, -1, 0));
        Assert.Equal([0f, 0f, 0f, 1f], PaintAt(mask, 10, -1));
        // Sunk a metre, neither paints.
        float[] sunk = Mask(0f, 0f, 0f, 1f);
        new TerrainModifiers(World(Net("rock4_coast", 0, 29, 0))).Apply(0, 0, Flat(), sunk);
        Assert.Equal(Mask(0f, 0f, 0f, 1f), sunk);
    }

    [Fact]
    public void AGroundDiscsDirtIsLeftOut()
    {
        // Uplift paints the ground under its discs back with saved paint, so their dirt is never seen.
        var m = new TerrainModifiers(World(Loc(Uplift.DiscName, 0, 30, 0)));
        Assert.True(m.InZone(0, 0)[0].PaintCleared);
        float[] mask = Mask(0f, 0f, 0f, 1f);
        m.Apply(0, 0, Flat(), mask);
        Assert.Equal(Mask(0f, 0f, 0f, 1f), mask);
    }

    [Fact]
    public void AModifierNearABorderReachesTheZonesItTouches()
    {
        var m = new TerrainModifiers(World(Net("raise", 31, 31, 31)));
        Assert.Single(m.InZone(0, 0));
        Assert.Single(m.InZone(1, 1));
        Assert.Single(m.InZone(1, 0));
        Assert.Empty(m.InZone(2, 2));
        Assert.Empty(m.InZone(-1, -1));
        // In the next zone, its vertices are levelled too (the zone shares that edge).
        float[] h = Flat();
        m.Apply(1, 1, h);
        Assert.Equal(31.5f, h[0]);
    }

    [Fact]
    public void PlayersModifiersComeLastThenByOrderTimeAndDistance()
    {
        var m = new TerrainModifiers(World(Net("raise", 0, 31, 0, time: 9), Loc("GoblinHut02", 0, 33, 0), Net("digg", 0, 31, 0, time: 2)));
        var list = m.InZone(0, 0);
        // The location's two modifiers (order 0, then 1), then the players' by time.
        Assert.False(list[0].Player);
        Assert.False(list[1].Player);
        Assert.True(list[0].SortOrder <= list[1].SortOrder);
        Assert.True(list[2].Player && list[2].LevelOffset < 0);
        Assert.True(list[3].Player && list[3].LevelOffset > 0);
    }

    [Fact]
    public void ObjectsThatAreNotModifiersAreLeftOut()
    {
        var m = new TerrainModifiers(World(
            Net("Beech1", 0, 30, 0),
            // A location's name placed as an object, and an object's name as a location: neither counts.
            Net("GoblinHut02", 0, 30, 0),
            Loc("raise", 0, 30, 0)));
        Assert.Equal(0, m.Count);
        Assert.Equal(0, m.LocationsWithModifiers);
        float[] h = Flat();
        Assert.All(m.Apply(0, 0, h), v => Assert.Equal(30f, v));
        Assert.All(h, v => Assert.Equal(30f, v));
    }

    [Fact]
    public void TheRadiusIsTheLargestOfWhatTheModifierDoes()
    {
        var m = new TerrainModifiers.Modifier(Vector3.Zero, true, 0, 2, false, true, 5, 3, true, 7, TerrainModifiers.Paint.Dirt, 1, false, 0, false, 0);
        Assert.Equal(7, m.Radius);
        Assert.Equal(5, (m with { PaintCleared = false }).Radius);
        Assert.Equal(2, (m with { PaintCleared = false, Smooth = false }).Radius);
        Assert.Equal(0, (m with { PaintCleared = false, Smooth = false, Level = false }).Radius);
        // The network prefabs the save reader needs to know about include the hoe's.
        Assert.Contains(StableHash.Of("raise"), TerrainModifiers.NetworkPrefabHashes);
        Assert.DoesNotContain(StableHash.Of("GoblinHut02"), TerrainModifiers.NetworkPrefabHashes);
    }
}
