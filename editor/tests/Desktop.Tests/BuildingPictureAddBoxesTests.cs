using System.Numerics;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A piece's box shapes as triangles for a building picture: a piece with no known shape gets one box, a box's six faces
/// are twelve triangles, and a piece's scale of zero is read as one (BuildingPicture.AddBoxes).
/// </summary>
public class BuildingPictureAddBoxesTests
{
    /// <summary>The triangles of one piece's boxes.</summary>
    private static List<BuildingPicture.Tri> Boxes(BuildingPicture.Piece piece)
    {
        var tris = new List<BuildingPicture.Tri>();
        BuildingPicture.AddBoxes(tris, piece);
        return tris;
    }

    [Fact]
    public void APieceWithNoKnownShapeGetsOneBoxOfTwelveTriangles()
    {
        var tris = Boxes(new BuildingPicture.Piece("no_such_piece_for_test", Vector3.Zero, Vector3.Zero, 1f));

        Assert.Equal(12, tris.Count);
    }

    [Fact]
    public void APieceWithNoKnownMaterialIsGrey()
    {
        var tris = Boxes(new BuildingPicture.Piece("no_such_piece_for_test", Vector3.Zero, Vector3.Zero, 1f));

        Assert.All(tris, t => Assert.Equal(new Vector3(130, 130, 130), t.Colour));
    }

    [Fact]
    public void ZeroScaleIsTakenAsOne()
    {
        var unit = Boxes(new BuildingPicture.Piece("no_such_piece_for_test", Vector3.Zero, Vector3.Zero, 1f));
        var zero = Boxes(new BuildingPicture.Piece("no_such_piece_for_test", Vector3.Zero, Vector3.Zero, 0f));

        Assert.Equal(unit, zero);
    }

    [Fact]
    public void TheBoxSpreadsHalfAMetreEitherSideOfThePieceAcrossX()
    {
        var tris = Boxes(new BuildingPicture.Piece("no_such_piece_for_test", new Vector3(5, 0, 0), Vector3.Zero, 1f));

        Assert.All(tris, t => Assert.InRange(t.A.X, 4.5f, 5.5f));
    }
}
