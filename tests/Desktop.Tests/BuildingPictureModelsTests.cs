using System.Numerics;
using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A building's picture drawn with models (a made-up textured cube, not the game's): the model's texture shows instead
/// of the piece's boxes, a part pointing at a submesh the mesh lacks is skipped, a flat triangle draws nothing, and a
/// piece without a model is drawn as boxes.
/// </summary>
public sealed class BuildingPictureModelsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-picture-models-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// A store holding "woodwall" as a 2 m cube with a green texture, and a part on a submesh the mesh does not have.
    /// </summary>
    public BuildingPictureModelsTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "pieces"));
        Directory.CreateDirectory(Path.Combine(_dir, "meshes"));
        Directory.CreateDirectory(Path.Combine(_dir, "tex"));
        var verts = new List<float>();
        for (int i = 0; i < 8; i++)
        {
            // Corner i of the cube: position, a normal, uv.
            verts.AddRange([(i & 1) * 2 - 1, (i >> 1 & 1) * 2, (i >> 2 & 1) * 2 - 1, 0, 1, 0, i & 1, i >> 1 & 1]);
        }
        uint[] faces =
        [
            0, 1, 3, 0, 3, 2, 4, 6, 7, 4, 7, 5, 0, 4, 5, 0, 5, 1, 2, 3, 7, 2, 7, 6, 0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3,
            // A flat triangle: nothing to fill.
            0, 0, 1,
        ];
        byte[] bytes = new byte[verts.Count * 4 + faces.Length * 4];
        Buffer.BlockCopy(verts.ToArray(), 0, bytes, 0, verts.Count * 4);
        Buffer.BlockCopy(faces, 0, bytes, verts.Count * 4, faces.Length * 4);
        File.WriteAllBytes(Path.Combine(_dir, "meshes", "cube.bin"), bytes);
        File.WriteAllText(Path.Combine(_dir, "meshinfo.json"), $"{{\"cube\": {{\"v\": 8, \"sub\": [{faces.Length}]}}}}");
        File.WriteAllText(Path.Combine(_dir, "materials.json"), "{\"green\": {\"color\": [1, 1, 1, 1], \"map\": \"green.png\", \"cutoff\": 0.5}}");
        File.WriteAllText(Path.Combine(_dir, "objects.json"), "{}");
        File.WriteAllText(Path.Combine(_dir, "pieces", "woodwall.json"),
            "{\"parts\": [{\"m\": [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1], \"mesh\": \"cube\", \"sub\": 0, \"mat\": \"green\"},"
            + " {\"m\": [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1], \"mesh\": \"cube\", \"sub\": 3, \"mat\": \"green\"}]}");
        using var bmp = new SKBitmap(2, 2);
        bmp.Erase(new SKColor(0, 200, 0));
        using var f = File.Create(Path.Combine(_dir, "tex", "green.png"));
        bmp.Encode(f, SKEncodedImageFormat.Png, 100);
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>The picture's opaque pixels, as colours.</summary>
    private static List<SKColor> Pixels(byte[] png)
    {
        using var bmp = SKBitmap.Decode(png);
        return [.. bmp.Pixels.Where(p => p.Alpha > 200)];
    }

    [Fact]
    public void TheModelsTextureShowsInsteadOfTheBoxes()
    {
        var store = new ModelStore(_dir);
        BuildingPicture.Piece[] wall = [new("woodwall", Vector3.Zero, new Vector3(0, 30, 0), 0)];
        var drawn = Pixels(BuildingPicture.Draw(wall, store));
        Assert.NotEmpty(drawn);
        // Green from the texture, shaded: never the wood boxes' brown.
        Assert.All(drawn, p => Assert.True(p.Green > p.Red && p.Green > p.Blue, $"{p} is not green"));
        var boxes = Pixels(BuildingPicture.Draw(wall, null));
        Assert.Contains(boxes, p => p.Red > p.Green);
        // A piece the store has no model for: its boxes.
        BuildingPicture.Piece[] floor = [new("wood_floor", Vector3.Zero, Vector3.Zero, 1)];
        Assert.Equal(Pixels(BuildingPicture.Draw(floor, null)).Count, Pixels(BuildingPicture.Draw(floor, store)).Count);
    }
}
