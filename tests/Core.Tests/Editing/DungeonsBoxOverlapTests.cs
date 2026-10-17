using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Whether two turned boxes of a dungeon overlap: boxes that share a face or only touch do not, and a box turned
/// a quarter turn reaches what its unturned self does not.
/// </summary>
public class DungeonsBoxOverlapTests
{
    [Fact]
    public void BoxesWithTheSameCentreOverlap()
    {
        var box = (C: Vector3.Zero, H: Vector3.One, R: Quaternion.Identity);

        Assert.True(Dungeons.BoxesOverlap(box, box));
    }

    [Fact]
    public void BoxesFarApartDoNotOverlap()
    {
        var a = (C: Vector3.Zero, H: Vector3.One, R: Quaternion.Identity);
        var b = (C: new Vector3(5, 0, 0), H: Vector3.One, R: Quaternion.Identity);

        Assert.False(Dungeons.BoxesOverlap(a, b));
    }

    [Fact]
    public void BoxesWhoseFacesTouchDoNotOverlap()
    {
        // Half sizes 1 each: the faces meet at x = 1 when the centres are 2 apart.
        var a = (C: Vector3.Zero, H: Vector3.One, R: Quaternion.Identity);
        var b = (C: new Vector3(2, 0, 0), H: Vector3.One, R: Quaternion.Identity);

        Assert.False(Dungeons.BoxesOverlap(a, b));
    }

    [Fact]
    public void BoxesThatPushIntoEachOtherOverlap()
    {
        var a = (C: Vector3.Zero, H: Vector3.One, R: Quaternion.Identity);
        var b = (C: new Vector3(1.5f, 0, 0), H: Vector3.One, R: Quaternion.Identity);

        Assert.True(Dungeons.BoxesOverlap(a, b));
    }

    [Fact]
    public void ABoxTurnedAQuarterTurnReachesTheBoxBesideItThatItMissedBeforeTheTurn()
    {
        // A long thin box along x, and a box above it on z: apart when the long box lies along x, touching
        // only once it is turned to lie along z.
        var thin = new Vector3(2, 0.1f, 0.1f);
        var beside = (C: new Vector3(0, 0, 1.5f), H: new Vector3(0.5f, 0.5f, 0.5f), R: Quaternion.Identity);
        var unturned = (C: Vector3.Zero, H: thin, R: Quaternion.Identity);
        var turned = (C: Vector3.Zero, H: thin, R: Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));

        Assert.False(Dungeons.BoxesOverlap(unturned, beside));
        Assert.True(Dungeons.BoxesOverlap(turned, beside));
    }
}
