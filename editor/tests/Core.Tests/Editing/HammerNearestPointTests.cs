using System.Numerics;
using TerrainEditor.Editing;
using Xunit;
using static WorldEditor.Tests.HammerTestScene;

namespace WorldEditor.Tests;

/// <summary>
/// The nearest point of each collider shape the hammer knows (a box, a capsule, a convex hull) and the angle between two
/// rotations: the points a piece is rested and snapped by. Reached through the hammer's private helpers.
/// </summary>
public class HammerNearestPointTests
{
    /// <summary>The nearest point of a box to a point.</summary>
    private static Vector3 OnBox(Hammer.Box box, Vector3 p) => (Vector3)PrivateCall.Static(typeof(Hammer), "ClosestBox", box, p)!;

    /// <summary>The nearest point of a capsule to a point.</summary>
    private static Vector3 OnCapsule(Hammer.Capsule capsule, Vector3 p) => (Vector3)PrivateCall.Static(typeof(Hammer), "ClosestCapsule", capsule, p)!;

    /// <summary>The nearest point of a convex hull to a point.</summary>
    private static Vector3 OnHull(Hammer.Mesh mesh, Vector3 p) => (Vector3)PrivateCall.Static(typeof(Hammer), "ClosestHull", mesh, p)!;

    /// <summary>The angle between two rotations, in degrees.</summary>
    private static float Angle(Quaternion a, Quaternion b) => (float)PrivateCall.Static(typeof(Hammer), "AngleDegrees", a, b)!;

    /// <summary>A unit cube centred on the origin, as twelve triangles.</summary>
    private static Hammer.Mesh UnitCube()
    {
        Vector3[] v =
        [
            new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f),
            new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f),
        ];
        int[] i =
        [
            0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 4, 7, 0, 7, 3,
            1, 2, 6, 1, 6, 5, 0, 1, 5, 0, 5, 4, 3, 7, 6, 3, 6, 2,
        ];
        return new Hammer.Mesh(true, v, i);
    }

    [Fact]
    public void TheAngleBetweenRotationsIsTheTurnInDegrees()
    {
        var quarterTurn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        Assert.Equal(90f, Angle(Quaternion.Identity, quarterTurn), 2);
    }

    [Fact]
    public void AHalfTurnIsAHundredAndEightyDegrees()
    {
        var halfTurn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI);
        Assert.Equal(180f, Angle(Quaternion.Identity, halfTurn), 2);
    }

    [Fact]
    public void ARotationAndItsNegativeQuaternionAreTheSameTurn()
    {
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitX, 0.3f);
        Assert.Equal(0f, Angle(turn, turn), 3);
        Assert.Equal(0f, Angle(turn, Quaternion.Negate(turn)), 3);
    }

    [Fact]
    public void APointInsideABoxIsItself()
    {
        var box = new Hammer.Box(new Vector3(1, 2, 3), new Vector3(1, 1, 1), Quaternion.Identity);
        Near(new Vector3(1.5f, 2, 3), OnBox(box, new Vector3(1.5f, 2, 3)));
    }

    [Fact]
    public void APointOutsideABoxIsOnTheNearestFace()
    {
        var box = new Hammer.Box(new Vector3(1, 2, 3), new Vector3(1, 1, 1), Quaternion.Identity);
        Near(new Vector3(2, 2, 3), OnBox(box, new Vector3(4, 2, 3)));
    }

    [Fact]
    public void ABoxTurnedAQuarterTurnIsCutAlongTheTurnedAxis()
    {
        // Long along x before the turn: after a quarter turn about y it runs along z, so the point (0, 0, 5) lies
        // beyond its +z end at z = 2.
        var turned = new Hammer.Box(Vector3.Zero, new Vector3(2, 1, 1), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));
        Near(new Vector3(0, 0, 2), OnBox(turned, new Vector3(0, 0, 5)));
    }

    [Fact]
    public void APointInsideACapsuleIsItself()
    {
        var capsule = new Hammer.Capsule(Vector3.Zero, new Vector3(0, 2, 0), 0.5f);
        Near(new Vector3(0.2f, 1, 0), OnCapsule(capsule, new Vector3(0.2f, 1, 0)));
    }

    [Fact]
    public void APointBeyondACapsuleEndIsOnItsRoundEnd()
    {
        var capsule = new Hammer.Capsule(Vector3.Zero, new Vector3(0, 2, 0), 0.5f);
        Near(new Vector3(0, 2.5f, 0), OnCapsule(capsule, new Vector3(0, 5, 0)));
    }

    [Fact]
    public void APointBesideACapsuleIsOnItsSide()
    {
        var capsule = new Hammer.Capsule(Vector3.Zero, new Vector3(0, 2, 0), 0.5f);
        Near(new Vector3(0.5f, 1, 0), OnCapsule(capsule, new Vector3(3, 1, 0)));
    }

    [Fact]
    public void ACapsuleWithNoLengthIsASphere()
    {
        var sphere = new Hammer.Capsule(Vector3.Zero, Vector3.Zero, 1f);
        Near(new Vector3(0, 1, 0), OnCapsule(sphere, new Vector3(0, 3, 0)));
    }

    [Fact]
    public void APointOutsideAHullIsOnItsNearestFace()
    {
        Near(new Vector3(0.5f, 0, 0), OnHull(UnitCube(), new Vector3(2, 0, 0)));
    }

    [Fact]
    public void APointInsideAHullIsItself()
    {
        var p = new Vector3(0.1f, 0.2f, 0.3f);
        Near(p, OnHull(UnitCube(), p));
    }
}
