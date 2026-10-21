using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The ghost placement off a hit: the piece is moved out along the hit's normal until its nearest collider point touches
/// the hit.
/// </summary>
public class HammerOffHitTests
{
    /// <summary>A piece of a single unit box at the origin, with no snap points.</summary>
    private static Hammer.Data UnitBox() =>
        new(Ground: false, ClipGround: false, ClipEverything: false, RotatedOverlap: false, Snaps: [],
            Colliders: [new Hammer.Box(Vector3.Zero, new Vector3(0.5f), Quaternion.Identity)]);

    [Fact]
    public void AHitOnTopPlacesThePieceAboveIt()
    {
        var pos = Hammer.OffHit(UnitBox(), new Vector3(0, 0.5f, 0), Vector3.UnitY, Quaternion.Identity);

        Assert.True(Vector3.Distance(new Vector3(0, 1, 0), pos) < 1e-3f, pos.ToString());
    }

    [Fact]
    public void AHitOnASideSetsThePieceBesideIt()
    {
        var pos = Hammer.OffHit(UnitBox(), new Vector3(0.5f, 0, 0), Vector3.UnitX, Quaternion.Identity);

        Assert.True(Vector3.Distance(new Vector3(1, 0, 0), pos) < 1e-3f, pos.ToString());
    }
}
