using TerrainEditor.Terrain;
using Xunit;
using static WorldEditor.Tests.TerrainModifierTestWorld;

namespace WorldEditor.Tests;

/// <summary>
/// The game's runtime terrain modifiers (TerrainModifiers, Heightmap.ApplyModifiers) on a flat 30 m zone: the hoe's raise
/// and dig (round and square, the 8 m limit of what players do) and cultivating (smoothing kept within 1 m of the levelled
/// ground).
/// </summary>
public class TerrainModifiersTests
{
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
}
