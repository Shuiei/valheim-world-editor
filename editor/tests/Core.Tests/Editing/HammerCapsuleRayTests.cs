using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// Hammer's ray against a capsule (marched): the distance to its surface and the normal there; no hit when the ray misses
/// or starts inside.
/// </summary>
public class HammerCapsuleRayTests
{
    // A capsule standing along y from -1 to 1 with radius 1: its side is at z = -1 and +1 from the axis.
    private static readonly Hammer.Capsule Upright = new(new Vector3(0, -1, 0), new Vector3(0, 1, 0), 1f);

    private static (float Distance, Vector3 Normal)? RayCapsule(Vector3 origin, Vector3 direction, Hammer.Capsule capsule)
    {
        var hit = (ValueTuple<float, Vector3>?)PrivateMember.Call(typeof(Hammer), "RayCapsule", null, origin, direction, capsule);
        return hit is { } h ? (h.Item1, h.Item2) : null;
    }

    [Fact]
    public void ARayHitsTheNearSideOfTheCapsule()
    {
        // From z = -5 along +z the side is met at z = -1: 4 m away, with the normal pointing back at the ray.
        var hit = RayCapsule(new Vector3(0, 0, -5), Vector3.UnitZ, Upright);

        Assert.NotNull(hit);
        Assert.Equal(4f, hit!.Value.Distance, 3);
        Assert.Equal(-1f, hit.Value.Normal.Z, 3);
    }

    [Fact]
    public void ARayThatMissesTheCapsuleHitsNothing()
    {
        Assert.Null(RayCapsule(new Vector3(3, 0, -5), Vector3.UnitZ, Upright));
    }

    [Fact]
    public void ARayStartingInsideTheCapsuleHitsNothing()
    {
        Assert.Null(RayCapsule(Vector3.Zero, Vector3.UnitZ, Upright));
    }
}
