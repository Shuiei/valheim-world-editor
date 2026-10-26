using System.Numerics;
using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using TerrainEditor.App;
using TerrainEditor.Save;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// Blueprints: copies kept as files (in the web editor's format), pasted, exported and imported.
/// </summary>
public class CopyFormatTests
{
    /// <summary>
    /// A small 3 × 2 copy with ground, an empty cell, paint and objects.
    /// </summary>
    private static CopyData Sample() => new()
    {
        W = 3,
        H = 2,
        Rel = [0f, 0.5f, float.NaN, -1.25f, 2, 0],
        Wt = [1f, 1, 0, 0.5f, 1, 1],
        Pnt = [-1f, -1, -1, -1, 1, 0, 0, 1, -1, -1, -1, -1, -1, -1, -1, -1, 0, 0, 1, 1, -1, -1, -1, -1],
        Objects = [new(StableHash.Of("woodwall"), "woodwall", 1, -0.5f, 0.25f, new Vector3(0, 90, 0), 0, 42), new(StableHash.Of("Beech1"), "Beech1", -1, 0, 0, Vector3.Zero, 1.1f, null, Follow: true)],
        Poly = [new(-2, -1), new(2, -1), new(2, 1), new(-2, 1)],
        Name = "Sample",
    };

    [Fact]
    public void ACopyGoesToJsonAndBack()
    {
        var c = Sample();
        var back = CopyFormat.FromJson(CopyFormat.ToJson(c));
        Assert.Equal(c.W, back.W);
        Assert.True(float.IsNaN(back.Rel[2]));
        Assert.Equal(-1.25f, back.Rel[3], 3);
        Assert.Equal(0.5f, back.Wt[3], 2);
        Assert.Equal(-1, back.Pnt[0]);
        Assert.Equal(1, back.Pnt[4], 2);
        Assert.Equal(c.Objects, back.Objects);
        Assert.Equal(c.Poly, back.Poly);
        // Elsewhere, the source ids are dropped.
        Assert.All(CopyFormat.FromJson(CopyFormat.ToJson(c), keepSources: false).Objects, o => Assert.Null(o.SourceId));
        Assert.StartsWith("data:image/png;base64,", CopyFormat.Thumb(c));
    }

    // Heights past ±327.67 m (a copy off a tall No-limit mountain) are kept as they are, not cut there; only the "no
    // ground" marker's value is moved a centimetre.
    [Fact]
    public void HeightsPastTheOldLimitAreKept()
    {
        var c = Sample();
        c.Rel[0] = 500f;
        c.Rel[1] = -400f;
        c.Rel[3] = -327.68f;
        var back = CopyFormat.FromJson(CopyFormat.ToJson(c));
        Assert.Equal(500f, back.Rel[0], 2);
        Assert.Equal(-400f, back.Rel[1], 2);
        Assert.Equal(-327.67f, back.Rel[3], 2);
    }

    // A kind without a known name (a mod's) is written as its hash and read back as that kind (it came
    // back as the hash of the number's text: an unknown kind, left out when pasted).
    [Fact]
    public void AKindWithoutANameKeepsItsHash()
    {
        var c = Sample();
        c.Objects.Clear();
        c.Objects.Add(new(-123456789, "-123456789", 0, 0, 0, Vector3.Zero, 0, null));
        Assert.Equal(-123456789, CopyFormat.FromJson(CopyFormat.ToJson(c)).Objects.Single().Prefab);
        Assert.Equal(StableHash.Of("woodwall"), StableHash.OfName("woodwall"));
    }

    [Fact]
    public void TheWebEditorsBlueprintsAreRead()
    {
        // As the web editor's encodeClip writes it.
        var json = JsonNode.Parse("""{"w":1,"h":1,"rel":[-32768],"wt":[0],"pnt":[-255,-255,-255,-255],"objects":[{"name":"stone_wall_2x1","dx":0.5,"dz":0,"dy":1,"rx":0,"ry":180,"rz":0,"scale":0,"sourceId":null}],"poly":[{"gx":-1,"gz":-1},{"gx":1,"gz":-1},{"gx":1,"gz":1}]}""")!.AsObject();
        var c = CopyFormat.FromJson(json, "Wall");
        var o = Assert.Single(c.Objects);
        Assert.Equal(StableHash.Of("stone_wall_2x1"), o.Prefab);
        Assert.Equal(180, o.Rotation.Y);
        Assert.False(o.Follow);
        Assert.Equal(3, c.Poly.Count);
        Assert.Equal("Wall", c.Name);
    }

