using System.Numerics;
using SkiaSharp;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A building's picture drawn with models (a made-up textured cube, not the game's): the model's texture shows instead
/// of the piece's boxes, a part pointing at a submesh the mesh lacks is skipped, a flat triangle draws nothing, a
/// piece without a model is drawn as boxes, a see-through material (glass) is blended over what is behind it, and a
/// mesh wound both ways (Unity's primitives) is drawn once.
/// </summary>
public sealed class BuildingPictureModelsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vwe-picture-models-" + Guid.NewGuid().ToString("N")[..8]);

    /// <summary>
    /// A store holding "woodwall" as a 2 m cube with a green texture, and a part on a submesh the mesh does not have;
    /// "glasscube" as the same cube of white glass, 0.4 opaque.
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
        File.WriteAllText(Path.Combine(_dir, "materials.json"), "{\"green\": {\"color\": [1, 1, 1, 1], \"map\": \"green.png\", \"cutoff\": 0.5},"
            + " \"glass\": {\"color\": [1, 1, 1, 0.4], \"transparent\": true}}");
        File.WriteAllText(Path.Combine(_dir, "objects.json"), "{}");
        File.WriteAllText(Path.Combine(_dir, "pieces", "glasscube.json"),
            "{\"parts\": [{\"m\": [1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1], \"mesh\": \"cube\", \"sub\": 0, \"mat\": \"glass\"}]}");
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

    [Fact]
    public void ASeeThroughMaterialIsBlendedOverWhatIsBehindIt()
    {
        var store = new ModelStore(_dir);

        // Alone: drawn, but never opaque (its front and back faces over the clear background).
        using var alone = SKBitmap.Decode(BuildingPicture.Draw([new BuildingPicture.Piece("glasscube", Vector3.Zero, Vector3.Zero, 0)], store));
        var glass = alone.Pixels.Where(p => p.Alpha > 0).ToList();
        Assert.NotEmpty(glass);
        Assert.All(glass, p => Assert.True(p.Alpha < 230, $"{p} is opaque"));
        // Around the green wall: the wall shows through, whitened by the glass in front of it.
        var over = Pixels(BuildingPicture.Draw(
            [new BuildingPicture.Piece("woodwall", Vector3.Zero, new Vector3(0, 30, 0), 0), new BuildingPicture.Piece("glasscube", Vector3.Zero, new Vector3(0, 30, 0), 1.2f)], store));
        Assert.NotEmpty(over);
        Assert.All(over, p => Assert.True(p.Green > p.Red, $"{p} is not green"));
        Assert.Contains(over, p => p.Red > 40);
    }

    /// <summary>A glass box made in code: Unity's cube (each triangle wound both ways), 2 m, of glass 0.4 opaque.</summary>
    private sealed class GlassBox : ModelStore.ISource
    {
        public string? ObjectName(int prefab) => null;

        public ModelStore.Model? Model(string name) => new(name, [new ModelStore.Part("cube", 0, "glass", Matrix4x4.CreateScale(2))], Vector3.One);

        public ModelStore.MeshData? Mesh(string id) => BundleModels.ViewMesh(BundleModels.PrimitiveMesh("Cube")!) with { BothWays = true };

        public ModelStore.MaterialData Material(string id) => new(new Vector4(1, 1, 1, 0.4f), null, 0, true, new Vector4(1, 1, 0, 0), true);

        public ModelStore.ImageData? Texture(string id) => null;
    }

    [Fact]
    public void AMeshWoundBothWaysIsDrawnOnce()
    {
        using var picture = SKBitmap.Decode(BuildingPicture.Draw([new BuildingPicture.Piece("glassbox", Vector3.Zero, Vector3.Zero, 0)], new ModelStore(new GlassBox())));

        // Its near and far sides once each: 1 - 0.6² = 0.64 opaque at most, not 1 - 0.6⁴ = 0.87.
        Assert.InRange(picture.Pixels.Max(p => p.Alpha), 150, 190);
    }
}
