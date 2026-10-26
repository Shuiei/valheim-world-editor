using System.Numerics;
using ValheimWorldEditor.Sdk;
using Xunit;

namespace ValheimRAFT.WorldEditor.Tests;

public sealed class RopeLaddersTests
{
    private static FakeObject Ladder(float height) => new(Keys.RopeLadder, new Vector3(10, height, 10));

    [Fact]
    public void ALadderOverDeepWaterHangsTwoMetresIntoIt()
    {
        Assert.Equal(5, RopeLadders.Length(Ladder(33), new FakeGround(10)));
        // Without the ground (outside the area): the water all the same.
        Assert.Equal(5, RopeLadders.Length(Ladder(33), new FakeGround(null)));
        Assert.Equal(5, RopeLadders.Length(Ladder(33), new FakeWorld()));
    }

    [Fact]
    public void ALadderStopsAtTheGround()
    {
        // Land, or shallow water: the ground comes first.
        Assert.Equal(2, RopeLadders.Length(Ladder(33), new FakeGround(31)));
        Assert.Equal(3.5f, RopeLadders.Length(Ladder(33), new FakeGround(29.5f)), 3);
    }

    [Fact]
    public void ALadderIsFortyMetresAtMostAndNoneUnderWater()
    {
        Assert.Equal(40, RopeLadders.Length(Ladder(120), new FakeGround(31)));
        Assert.Equal(0, RopeLadders.Length(Ladder(20), new FakeGround(10)));
        Assert.Empty(new RopeLadders().ShapesOf(Ladder(20), new FakeGround(10)));
    }

    [Fact]
    public void ALadderHasTwoRopesAndARungEveryHalfMetre()
    {
        var shapes = new RopeLadders().ShapesOf(Ladder(33), new FakeGround(10)).ToList();

        // 5 m: ten rungs, and the two ropes down to the last.
        Assert.Equal(12, shapes.Count);
        Assert.All(shapes, s => Assert.False(s.WorldSpace));
        Assert.Equal(-5, shapes.Take(2).SelectMany(s => s.Vertices).Min(v => v.Y), 3);
        // The ropes 0.8 m apart, across the ladder.
        Assert.Equal(0.8f, shapes[0].Vertices.Average(v => v.X) - shapes[1].Vertices.Average(v => v.X), 3);
    }

    [Fact]
    public void ALadderIsShapedInItsOwnFrame()
    {
        Shape rope = new RopeLadders().ShapesOf(Ladder(33), new FakeGround(10)).First();
        Assert.All(rope.Vertices, v => Assert.InRange(v.Y, -5.1f, 0.1f));
    }
}
