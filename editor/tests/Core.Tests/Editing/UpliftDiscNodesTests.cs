using System.Numerics;
using TerrainEditor.Editing;
using Xunit;
using static WorldEditor.Tests.UpliftBlockFixture;

namespace WorldEditor.Tests;

/// <summary>
/// The disc nodes of a block: a node is placed on the metre grid within a disc's reach of an open point, once, at the
/// height the point asks for, and the nodes' heights can be set; the heights are fitted and measured against the ground
/// asked for.
/// </summary>
public class UpliftDiscNodesTests
{
    [Fact]
    public void ANodeIsPlacedOnEachGridNodeWithinTheReachOfAnOpenPoint()
    {
        var block = SingleZone();
        var nodes = new Uplift.DiscNodes(block, Targets(block, 5f), Flags(block, true));

        nodes.AddNear(Middle);

        Assert.Equal(5, nodes.Count);
        Assert.All(nodes.Discs(), d => Assert.Equal(5f, d.Y));
    }

    [Fact]
    public void AnAddedNodeIsNotAddedTwice()
    {
        var block = SingleZone();
        var nodes = new Uplift.DiscNodes(block, Targets(block, 5f), Flags(block, true));
        nodes.AddNear(Middle);

        nodes.AddNear(Middle);

        Assert.Equal(5, nodes.Count);
    }

    [Fact]
    public void ASetHeightReplacesTheNodesHeight()
    {
        var block = SingleZone();
        var nodes = new Uplift.DiscNodes(block, Targets(block, 5f), Flags(block, true));
        nodes.AddNear(Middle);

        nodes.Set(0, 0, 2f);

        Assert.Equal(2f, nodes.Discs().Single(d => d.X == 0 && d.Z == 0).Y);
    }

    [Fact]
    public void NothingIsAddedWhereTheGroundIsAlreadyAsAsked()
    {
        var block = SingleZone();
        var nodes = new Uplift.DiscNodes(block, Targets(block, 5f), Flags(block, true));

        nodes.AddWhereMissing([.. Enumerable.Repeat(5f, block.N)]);

        Assert.Equal(0, nodes.Count);
    }

    [Fact]
    public void NodesAreAddedWhereTheGroundIsMoreThanTheNeedOff()
    {
        var block = SingleZone();
        var nodes = new Uplift.DiscNodes(block, Targets(block, 10f), Flags(block, true));

        nodes.AddWhereMissing(new float[block.N]);

        Assert.True(nodes.Count > 0);
    }

    [Fact]
    public void MissCountsOneForEachPointOneMetreOff()
    {
        var block = SingleZone();
        var targets = Targets(block, 1f);
        var heights = new Uplift.DiscHeights(block, targets, Flags(block, true), new Uplift.DiscNodes(block, targets, Flags(block, true)));

        Assert.Equal(block.Points.Length, heights.Miss(new float[block.N]), 3);
    }

    [Fact]
    public void MissIsZeroWhereNoPointIsEditedOrLifted()
    {
        var block = SingleZone();
        var targets = Targets(block, 1f);
        var heights = new Uplift.DiscHeights(block, targets, Flags(block, false), new Uplift.DiscNodes(block, targets, Flags(block, false)));

        Assert.Equal(0.0, heights.Miss(new float[block.N]));
    }

    [Fact]
    public void WantedRangeIsTheLowestAndHighestGroundAskedWithinTheDisc()
    {
        var block = SingleZone();
        float[] want = [.. Enumerable.Repeat(2f, block.N)];
        want[Middle] = 7f;
        var targets = Targets(block, 0f, want);
        var heights = new Uplift.DiscHeights(block, targets, Flags(block, false), new Uplift.DiscNodes(block, targets, Flags(block, false)));

        Assert.Equal((2f, 7f), heights.WantedRange(Vector3.Zero));
    }

    [Fact]
    public void AFitMovesADiscToTheOnlyHeightAskedWithinItsReach()
    {
        var block = SingleZone();
        var targets = Targets(block, 2f);
        var nodes = new Uplift.DiscNodes(block, targets, Flags(block, true));
        var heights = new Uplift.DiscHeights(block, targets, Flags(block, true), nodes);

        heights.Fit([new Vector3(0, 0, 0)], new float[block.N]);

        Assert.Equal(2f, nodes.Discs().Single().Y);
    }
}
