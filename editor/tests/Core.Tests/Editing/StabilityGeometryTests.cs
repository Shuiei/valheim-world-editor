using System.Numerics;
using TerrainEditor.Editing;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The support geometry of a piece: its scale (as made when unset), its centre of mass in the world (scaled, then turned
/// with the piece), and whether two oriented boxes overlap (a separating axis test along both boxes' sides).
/// </summary>
public class StabilityGeometryTests
{
    /// <summary>
    /// A unit box (half size 1 on every side) at this middle, turned by this rotation.
    /// </summary>
    private static Stability.Obb UnitBox(Vector3 centre, Quaternion turn) =>
        new(centre, Vector3.Transform(Vector3.UnitX, turn), Vector3.Transform(Vector3.UnitY, turn), Vector3.Transform(Vector3.UnitZ, turn), Vector3.One);

    /// <summary>
    /// Whether two boxes overlap, through the stability check's own separating axis test.
    /// </summary>
    private static bool Overlap(Stability.Obb a, Stability.Obb b) => (bool)PrivateCall.Static(typeof(Stability), "Overlap", a, b)!;

    [Fact]
    public void APieceWithoutAScaleIsScaledOne()
    {
        var piece = new Stability.Piece("wood_wall", Vector3.Zero, Quaternion.Identity, 0f);

        Assert.Equal(1f, (float)PrivateCall.Static(typeof(Stability), "ScaleOf", piece)!);
    }

    [Fact]
    public void APieceWithAScaleKeepsIt()
    {
        var piece = new Stability.Piece("wood_wall", Vector3.Zero, Quaternion.Identity, 2.5f);

        Assert.Equal(2.5f, (float)PrivateCall.Static(typeof(Stability), "ScaleOf", piece)!);
    }

    [Fact]
    public void ACentreOfMassIsScaledAndMovedWithThePiece()
    {
        var data = new Stability.Data(0, true, new Vector3(1, 0, 0), []);
        var piece = new Stability.Piece("wood_wall", new Vector3(10, 0, 0), Quaternion.Identity, 2f);

        var centre = (Vector3)PrivateCall.Static(typeof(Stability), "Com", data, piece)!;

        Assert.True(Vector3.Distance(new Vector3(12, 0, 0), centre) < 1e-4f, $"centre {centre}");
    }

    [Fact]
    public void ACentreOfMassIsTurnedWithThePiece()
    {
        // A turn of a quarter about Y takes the X axis to minus Z.
        var data = new Stability.Data(0, true, new Vector3(1, 0, 0), []);
        var piece = new Stability.Piece("wood_wall", Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), 1f);

        var centre = (Vector3)PrivateCall.Static(typeof(Stability), "Com", data, piece)!;

        Assert.True(Vector3.Distance(new Vector3(0, 0, -1), centre) < 1e-4f, $"centre {centre}");
    }

    [Fact]
    public void BoxesSeparatedAlongTheirSidesDoNotOverlap()
    {
        Assert.False(Overlap(UnitBox(Vector3.Zero, Quaternion.Identity), UnitBox(new Vector3(2.5f, 0, 0), Quaternion.Identity)));
    }

    [Fact]
    public void BoxesCloserThanTheirSidesOverlap()
    {
        Assert.True(Overlap(UnitBox(Vector3.Zero, Quaternion.Identity), UnitBox(new Vector3(1.5f, 0, 0), Quaternion.Identity)));
    }

    [Fact]
    public void ATurnedBoxIsSeparatedAlongItsOwnExtent()
    {
        // The turned box reaches 1.414 along Z (its corner), so a box 2.5 away along Z misses it, and one 2.3 away hits it.
        var turned = UnitBox(Vector3.Zero, Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 4));

        Assert.False(Overlap(turned, UnitBox(new Vector3(0, 0, 2.5f), Quaternion.Identity)));
        Assert.True(Overlap(turned, UnitBox(new Vector3(0, 0, 2.3f), Quaternion.Identity)));
    }
}
