using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A ray against a mesh's triangles (Hammer.RayMesh): the nearest triangle on the ray's way is hit at its distance, both
/// sides count, the normal faces the ray, and a triangle behind the ray or with no area is never hit.
/// </summary>
public class HammerRayMeshTests
{
    /// <summary>The unit triangle in the ground plane (y = 0), facing up.</summary>
    private static Hammer.Mesh Flat() => new(true, [Vector3.Zero, Vector3.UnitX, Vector3.UnitZ], [0, 1, 2]);

    private static (float, Vector3)? RayMesh(Vector3 o, Vector3 d, Hammer.Mesh m) =>
        ((float, Vector3)?)PrivateCall.Static(typeof(Hammer), "RayMesh", o, d, m);

    [Fact]
    public void ARayFromAboveHitsTheTriangleAtItsDistanceWithTheNormalTowardTheRay()
    {
        var hit = RayMesh(new Vector3(0.25f, 5f, 0.25f), -Vector3.UnitY, Flat());
        Assert.NotNull(hit);
        Assert.Equal(5f, hit.Value.Item1, 4);
        Assert.Equal(Vector3.UnitY, hit.Value.Item2);
    }

    [Fact]
    public void ARayFromBelowHitsTheFarSideWithTheNormalTowardTheRay()
    {
        var hit = RayMesh(new Vector3(0.25f, -5f, 0.25f), Vector3.UnitY, Flat());
        Assert.NotNull(hit);
        Assert.Equal(5f, hit.Value.Item1, 4);
        Assert.Equal(-Vector3.UnitY, hit.Value.Item2);
    }

    [Fact]
    public void ARayPassingBesideTheTriangleHitsNothing()
    {
        Assert.Null(RayMesh(new Vector3(2f, 5f, 2f), -Vector3.UnitY, Flat()));
    }

    [Fact]
    public void ATriangleBehindTheRayIsNotHit()
    {
        Assert.Null(RayMesh(new Vector3(0.25f, 5f, 0.25f), Vector3.UnitY, Flat()));
    }

    [Fact]
    public void ATriangleWithNoAreaIsNeverHit()
    {
        var line = new Hammer.Mesh(true, [Vector3.Zero, Vector3.UnitX, new Vector3(2f, 0f, 0f)], [0, 1, 2]);
        Assert.Null(RayMesh(new Vector3(0.25f, 5f, 0f), -Vector3.UnitY, line));
    }

    [Fact]
    public void TheNearerOfTwoTrianglesOnTheRayIsHit()
    {
        var stacked = new Hammer.Mesh(true,
            [Vector3.Zero, Vector3.UnitX, Vector3.UnitZ, new Vector3(0f, 2f, 0f), new Vector3(1f, 2f, 0f), new Vector3(0f, 2f, 1f)],
            [0, 1, 2, 3, 4, 5]);
        var hit = RayMesh(new Vector3(0.25f, 5f, 0.25f), -Vector3.UnitY, stacked);
        Assert.NotNull(hit);
        Assert.Equal(3f, hit.Value.Item1, 4);
    }
}
