using TerrainEditor.Terrain;
using Xunit;
using static WorldEditor.Tests.TerrainModifierTestWorld;

namespace WorldEditor.Tests;

/// <summary>
/// The game's runtime terrain modifiers (TerrainModifiers) of a location on a flat 30 m zone: its smoothing, levelling,
/// turning, cleared paint and the clearing of vegetation, and the ground discs of uplift that keep their dirt.
/// </summary>
public class TerrainModifiersLocationTests
{
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
}
