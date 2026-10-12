using System.Numerics;
using System.Text.Json.Nodes;
using SkiaSharp;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The clipboard format's rarer shapes: a copy with fields missing (no size, outline, follow flag or source id) read
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
        var json = JsonNode.Parse("""{"rel":[-32768,150],"wt":[255,128],"pnt":[-255,0,0,0,255,0,0,255],"objects":[{"name":"woodwall","dx":1,"dz":2,"dy":0.5,"rx":0,"ry":90,"rz":0,"scale":0,"sourceId":7,"follow":true},{"name":"Beech1"}],"name":"Kept"}""")!.AsObject();
        var c = CopyFormat.FromJson(json);
        // No size, no outline: none.
        Assert.Equal((0, 0), (c.W, c.H));
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
}
