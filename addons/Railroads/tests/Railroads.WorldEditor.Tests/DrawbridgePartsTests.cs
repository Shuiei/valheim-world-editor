using System.Numerics;
using Xunit;

namespace Railroads.WorldEditor.Tests;

/// <summary>
/// The drawbridge's moving parts laid out as the mod lays them out (Railroads 0.3.6): the leaves flat when lowered and
/// upright when raised; the chains always as long, so the weights hang lower as the leaves rise; the second end the first
/// turned half round; drawn raised while the mod says the leaves are up.
/// </summary>
public class DrawbridgePartsTests
{
    private static List<(string Model, Vector3 At, Quaternion Turn)> Parts(float raised) => [.. DrawbridgeParts.Parts(raised)];

    [Theory]
    [InlineData(0, 9.55f)]
    [InlineData(0.5f, 6.4989f)]
    [InlineData(1, 4.4527f)]
    public void TheWeightsHangLowerAsTheLeavesRise(float raised, float high)
    {
        var weights = Parts(raised).Where(p => p.Model == DrawbridgeParts.Weight).ToList();

        Assert.Equal(4, weights.Count);
        Assert.All(weights, w => Assert.Equal(high, w.At.Y, 3));
        // Below the sheaves' far side, 1.7 m before each hinge.
        Assert.Equal([-1.7f, -1.7f, 17.7f, 17.7f], weights.Select(w => w.At.Z).Order());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5f)]
    [InlineData(1)]
    public void EveryChainIsAsLongWhateverTheLeaves(float raised)
    {
        var parts = Parts(raised);

        Assert.Equal(4 * 58, parts.Count(p => p.Model == DrawbridgeParts.Link));
        Assert.Equal(2, parts.Count(p => p.Model == DrawbridgeParts.Leaf));
        Assert.Equal(4, parts.Count(p => p.Model == DrawbridgeParts.Sheave));
        Assert.Equal(4, parts.Count(p => p.Model == DrawbridgeParts.Pinion));
    }

    [Fact]
    public void TheLeavesLieFlatLoweredAndStandUpRaised()
    {
        var down = Parts(0).Where(p => p.Model == DrawbridgeParts.Leaf).ToList();
        var up = Parts(1).Where(p => p.Model == DrawbridgeParts.Leaf).ToList();

        // Each leaf from its hinge towards the middle: the first at the start, the second at the far end.
        Assert.Equal([new Vector3(0, 0.34f, 0), new Vector3(0, 0.34f, DrawbridgeParts.Length)], down.Select(l => l.At));
        Assert.Equal([Vector3.UnitZ, -Vector3.UnitZ], down.Select(l => Round(Vector3.Transform(Vector3.UnitZ, l.Turn))));
        Assert.Equal([Vector3.UnitY, Vector3.UnitY], up.Select(l => Round(Vector3.Transform(Vector3.UnitZ, l.Turn))));
    }

    [Fact]
    public void AChainRunsFromItsLeafOverItsSheave()
    {
        var links = Parts(0).Where(p => p.Model == DrawbridgeParts.Link).Take(58).ToList();

        // The first link at the chain's eye, 4 m along the leaf; the chain rises to the sheave and hangs down past it.
        Assert.True(Vector3.Distance(links[0].At, new Vector3(-1.42f, 0.25f, 4)) < 0.15f, links[0].At.ToString());
        Assert.True(links.Max(l => l.At.Y) is > 10.4f and < 10.6f);
        Assert.Equal(-1.7f, links[^1].At.Z, 3);
        Assert.Equal(-Vector3.UnitY, Round(Vector3.Transform(Vector3.UnitZ, links[^1].Turn)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void TheSecondEndIsTheFirstTurnedHalfRound(float raised)
    {
        var parts = Parts(raised);
        var first = parts.Where(p => p.At.Z < DrawbridgeParts.Length / 2).ToList();
        var second = parts.Where(p => p.At.Z >= DrawbridgeParts.Length / 2).ToList();

        Assert.Equal(first.Count, second.Count);
        foreach (var (model, at, turn) in first)
        {
            var mirrored = new Vector3(-at.X, at.Y, DrawbridgeParts.Length - at.Z);
            Assert.Contains(second, p => p.Model == model && Vector3.Distance(p.At, mirrored) < 1e-4f
                && Vector3.Distance(Vector3.Transform(Vector3.UnitZ, p.Turn), Vector3.Transform(Vector3.UnitZ, turn) * new Vector3(-1, 1, -1)) < 1e-4f);
        }
    }

    [Fact]
    public void ItIsDrawnRaisedWhileTheModSaysItsLeavesAreUp()
    {
        var bridge = new FakeObject(RailroadsMod.Drawbridge);
        Assert.False(DrawbridgeParts.IsUp(bridge));

        bridge.Ints[RailroadsMod.Keys.BridgeUp] = 1;
        Assert.True(DrawbridgeParts.IsUp(bridge));

        bridge.Ints[RailroadsMod.Keys.BridgeUp] = 0;
        Assert.False(DrawbridgeParts.IsUp(bridge));
    }

    /// <summary>A direction rounded to 4 places, to compare.</summary>
    private static Vector3 Round(Vector3 v) => new((float)Math.Round(v.X, 4) + 0f, (float)Math.Round(v.Y, 4) + 0f, (float)Math.Round(v.Z, 4) + 0f);
}
