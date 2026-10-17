using System.Numerics;
using System.Text.Json.Nodes;
using SkiaSharp;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The clipboard format's rarer shapes: a copy with fields missing (no outline, follow flag or source id) read
/// with its own name or the one given; and its picture with nothing in it, flat ground (painted or not) and no
/// outline, and objects of every kind.
/// </summary>
public class CopyFormatBranchesTests
{
    /// <summary>The picture as a bitmap.</summary>
    private static SKBitmap Decoded(string url) => SKBitmap.Decode(Convert.FromBase64String(url["data:image/png;base64,".Length..]));

    [Fact]
    public void ACopyWithFieldsMissingIsReadWithDefaults()
    {
        var json = JsonNode.Parse("""{"w":2,"h":1,"rel":[-32768,150],"wt":[255,128],"pnt":[-255,0,0,0,255,0,0,255],"objects":[{"name":"woodwall","dx":1,"dz":2,"dy":0.5,"rx":0,"ry":90,"rz":0,"scale":0,"sourceId":7,"follow":true},{"name":"Beech1"}],"name":"Kept"}""")!.AsObject();
        var c = CopyFormat.FromJson(json);
        // No outline: none.
        Assert.Equal((2, 1), (c.W, c.H));
        Assert.Empty(c.Poly);
        Assert.Equal("Kept", c.Name);
        Assert.True(float.IsNaN(c.Rel[0]));
        Assert.Equal(1.5f, c.Rel[1], 3);
        Assert.Equal(-1, c.Pnt[0]);
        Assert.Equal(1, c.Pnt[4], 3);
        var wall = c.Objects[0];
        Assert.Equal((7, true), (wall.SourceId, wall.Follow));
        Assert.Equal(90, wall.Rotation.Y);
        // An object with nothing but its name: at the anchor, no source, not following.
        var tree = c.Objects[1];
        Assert.Equal(StableHash.Of("Beech1"), tree.Prefab);
        Assert.Equal((0f, 0f, 0f), (tree.Dx, tree.Dz, tree.Dy));
        Assert.Null(tree.SourceId);
        Assert.False(tree.Follow);
        // The name given wins, and sources dropped.
        var named = CopyFormat.FromJson(json, "Given", keepSources: false);
        Assert.Equal("Given", named.Name);
        Assert.Null(named.Objects[0].SourceId);
    }

    [Fact]
    public void AnEmptyCopysPictureIsTheBackgroundOnly()
    {
        var c = new CopyData { W = 2, H = 1, Rel = [float.NaN, float.NaN], Wt = [0f, 0], Pnt = [.. Enumerable.Repeat(-1f, 8)], Objects = [], Poly = [] };
        using var bmp = Decoded(CopyFormat.Thumb(c, 32));
        Assert.Equal(32, bmp.Width);
        Assert.All(bmp.Pixels, p => Assert.Equal(new SKColor(0x1e, 0x23, 0x2b), p));
    }

    [Fact]
    public void FlatGroundIsShadedEvenlyAndPaintShows()
    {
        // 2 × 2, flat; one cell painted, one empty; no outline; an object of no known kind.
        var c = new CopyData
        {
            W = 2,
            H = 2,
            Rel = [1f, 1, float.NaN, 1],
            Wt = [1f, 1, 1, 1],
            Pnt = [1f, 0, 0, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1],
            Objects = [new(StableHash.Of("no_such_thing_xyz"), "no_such_thing_xyz", 0, 0, 0, Vector3.Zero, 0, null)],
            Poly = [],
        };
        using var bmp = Decoded(CopyFormat.Thumb(c, 64));
        var pixels = bmp.Pixels;
        // Mid-height ground (flat), painted and not.
        Assert.Contains(pixels, p => p == new SKColor(170, 135, 90));
        Assert.Contains(pixels, p => p == new SKColor(125, 145, 85));
    }

    [Fact]
    public void AGridSizeIsAcceptedAsItIs()
    {
        Assert.Null(Record.Exception(() => CopyFormat.CheckSize(2, 3)));
    }

    [Fact]
    public void ANonPositiveSizeIsNotAGrid()
    {
        Assert.Contains("is not a grid", Assert.Throws<FormatException>(() => CopyFormat.CheckSize(0, 3)).Message);
    }

    [Fact]
    public void ASizeWhoseDiagonalDoesNotFitIsTooLargeToPaste()
    {
        Assert.Contains("is too large to paste", Assert.Throws<FormatException>(() => CopyFormat.CheckSize(50000, 10)).Message);
    }

    [Fact]
    public void AnObjectWithANameIsAcceptedByTheNameCheck()
    {
        Assert.Null(Record.Exception(() => CopyFormat.CheckNames(JsonNode.Parse("[{\"name\":\"Beech\"}]")!.AsArray())));
    }

    [Fact]
    public void AnObjectWithoutANameFailsTheNameCheck()
    {
        Assert.Contains("has no name", Assert.Throws<FormatException>(() => CopyFormat.CheckNames(JsonNode.Parse("[{\"dx\":1}]")!.AsArray())).Message);
    }

    [Fact]
    public void AnOutlinePointWithFiniteCoordinatesPassesTheOutlineCheck()
    {
        Assert.Null(Record.Exception(() => CopyFormat.CheckOutline(JsonNode.Parse("{\"poly\":[{\"gx\":1,\"gz\":2}]}")!.AsObject())));
    }

    [Fact]
    public void AnOutlinePointThatIsNotACoordinateFailsTheOutlineCheck()
    {
        Assert.Contains("outline is not a point", Assert.Throws<FormatException>(() => CopyFormat.CheckOutline(JsonNode.Parse("{\"poly\":[{\"gx\":\"a\",\"gz\":2}]}")!.AsObject())).Message);
    }

    [Fact]
    public void ACopysObjectsListIsReturnedAsIt()
    {
        var list = CopyFormat.ObjectsOf(JsonNode.Parse("{\"objects\":[{\"name\":\"Beech\"}]}")!.AsObject());
        Assert.Single(list);
    }

    [Fact]
    public void ACopyWithoutAnObjectsListFailsToRead()
    {
        Assert.Contains("no objects list", Assert.Throws<FormatException>(() => CopyFormat.ObjectsOf([])).Message);
    }

    [Fact]
    public void ObjectsAreReadWithTheirNameAndOffset()
    {
        var list = CopyFormat.ObjectsFrom(JsonNode.Parse("[{\"name\":\"Beech\",\"dx\":2}]")!.AsArray(), true);
        var o = Assert.Single(list);
        Assert.Equal(("Beech", 2f), (o.Name, o.Dx));
    }
}
