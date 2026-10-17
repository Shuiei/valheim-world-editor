using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// A ray against a turned box (the hammer's slab test): the distance to the near face and its normal, or nothing when
/// the ray misses, or starts inside the box.
/// </summary>
public class HammerRayBoxTests
{
    /// <summary>A unit box (half sizes 1) at the origin, unturned.</summary>
    private static readonly Hammer.Box UnitBox = new(Vector3.Zero, Vector3.One, Quaternion.Identity);

    /// <summary>Calls the hammer's ray test against a box.</summary>
    private static (float Distance, Vector3 Normal)? Hit(Vector3 origin, Vector3 direction, Hammer.Box box)
    {
        object? result = PrivateMember.Call(typeof(Hammer), "RayBox", null, origin, direction, box);
        return (ValueTuple<float, Vector3>?)result;
    }

    [Fact]
    public void ARayFromOutsideHitsTheNearFaceAtItsDistance()
    {
        var hit = Hit(new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, 1f), UnitBox);

        Assert.Equal(4f, hit!.Value.Distance);
    }

    [Fact]
    public void ARayHittingTheNearFaceGivesTheFaceNormalFacingTheRay()
    {
        var hit = Hit(new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, 1f), UnitBox);

        Assert.Equal(new Vector3(0f, 0f, -1f), hit!.Value.Normal);
    }

    [Fact]
    public void ARayThatPassesBesideTheBoxMisses()
    {
        Assert.Null(Hit(new Vector3(5f, 0f, -5f), new Vector3(0f, 0f, 1f), UnitBox));
    }

    [Fact]
    public void ARayThatStartsInsideTheBoxHitsNothing()
    {
        Assert.Null(Hit(Vector3.Zero, new Vector3(0f, 0f, 1f), UnitBox));
    }

    [Fact]
    public void ARayAimedAwayFromTheBoxHitsNothing()
    {
        Assert.Null(Hit(new Vector3(0f, 0f, -5f), new Vector3(0f, 0f, -1f), UnitBox));
    }
}
