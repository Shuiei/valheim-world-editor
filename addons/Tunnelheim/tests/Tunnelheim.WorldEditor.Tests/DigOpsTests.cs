using System.Numerics;
using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// The Tunnel tool's edits (DigOps): aimed ones keep their direction as the mod reads it back; the square dig snaps to
/// its own grid; Add Earth sets its lump out from the rock and keeps under the building height; the walls and ceiling
/// face the right way; the shapes cover what they say.
/// </summary>
public class DigOpsTests
{
    private static void Near(Vector3 expected, Vector3 actual, float within = 1e-3f) =>
        Assert.True(Vector3.Distance(expected, actual) < within, $"{actual} is not {expected}");

    [Theory]
    [InlineData(0f, 0f, 1f)]
    [InlineData(1f, -2f, 0.5f)]
    [InlineData(-0.3f, 0.9f, -0.2f)]
    [InlineData(0f, -1f, 0f)]
    public void AnAimedEditsDirectionReadsBackAsTheModReadsIt(float x, float y, float z)
    {
        var dir = Vector3.Normalize(new Vector3(x, y, z));
        var op = DigOps.Aimed(DigKind.Chip, new Vector3(1, 2, 3), 1.5f, dir * 3);
        Near(dir, DigShapes.Direction(op));
        Assert.Equal((DigKind.Chip, new Vector3(1, 2, 3), 1.5f), (op.Kind, op.Position, op.Value));
    }

    [Fact]
    public void TheSquareDigSnapsToAGridOfItsSize()
    {
        var op = DigOps.SquareDig(new Vector3(3.9f, 10.2f, -0.4f), -Vector3.UnitY, 1f);
        Assert.Equal(DigKind.Box, op.Kind);
        Assert.Equal(1.02f, op.Value);
        // 0.35 m down the swing, then the middle of the 2 m cell holding it.
        Near(new Vector3(3f, 9f, -1f), op.Position);
    }

    [Fact]
    public void AddEarthIsSetOutFromTheRockAndKeptUnderTheBuildingHeight()
    {
        var op = DigOps.AddEarth(new Vector3(0, 10, 0), new Vector3(0, 0, 2));
        Assert.Equal((DigKind.Fill, 1f), (op.Kind, op.Value));
        Near(new Vector3(0, 10, 0.4f), op.Position);
        var grid = new RockGrid((_, _) => 10f);
        Assert.True(DigOps.MayFill(grid, op));
        Assert.False(DigOps.MayFill(grid, DigOps.AddEarth(new Vector3(0, 76, 0), Vector3.UnitY)));
    }

    [Fact]
    public void WallsFaceTheLookAndCeilingsFaceDown()
    {
        Near(new Vector3(0, 0, -1), DigOps.WallNormal(new Vector3(0, -0.5f, 2), Vector3.UnitY));
        Near(Vector3.UnitX, DigOps.WallNormal(-Vector3.UnitY, new Vector3(3, 1, 0)));
        Near(Vector3.UnitZ, DigOps.WallNormal(-Vector3.UnitY, Vector3.UnitY));
        Near(-Vector3.UnitY, DigShapes.Direction(DigOps.Plane(Vector3.Zero, DigOps.CeilingNormal, 2)));
    }

    [Fact]
    public void ABoreRunsFromItsStartToItsEnd()
    {
        var ops = DigOps.Bore(Vector3.Zero, new Vector3(10, 0, 0), 2f);
        Assert.Equal(11, ops.Count);
        Assert.All(ops, o => Assert.Equal((DigKind.Sphere, 2f), (o.Kind, o.Value)));
        Near(Vector3.Zero, ops[0].Position);
        Near(new Vector3(10, 0, 0), ops[^1].Position);
        Assert.Equal(401, DigOps.Bore(Vector3.Zero, new Vector3(1000, 0, 0), 0.5f).Count);
    }

    [Fact]
    public void ARoomAndAShaftAreFilledWithBoxes()
    {
        var room = DigOps.Room(new Vector3(0, 10, 0), 6, 4, 4);
        Assert.Equal(3 * 2 * 2, room.Count);
        Assert.All(room, o => Assert.Equal((DigKind.Box, 1f), (o.Kind, o.Value)));
        // Centred across, from the floor up: exactly the size asked for.
        Assert.Equal((new Vector3(-3, 10, -2), new Vector3(3, 14, 2)), DigOps.Extent(room));
        // An odd size is rounded up to whole 2 m cubes, still centred.
        Assert.Equal((new Vector3(-4, 10, -4), new Vector3(4, 14, 4)), DigOps.Extent(DigOps.Room(new Vector3(0, 10, 0), 7, 7, 3)));

        var shaft = DigOps.Shaft(new Vector3(5, 20, 5), 1.5f, 9f);
        Assert.All(shaft, o => Assert.Equal((DigKind.Box, 1.5f, 5f, 5f), (o.Kind, o.Value, o.Position.X, o.Position.Z)));
        Assert.Equal(12.5f, shaft.Min(o => o.Position.Y));
        Assert.True(shaft.Max(o => o.Position.Y) + 1.5f >= 20f);
    }

    [Fact]
    public void StairsGoDownTheWayLookedLevel()
    {
        var op = DigOps.Stairs(new Vector3(0, 30, 0), new Vector3(1, -1, 0), 6);
        Assert.Equal((DigKind.Stairs, 6f), (op.Kind, op.Value));
        Near(Vector3.UnitX, DigShapes.Direction(op));
        Near(Vector3.UnitZ, DigShapes.Direction(DigOps.Stairs(Vector3.Zero, -Vector3.UnitY, 1)));
    }

    [Fact]
    public void TheRoundDigFollowsTheSurfaceItHits()
    {
        var grid = new RockGrid((_, _) => 10f);
        // Flat ground: the chip goes along its normal, up, whatever the swing.
        var op = DigOps.RoundDig(grid, new Vector3(4, 10, 4), new Vector3(1, -1, 0), 1.5f);
        Assert.Equal((DigKind.Chip, 1.5f), (op.Kind, op.Value));
        Near(Vector3.UnitY, DigShapes.Direction(op));
        // No slope in the rock (deep inside it): the swing's own direction, reversed.
        Near(new Vector3(0, 1, 0), grid.SurfaceNormal(new Vector3(4, -40, 4), Vector3.UnitY));
        Near(-Vector3.UnitY, DigShapes.Direction(DigOps.RoundDig(grid, new Vector3(4, -40, 4), Vector3.UnitY, 1)));
        // No swing at all: straight down, so up.
        Near(Vector3.UnitY, DigShapes.Direction(DigOps.RoundDig(grid, new Vector3(4, -40, 4), Vector3.Zero, 1)));
    }
}
