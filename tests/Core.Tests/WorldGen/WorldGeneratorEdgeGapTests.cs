using ValheimGen;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The world's edges: the wave the edge circles ride on (the sine of twenty times the angle), and the gap the ground
/// takes at the Ashlands' and the Deep North's edge (0 on the edge, 1 from 400 m past it, eased in between).
/// </summary>
public class WorldGeneratorEdgeGapTests
{
    [Fact]
    public void WorldAngleIsTheSineOfTwentyTimesTheAngleFromTheZAxis()
    {
        // An angle of pi/40 makes twenty times it pi/2, whose sine is 1.
        float x = (float)Math.Sin(Math.PI / 40), z = (float)Math.Cos(Math.PI / 40);
        Assert.Equal(1.0, WorldGenerator.WorldAngle(x, z), 4);
    }

    [Fact]
    public void WorldAngleOnTheZAxisIsZero()
    {
        Assert.Equal(0f, WorldGenerator.WorldAngle(0f, 1f), 4);
    }

    [Fact]
    public void TheDeepNorthGapIsZeroOnItsEdge()
    {
        // The point is 12 000 m (the edge radius) from the circle's middle, where the wave is 0.
        Assert.Equal(0.0, WorldGenerator.CreateDeepNorthGap(0f, 8000f), 6);
    }

    [Fact]
    public void TheDeepNorthGapIsOneFrom400MPastItsEdge()
    {
        Assert.Equal(1.0, WorldGenerator.CreateDeepNorthGap(0f, 8400f), 6);
    }

    [Fact]
    public void TheDeepNorthGapIsTheSameShortOfAndPastTheEdgeByTheSameDistance()
    {
        double short200 = WorldGenerator.CreateDeepNorthGap(0f, 7800f);
        double past200 = WorldGenerator.CreateDeepNorthGap(0f, 8200f);
        Assert.Equal(past200, short200, 9);
        Assert.True(past200 is > 0.0 and < 1.0);
    }

    [Fact]
    public void TheAshlandsGapIsZeroOnItsEdge()
    {
        // The point is 12 000 m (the edge radius) from the Ashlands' circle's middle, where the wave is 0.
        double gap = PrivateMember.Call<double>(typeof(WorldGenerator), "AshlandsGap", null, 0f, -8000f);
        Assert.Equal(0.0, gap, 6);
    }

    [Fact]
    public void TheAshlandsGapIsOneFrom400MPastItsEdge()
    {
        double gap = PrivateMember.Call<double>(typeof(WorldGenerator), "AshlandsGap", null, 0f, -8400f);
        Assert.Equal(1.0, gap, 6);
    }
}
