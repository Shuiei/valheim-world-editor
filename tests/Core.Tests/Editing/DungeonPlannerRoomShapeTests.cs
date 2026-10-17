using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The shape a room is drawn with (DungeonPlanner.ShapeFor): a room under six tiles either way is a rectangle, and a
/// kind's chance of a notched or apsed shape is the one its roll falls under, with a T only for a store-like room eight
/// tiles across or more.
/// </summary>
public class DungeonPlannerRoomShapeTests
{
    /// <summary>A random source whose every draw is the same value, so the roll a shape is taken on is known.</summary>
    private sealed class FixedRoll(double roll) : Random
    {
        /// <summary>The value every draw gives.</summary>
        public override double NextDouble() => roll;
    }

    private static DungeonPlan.Shape ShapeFor(DungeonKit.Room room, int across, int deep, double roll)
    {
        var p = new DungeonPlannerProbe(new DungeonGen.Settings(Size: 2, Levels: 1, Seed: 1), DungeonKit.StyleOf("Crypt"), new FixedRoll(roll));
        return (DungeonPlan.Shape)p.Call("ShapeFor", room, across, deep)!;
    }

    [Fact]
    public void ARoomUnderSixTilesAcrossIsARectangleWhateverTheRoll()
    {
        Assert.Equal(DungeonPlan.Shape.Rect, ShapeFor(DungeonKit.Room.Hall, 5, 9, 0.0));
    }

    [Fact]
    public void AHallIsApsedWhenTheRollIsUnderThirtyFivePercent()
    {
        Assert.Equal(DungeonPlan.Shape.Apse, ShapeFor(DungeonKit.Room.Hall, 8, 8, 0.2));
        Assert.Equal(DungeonPlan.Shape.Rect, ShapeFor(DungeonKit.Room.Hall, 8, 8, 0.9));
    }

    [Fact]
    public void AChapelIsApsedBelowFortyFivePercentAndCrossedBelowSixty()
    {
        Assert.Equal(DungeonPlan.Shape.Apse, ShapeFor(DungeonKit.Room.Chapel, 8, 8, 0.2));
        Assert.Equal(DungeonPlan.Shape.Cross, ShapeFor(DungeonKit.Room.Chapel, 8, 8, 0.5));
        Assert.Equal(DungeonPlan.Shape.Rect, ShapeFor(DungeonKit.Room.Chapel, 8, 8, 0.7));
    }

    [Fact]
    public void AStoreRoomIsATOnlyWhenEightTilesAcross()
    {
        Assert.Equal(DungeonPlan.Shape.T, ShapeFor(DungeonKit.Room.Store, 8, 6, 0.35));
        Assert.Equal(DungeonPlan.Shape.Rect, ShapeFor(DungeonKit.Room.Store, 6, 6, 0.35));
    }

    [Fact]
    public void AStairwellStaysWholeWhateverTheRoll()
    {
        Assert.Equal(DungeonPlan.Shape.Rect, ShapeFor(DungeonKit.Room.StairBottom, 8, 8, 0.0));
    }
}
