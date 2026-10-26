using TerrainEditor.App;
using WorldEditor.Tests.Unity;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The build pieces' boxes (PieceScan): the solid box colliders an object has, and the eight corners each box adds to
/// a piece's box, in the piece's frame. Made-up pieces (TestGame), hand-picked numbers.
/// </summary>
public class GameCataloguesPieceTests
{
    /// <summary>
    /// The corners a box of size 2 around the origin adds to a piece at <paramref name="p"/>, scaled by
    /// <paramref name="scale"/>, in the order the scan adds them.
    /// </summary>
    private static List<(double X, double Y, double Z)> CornersOf((double X, double Y, double Z) p, (double X, double Y, double Z) scale)
    {
        using var game = new TestGame();
        using var g = ColliderTestGame.Piece(game, game.Bundle("p_pieces"), r => r.Box((0, 0, 0), (2, 2, 2)), out long box);
        var pts = new List<(double X, double Y, double Z)>();
        GameCatalogues.PieceScan.AddBoxCorners([g.Open("p_pieces")!.Field(box)!], p, GameCatalogues.Quat.Root, scale, pts);
        return pts;
    }

    [Fact]
    public void AnObjectsSolidBoxesAreTheOnesKept()
    {
        // A solid box (size 2 in x), a trigger box (size 1), a disabled box and a sphere: only the solid box is kept.
        using var game = new TestGame();
        using var g = ColliderTestGame.Piece(game, game.Bundle("p_pieces"), r =>
        {
            r.Box((0, 1, 0), (2, 2, 0.2));
            r.Box((0, 5, 0), (1, 1, 1), trigger: true);
            r.Box((0, 6, 0), (1, 1, 1), enabled: false);
            r.Sphere((1, 0, 0), 0.5);
            return r.Go;
        }, out long go);
        var bundle = g.Open("p_pieces")!;
        var (tr, boxes) = GameCatalogues.PieceScan.TransformAndBoxes(bundle, bundle.Field(go)!);
        Assert.NotNull(tr);
        var box = Assert.Single(boxes);
        Assert.Equal(2f, box["m_Size"]["x"].AsFloat);
    }

    [Fact]
    public void ABoxAddsItsEightCornersAroundThePiecesOrigin()
    {
        // Size 2 around the origin: the corners are at +-1 on each axis, the first (-1, -1, -1) and the last (1, 1, 1),
        // here moved by (10, 0, 0).
        var pts = CornersOf((10.0, 0.0, 0.0), (1.0, 1.0, 1.0));
        Assert.Equal(8, pts.Count);
        Assert.Equal((9.0, -1.0, -1.0), pts[0]);
        Assert.Equal((11.0, 1.0, 1.0), pts[7]);
    }

    [Fact]
    public void ABoxCornersAreScaledByTheObjectsScale()
    {
        // Under the scale (2, 1, 1) the box is 4 wide in x: its first corner is (-2, -1, -1), its last (2, 1, 1).
        var pts = CornersOf((0.0, 0.0, 0.0), (2.0, 1.0, 1.0));
        Assert.Equal((-2.0, -1.0, -1.0), pts[0]);
        Assert.Equal((2.0, 1.0, 1.0), pts[7]);
    }
}
