using System.Numerics;
using System.Text.Json.Nodes;
using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A building's picture in its rarer cases: a copy with no objects, objects that are not objects or have no name, or
/// lack their numbers; and a model scaled by its piece, with a sliver of a triangle seen edge-on (nothing to fill).
/// </summary>
public sealed class BuildingPictureBranchesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-picture-branches-" + Guid.NewGuid().ToString("N")[..8]);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void AClipsObjectsAreReadAsTheyCome()
    {
        JsonObject none = [];
        Assert.Empty(BuildingPicture.FromClip(none));
        var clip = JsonNode.Parse("""{"objects":[3,{"dx":1},{"name":"woodwall"},{"name":"wood_floor","dx":1.5,"dy":2,"dz":-1,"rx":0,"ry":90,"rz":0,"scale":2}]}""")!.AsObject();
        var pieces = BuildingPicture.FromClip(clip);
        Assert.Equal(2, pieces.Count);
        Assert.Equal(new BuildingPicture.Piece("woodwall", Vector3.Zero, Vector3.Zero, 0), pieces[0]);
        Assert.Equal(new BuildingPicture.Piece("wood_floor", new Vector3(1.5f, 2, -1), new Vector3(0, 90, 0), 2), pieces[1]);
    }

    [Fact]
    public void AScaledModelIsDrawnAndATriangleSeenEdgeOnIsSkipped()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "pieces"));
        Directory.CreateDirectory(Path.Combine(_dir, "meshes"));
        Directory.CreateDirectory(Path.Combine(_dir, "tex"));
        // A unit square facing up, and a sliver whose long side points at the picture's eye (south-east, above).
        var eye = Vector3.Normalize(new Vector3(1f, 0.8f, 1.2f));
        Vector3[] corners = [new(-1, 0, -1), new(1, 0, -1), new(1, 0, 1), new(-1, 0, 1), Vector3.Zero, eye, new(0, 1e-5f, 0)];
        var verts = new List<float>();
        foreach (var c in corners)
        {
            verts.AddRange([c.X, c.Y, c.Z, 0, 1, 0, 0.5f, 0.5f]);
        }
        uint[] faces = [0, 1, 2, 0, 2, 3, 4, 5, 6];
        byte[] bytes = new byte[verts.Count * 4 + faces.Length * 4];
        Buffer.BlockCopy(verts.ToArray(), 0, bytes, 0, verts.Count * 4);
        Buffer.BlockCopy(faces, 0, bytes, verts.Count * 4, faces.Length * 4);
        File.WriteAllBytes(Path.Combine(_dir, "meshes", "square.bin"), bytes);
        File.WriteAllText(Path.Combine(_dir, "meshinfo.json"), $"{{\"square\": {{\"v\": {corners.Length}, \"sub\": [{faces.Length}]}}}}");
        File.WriteAllText(Path.Combine(_dir, "materials.json"), "{\"green\": {\"color\": [1, 1, 1, 1], \"map\": \"green.png\", \"cutoff\": 0.5}}");
        File.WriteAllText(Path.Combine(_dir, "objects.json"), "{}");
        File.WriteAllText(Path.Combine(_dir, "pieces", "wood_floor.json"),
            "{\"parts\": [{\"m\": [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1], \"mesh\": \"square\", \"sub\": 0, \"mat\": \"green\"}]}");
        using (var bmp = new SKBitmap(2, 2))
        {
            bmp.Erase(new SKColor(0, 200, 0));
            using var f = File.Create(Path.Combine(_dir, "tex", "green.png"));
            bmp.Encode(f, SKEncodedImageFormat.Png, 100);
        }
        var store = new ModelStore(_dir);
        using var drawn = SKBitmap.Decode(BuildingPicture.Draw([new BuildingPicture.Piece("wood_floor", Vector3.Zero, Vector3.Zero, 2)], store, 64));
        // The square's texture, green.
        var opaque = drawn.Pixels.Where(p => p.Alpha > 200).ToList();
        Assert.NotEmpty(opaque);
        Assert.All(opaque, p => Assert.True(p.Green > p.Red && p.Green > p.Blue, $"{p} is not green"));
    }
}
