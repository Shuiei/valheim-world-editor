using System.Numerics;
using Avalonia.Headless.XUnit;
using TerrainEditor.Terrain;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The map's drawing helpers: corners, quads and discs as triangles, the zoom kind of the buildings, the shapes of a
/// piece, the buildings' rebuild check, and the frame that turns world metres into the lines' clip space.
/// </summary>
public class MapViewDrawHelpersTests
{
    [Fact]
    public void ACornerIsItsPositionAndColour()
    {
        List<float> to = [];
        MapView.Corner(to, 1, 2, new Vector4(0.1f, 0.2f, 0.3f, 0.4f));
        Assert.Equal(new float[] { 1, 2, 0.1f, 0.2f, 0.3f, 0.4f }, to.ToArray());
    }

    [Fact]
    public void AQuadIsTwoTrianglesOfSixCorners()
    {
        List<float> to = [];
        MapView.Quad(to, new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), Vector4.One);
        Assert.Equal(36, to.Count);
        // The corners in order: a, b, c, a, c, d.
        Assert.Equal(new float[] { 0, 0 }, to.GetRange(0, 2).ToArray());
        Assert.Equal(new float[] { 1, 0 }, to.GetRange(6, 2).ToArray());
        Assert.Equal(new float[] { 1, 1 }, to.GetRange(12, 2).ToArray());
        Assert.Equal(new float[] { 0, 0 }, to.GetRange(18, 2).ToArray());
        Assert.Equal(new float[] { 1, 1 }, to.GetRange(24, 2).ToArray());
        Assert.Equal(new float[] { 0, 1 }, to.GetRange(30, 2).ToArray());
    }

    [Fact]
    public void ADiscIsAFanOfTriangles()
    {
        List<float> to = [];
        MapView.Disc(to, 0, 0, 2, Vector4.One, 8);
        // Eight triangles of three corners: the middle first, then two points on the rim.
        Assert.Equal(8 * 3 * 6, to.Count);
        Assert.Equal(new float[] { 0, 0 }, to.GetRange(0, 2).ToArray());
        Assert.Equal(2f, to[6], 4);
        Assert.Equal(0f, to[7], 4);
    }

    [Fact]
    public void TheZoomKindFollowsTheMetresPerPixel()
    {
        Assert.Equal(2, MapView.KindFor(0.5f));
        Assert.Equal(1, MapView.KindFor(0.8f));
        Assert.Equal(1, MapView.KindFor(1f));
        Assert.Equal(0, MapView.KindFor(2.5f));
        Assert.Equal(0, MapView.KindFor(10f));
    }

    [Fact]
    public void ADotIsASquareAtTheSmallestZoom()
    {
        float[] pc = new float[PieceCatalog.Stride];
        pc[0] = 5;
        pc[2] = 7;
        List<float> fill = [];
        List<float> edge = [];
        MapView.AddPiece(fill, edge, pc, 0, false, 0, 1f);
        Assert.Equal(36, fill.Count);
        Assert.Empty(edge);
        Assert.Equal(4f, fill[0], 4);
        Assert.Equal(6f, fill[1], 4);
    }

    [Fact]
    public void AClosePieceIsAFootprintWithOutlinesWhenZoomedInMost()
    {
        float[] pc = new float[PieceCatalog.Stride];
        pc[4] = -1;
        pc[5] = 1;
        pc[6] = -1;
        pc[7] = 1;
        List<float> fill = [];
        List<float> edge = [];
        MapView.AddPiece(fill, edge, pc, 0, false, 2, 0.5f);
        Assert.Equal(36, fill.Count);
        // Four sides, each two points of six floats, so 48 floats.
        Assert.Equal(48, edge.Count);
    }

    [Fact]
    public void AFaintRuinIsDimmedToAHalfAndAPlainPieceIsNot()
    {
        float[] pc = new float[PieceCatalog.Stride];
        pc[4] = -1;
        pc[5] = 1;
        pc[6] = -1;
        pc[7] = 1;
        List<float> plain = [];
        List<float> faint = [];
        MapView.AddPiece(plain, [], pc, 0, false, 1, 1f);
        MapView.AddPiece(faint, [], pc, 0, true, 1, 1f);
        // The first corner's alpha: the piece's fill alpha, and that times 0.55 for a faint ruin.
        Assert.Equal(0.92f, plain[5], 3);
        Assert.Equal(0.92f * 0.55f, faint[5], 3);
    }

    [AvaloniaFact]
    public void AFreshViewMustBuildItsBuildingsShapes()
    {
        var map = new MapView();
        Assert.True(map.BuildingsStale(new float[PieceCatalog.Stride], 0, 1f));
    }

    [Fact]
    public void AFrameTurnsWorldMetresIntoClipSpace()
    {
        var frame = new MapView.LineFrame(new Vector2(0, 0), 100, 100, 1f);
        var c = frame.C(50, 0);
        Assert.Equal(1f, c.X, 4);
        Assert.Equal(0f, c.Y, 4);
        List<float> to = [];
        frame.Box(to, -10, -10, 10, 10);
        // Four sides of two points, each point three floats: 24 floats; the first side starts at the box's corner.
        Assert.Equal(24, to.Count);
        Assert.Equal(-0.2f, to[0], 4);
        Assert.Equal(-0.2f, to[1], 4);
        Assert.Equal(0.2f, to[3], 4);
        Assert.Equal(-0.2f, to[4], 4);
    }
}