    // A hand-edited or older blueprint whose ground arrays are not as long as its size says (or missing), whose size
    // is not a grid or too large to paste, with a nameless object or an outline point that is not one is refused as it is read: pasted, it
    // stopped half-way through.
    [Theory]
    [InlineData("""{"w":2,"h":2,"rel":[0,0,0],"wt":[0,0,0,0],"pnt":[0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0],"objects":[]}""", "\"rel\" list holds 3 values, not the 4")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0,0],"pnt":[0,0,0,0],"objects":[]}""", "\"wt\" list holds 2 values, not the 1")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"objects":[]}""", "no \"pnt\" list")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0],"objects":[]}""", "\"pnt\" list holds 3 values, not the 4")]
    [InlineData("""{"w":0,"h":1,"rel":[],"wt":[],"pnt":[],"objects":[]}""", "size (0 × 1)")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0]}""", "no objects list")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0],"objects":[{"dx":1}]}""", "has no name")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0],"objects":[{"name":" "}]}""", "has no name")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0],"objects":[],"poly":[{"gx":0,"gz":0},null]}""", "outline is not a point")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0],"objects":[],"poly":[{"gx":0}]}""", "outline is not a point")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0],"objects":[],"poly":[{"gx":"a","gz":0}]}""", "outline is not a point")]
    [InlineData("""{"w":1,"h":1,"rel":[0],"wt":[0],"pnt":[0,0,0,0],"objects":[],"poly":[{"gx":1e40,"gz":0}]}""", "outline is not a point")]
    [InlineData("""{"w":50000,"h":10}""", "size (50000 × 10) is too large")]
    public void ACopyThatDoesNotHoldTogetherIsRefused(string text, string why)
    {
        var e = Assert.Throws<FormatException>(() => CopyFormat.FromJson(JsonNode.Parse(text)!.AsObject()));
        Assert.Contains(why, e.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task SavedListedPastedExportedAndImported()
    {
        string dir = Path.Combine(Path.GetTempPath(), "vwe-bp-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var s = TestScenes.Flat(2);
            var w = TestWindows.Editing(s);
            var bp = w.Blueprints;
            bp.Store = new BlueprintStore(dir);
            string hs = Path.Combine(dir, "homestead");
            bp.FindHomestead = () => new Homestead.Status(true, "1.3.2", ["test"], hs);
            bp.AskDetails = (d, _) => Task.FromResult<Homestead.Details?>(d with { Name = "Gate" });
            bp.Confirm = _ => Task.FromResult(true);
            Assert.Null(await bp.Save());
            Assert.StartsWith("Copy something first", w.MessageText.Text);
            w.View.Paste.Clip = Sample();
            Assert.Equal("Gate", await bp.Save());
            // A Homestead blueprint with its picture; the copy's ground is not kept.
            var listed = Assert.Single(Homestead.List(hs));
            Assert.Equal(("Gate", 2), (listed.Name, listed.Pieces));
            Assert.NotNull(listed.Picture);
            Assert.Contains("ground shape is not kept", w.MessageText.Text);
            bp.Toggle(true);
            Assert.Single(bp.List.Children);
            // Pasting it: on the clipboard, in the Paste tool.
            w.View.Paste.Clip = null;
            bp.Paste("hs:Gate.blueprint");
            Assert.Equal("Gate", w.View.Paste.Clip!.Name);
            Assert.Equal(ToolMode.Paste, w.View.Mode);
            // As a .vbuild, and back in.
            string file = bp.Export("hs:Gate.blueprint", "vbuild")!;
            Assert.Contains("woodwall", File.ReadAllText(file));
            Assert.Equal("Gate (2)", bp.Import(file));
            Assert.Equal(2, Homestead.List(hs).Single(b => b.Name == "Gate (2)").Pieces);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
