using System.Numerics;
using TerrainEditor.Editing;
using TerrainEditor.Save;
using TerrainEditor.Terrain;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The game's runtime terrain modifiers (TerrainModifiers, Heightmap.ApplyModifiers) on a flat 30 m zone: the hoe's
/// raise and dig (round and square, the 8 m limit of what players do), cultivating (smoothing kept within 1 m of the
/// levelled ground), a location's flattening and its rotation, its cleared paint, which zones a modifier reaches, the
/// game's order, and what does not count as a modifier.
/// </summary>
public class TerrainModifiersTests
{
    private const int N = 65;

    /// <summary>
    /// A world holding only these placed objects.
    /// </summary>
    private static WorldSave World(params PlacedObject[] placed)
    {
        var w = new WorldSave { Directory = "none", SaveNumber = 1 };
        w.Placed.AddRange(placed);
        return w;
    }

    /// <summary>
    /// A network object of this prefab at x, y, z, made at this time.
    /// </summary>
    private static PlacedObject Net(string prefab, float x, float y, float z, long time = 0) => new(StableHash.Of(prefab), 0, new Vector3(x, y, z), Vector3.Zero, time);

    /// <summary>
    /// A location at x, y, z, turned by this angle.
    /// </summary>
    private static PlacedObject Loc(string location, float x, float y, float z, float turn = 0) => new(0, StableHash.Of(location), new Vector3(x, y, z), new Vector3(0, turn, 0), 0);

    /// <summary>
    /// A zone's 65 × 65 heights, all 30 m.
    /// </summary>
    private static float[] Flat() => [.. Enumerable.Repeat(30f, N * N)];

    /// <summary>
    /// The vertex of zone 0, 0 at world x, z (the zone spans -32..32).
    /// </summary>
    private static float At(float[] h, int x, int z) => h[(z + 32) * N + x + 32];

    /// <summary>
    /// A zone's 65 × 65 paint mask, every vertex this RGBA.
    /// </summary>
    private static float[] Mask(float r, float g, float b, float a) => [.. Enumerable.Range(0, N * N).SelectMany(_ => new[] { r, g, b, a })];

    /// <summary>
    /// The paint of the vertex of zone 0, 0 at world x, z.
    /// </summary>
    private static float[] PaintAt(float[] mask, int x, int z) => mask[(((z + 32) * N + x + 32) * 4)..(((z + 32) * N + x + 32) * 4 + 4)];

    [Fact]
    public void TheHoesRaiseLevelsARoundSpotAndKeepsTheGroundBefore()
    {
        var m = new TerrainModifiers(World(Net("raise", 0, 31, 0)));
        Assert.Equal(1, m.Count);
        float[] h = Flat();
        float[] before = m.Apply(0, 0, h);
        // Raise: half a metre above where it was placed, within 1.5 m (a round area).
        Assert.Equal(31.5f, At(h, 0, 0));
        Assert.Equal(31.5f, At(h, 1, 1));
        Assert.Equal(30f, At(h, 2, 0));
        Assert.Equal(30f, At(h, 2, 2));
        // The saved edits are clamped against the ground before the players' modifiers.
        Assert.All(before, v => Assert.Equal(30f, v));
    }

    [Fact]
    public void WhatPlayersDoStaysWithinEightMetres()
    {
        var m = new TerrainModifiers(World(Net("raise", 0, 60, 0), Net("digg", 10, 0, 10, time: 5)));
        float[] h = Flat();
        m.Apply(0, 0, h);
        Assert.Equal(38f, At(h, 0, 0));
        Assert.Equal(22f, At(h, 10, 10));
    }

    [Fact]
    public void TheNewDigIsSquare()
    {
        var m = new TerrainModifiers(World(Net("digg_v2", 0, 30, 0)));
        float[] h = Flat();
        m.Apply(0, 0, h);
        // One metre down over a square of 3 × 3 vertices, corners included.
        Assert.Equal(29f, At(h, 1, 1));
        Assert.Equal(29f, At(h, -1, 1));
        Assert.Equal(30f, At(h, 2, 0));
    }

    [Fact]
    public void CultivatingSmoothsWithinAMetreOfTheLevelledGround()
    {
        var m = new TerrainModifiers(World(Net("cultivate", 0, 35, 0)));
        float[] h = Flat();
        m.Apply(0, 0, h);
        // It pulls towards 35 m but a player's smoothing stays within 1 m of the ground under it.
        Assert.Equal(31f, At(h, 0, 0));
        Assert.InRange(At(h, 2, 0), 30f, 31f);
        Assert.Equal(30f, At(h, 4, 0));
    }

