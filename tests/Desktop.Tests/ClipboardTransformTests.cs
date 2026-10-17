using System.Numerics;
using Xunit;
using static TerrainEditor.Desktop.Tests.ClipboardSetup;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Turning and mirroring a copy: the pieces move around the middle, and a mirrored tilt keeps its pitch.
/// </summary>
public class ClipboardTransformTests
{
    [Fact]
    public void TurningAndMirroringMoveTheObjectsAroundTheMiddle()
    {
        var p = new PasteTool();
        p.TurnBy(90);
        Assert.Equal(new Vector2(0, 1), Round(p.Xf(new Vector2(1, 0))));
        p.Mirror = true;
        Assert.Equal(new Vector2(0, -1), Round(p.Xf(new Vector2(1, 0))));
        Assert.Equal(new Vector2(1, 0), Round(p.Inv(p.Xf(new Vector2(1, 0)))));
        p.TurnBy(-100);
        Assert.Equal(350, p.Turn, 3);
    }

    // Mirrored east to west, a tilted piece keeps its pitch (a beam sloping nose-down still slopes nose-down)
    // while its yaw and roll reverse: the mirrored rotation moves a mirrored point where the mirror of the
    // original's point is.
    [Fact]
    public void MirroringKeepsThePitchAndReversesYawAndRoll()
    {
        var p = new PasteTool { Mirror = true };
        var r = new Vector3(30, 40, 50);
        Assert.Equal(new Vector3(30, -40, -50), p.Rotated(r));
        var original = TerrainEditor.App.BlueprintFormats.FromEuler(r);
        var mirrored = TerrainEditor.App.BlueprintFormats.FromEuler(p.Rotated(r));
        foreach (var v in new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, new Vector3(1, 2, 3) })
        {
            var expected = Flip(Vector3.Transform(Flip(v), original));
            var got = Vector3.Transform(v, mirrored);
            Assert.Equal(expected.X, got.X, 3);
            Assert.Equal(expected.Y, got.Y, 3);
            Assert.Equal(expected.Z, got.Z, 3);
        }
        p.Mirror = false;
        p.TurnBy(90);
        Assert.Equal(new Vector3(30, -50, 50), p.Rotated(r));
        static Vector3 Flip(Vector3 v) => new(-v.X, v.Y, v.Z);
    }
}
