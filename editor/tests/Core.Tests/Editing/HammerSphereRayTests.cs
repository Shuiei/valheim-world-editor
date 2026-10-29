using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Hammer's ray against a sphere: the distance to the near side and the surface's normal there; no hit when the ray
/// misses, points away, or starts inside.
/// </summary>
public class HammerSphereRayTests
{
    private static (float Distance, Vector3 Normal)? RaySphere(Vector3 origin, Vector3 direction, Vector3 center, float radius)
    {
        var hit = (ValueTuple<float, Vector3>?)PrivateMember.Call(typeof(Hammer), "RaySphere", null, origin, direction, center, radius);
        return hit is { } h ? (h.Item1, h.Item2) : null;
    }

    [Fact]
    public void ARayHitsTheNearSideOfTheSphere()
    {
        // From z = -5 along +z, a unit sphere at the origin is first met at z = -1: 4 m away, its normal pointing back at the ray.
        var hit = RaySphere(new Vector3(0, 0, -5), Vector3.UnitZ, Vector3.Zero, 1f);

        Assert.NotNull(hit);
        Assert.Equal(4f, hit!.Value.Distance, 4);
        Assert.Equal(-1f, hit.Value.Normal.Z, 4);
        Assert.Equal(0f, hit.Value.Normal.X, 4);
    }

    [Fact]
    public void ARayThatMissesTheSphereHitsNothing()
    {
        Assert.Null(RaySphere(new Vector3(0, 2, -5), Vector3.UnitZ, Vector3.Zero, 1f));
    }

    [Fact]
    public void ARayPointingAwayFromTheSphereHitsNothing()
    {
        Assert.Null(RaySphere(new Vector3(0, 0, 5), Vector3.UnitZ, Vector3.Zero, 1f));
    }

    [Fact]
    public void ARayStartingInsideTheSphereHitsNothing()
    {
        Assert.Null(RaySphere(Vector3.Zero, Vector3.UnitZ, Vector3.Zero, 1f));
    }
}