    [Fact]
    public void ALocationSmoothsItsGroundTowardsItsHeight()
    {
        // The goblin hut smooths the ground towards its height over 7 m (the game's cubic falloff);
        // it does not level.
        var m = new TerrainModifiers(World(Loc("GoblinHut02", 0, 33, 0)));
        Assert.Equal(1, m.LocationsWithModifiers);
        float[] h = Flat();
        float[] before = m.Apply(0, 0, h);
        Assert.Equal(33f, At(h, 0, 0));
        float t = 1f - MathF.Pow(3f / 7f, 3f);
        Assert.Equal(30f + 3f * t, At(h, 3, 0), 4);
        // Beyond 7 m (the corner at 5, 5 is 7.07 m away): untouched.
        Assert.Equal(30f, At(h, 5, 5));
        Assert.Equal(30f, At(h, 20, 20));
        // No player modifier: the edits are clamped against the smoothed ground itself.
        Assert.Equal(33f, At(before, 0, 0));
    }

    [Fact]
    public void ALocationThatLevelsSetsItsHeightInARoundArea()
    {
        // The statue group levels a 1.5 m circle 1 m above its height, smoothing out to 4 m.
        var m = new TerrainModifiers(World(Loc("Mistlands_StatueGroup1", 0, 32, 0)));
        float[] h = Flat();
        m.Apply(0, 0, h);
        Assert.Equal(33f, At(h, 0, 0));
        Assert.Equal(33f, At(h, 1, 1));
        Assert.InRange(At(h, 2, 0), 30f, 33f);
    }

    [Fact]
    public void ALocationsModifiersTurnWithIt()
    {
        // The goblin hut's second modifier sits 4.1 m to its west; turned 90°, it is to the north
        // or south instead. The heights there differ from the unturned hut's.
        float[] straight = Flat();
        new TerrainModifiers(World(Loc("GoblinHut02", 0, 33, 0))).Apply(0, 0, straight);
        float[] turned = Flat();
        new TerrainModifiers(World(Loc("GoblinHut02", 0, 33, 0, turn: 90))).Apply(0, 0, turned);
        Assert.NotEqual(straight, turned);
        // The middle is levelled the same.
        Assert.Equal(At(straight, 0, 0), At(turned, 0, 0));
    }

    [Fact]
    public void ALocationPaintsItsClearedGroundWithinItsRadius()
    {
        // Eikthyr's altar paints dirt over 4.14 m, fading out as the game does; the vegetation (alpha) is kept.
        var m = new TerrainModifiers(World(Loc("Eikthyrnir", 0, 30, 0)));
        float[] h = Flat(), mask = Mask(0f, 0.5f, 0f, 0.4f);
        m.Apply(0, 0, h, mask);
        Assert.Equal([1f, 0f, 0f, 0.4f], PaintAt(mask, 0, 0));
        float t = MathF.Pow(1f - 2f / 4.14f, 0.1f);
        float[] two = PaintAt(mask, 2, 0);
        Assert.Equal(t, two[0], 4);
        Assert.Equal(0.5f * (1f - t), two[1], 4);
        Assert.Equal(0f, two[2]);
        Assert.Equal(0.4f, two[3]);
        // Beyond its radius (5 m, and the corner at 3, 3 is 4.24 m away): untouched.
        Assert.Equal([0f, 0.5f, 0f, 0.4f], PaintAt(mask, 5, 0));
        Assert.Equal([0f, 0.5f, 0f, 0.4f], PaintAt(mask, 3, 3));
        // Without a mask, only the heights.
        float[] alone = Flat();
        m.Apply(0, 0, alone);
        Assert.Equal(h, alone);
    }

    [Fact]
    public void ClearingVegetationTakesEveryChannelToNothing()
    {
        // The second place of mystery clears 20 m: paint and vegetation both go.
        var m = new TerrainModifiers(World(Loc("PlaceofMystery2", 0, 30, 0)));
        float[] mask = Mask(0.2f, 0.5f, 0.1f, 1f);
        m.Apply(0, 0, Flat(), mask);
        Assert.Equal([0f, 0f, 0f, 0f], PaintAt(mask, 0, 0));
        float keep = 1f - MathF.Pow(0.5f, 0.1f);
        float[] ten = PaintAt(mask, 10, 0);
        Assert.Equal(0.2f * keep, ten[0], 4);
        Assert.Equal(0.5f * keep, ten[1], 4);
        Assert.Equal(0.1f * keep, ten[2], 4);
        Assert.Equal(keep, ten[3], 4);
        Assert.Equal([0.2f, 0.5f, 0.1f, 1f], PaintAt(mask, 21, 0));
    }

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
