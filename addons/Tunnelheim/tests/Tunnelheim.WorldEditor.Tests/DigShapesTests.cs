using System.Numerics;
using Xunit;

namespace Tunnelheim.WorldEditor.Tests;

/// <summary>
/// The dig shapes' aim and reach: the direction from pitch and yaw (dig straight's packed floor and pitch), each kind's
/// bounding sphere, a zero turn the identity, and the rough finish bounded.
/// </summary>
public class DigShapesTests
{
    [Fact]
    public void AnEditsDirectionComesFromItsPitchAndYaw()
    {
        var up = DigShapes.Direction(new DigOp(DigKind.Plane, Vector3.Zero, 1f, 90000));
        Assert.Equal(1f, up.Y, 5);
        var east = DigShapes.Direction(new DigOp(DigKind.Chip, Vector3.Zero, 1f, 0, 90f));
        Assert.Equal(1f, east.X, 5);
        int packed = ((-150 + 32768) << 16) | (-4500 + 32768);
        var floor = new DigOp(DigKind.ChipFloor, new Vector3(0f, 10f, 0f), 1.5f, packed);
        Assert.Equal(-MathF.Sqrt(0.5f), DigShapes.Direction(floor).Y, 4);
        Assert.Equal(8.5f, DigShapes.FloorY(floor), 4);
    }

    [Fact]
    public void EachKindReachesItsBoundingSphere()
    {
        var c = new Vector3(1f, 2f, 3f);
        Assert.Equal(2f * 1.42f, DigShapes.Bounds(new DigOp(DigKind.Box, c, 2f)).Radius);
        Assert.Equal(1.2f * 1.5f + 0.1f, DigShapes.Bounds(new DigOp(DigKind.Chip, c, 1.5f)).Radius, 5);
        Assert.Equal(1.5f, DigShapes.Bounds(new DigOp(DigKind.Sphere, c, 1.5f)).Radius);
        Assert.NotEqual(c, DigShapes.Bounds(new DigOp(DigKind.Stairs, c, 4f, 0, 0f)).Centre);
        Assert.NotEqual(c, DigShapes.Bounds(new DigOp(DigKind.Stroke, c, 1f, 0, 0f)).Centre);
    }

    [Fact]
    public void AZeroTurnLeavesAPointAndAHalfTurnFlipsIt()
    {
        var v = new Vector3(1.5f, 2f, -3f);
        Assert.Equal(v, DigShapes.Turn(0f, v));
        var back = DigShapes.Turn(180f, v);
        Assert.Equal(-1.5f, back.X, 4);
        Assert.Equal(3f, back.Z, 4);
    }

    [Fact]
    public void TheRoughFinishStaysWithinItsRange()
    {
        // Points all over a 200 m wide, 80 m high box, on a grid not lined up with the noise's own.
        for (float x = -100f; x <= 100f; x += 6.7f)
        {
            for (float y = 0f; y <= 80f; y += 8.3f)
            {
                for (float z = -100f; z <= 100f; z += 7.1f)
                {
                    Assert.InRange(DigShapes.Rough(new Vector3(x, y, z)), -1f, 1f);
                }
            }
        }
    }

    [Fact]
    public void EveryShapeIsInsideAtItsCentre()
    {
        var c = new Vector3(5f, 30f, 5f);
        foreach (var kind in new[] { DigKind.Sphere, DigKind.Box, DigKind.Chip, DigKind.Stroke })
        {
            Assert.True(DigShapes.Distance(new DigOp(kind, c, 1.5f), c) < 0f, kind.ToString());
        }
        Assert.True(DigShapes.Distance(new DigOp(DigKind.Stairs, c, 4f), c + new Vector3(0f, 0.75f, 0.5f)) < 0f);
    }
}
