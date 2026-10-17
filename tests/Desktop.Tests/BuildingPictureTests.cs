using System.Numerics;
using SkiaSharp;
using TerrainEditor.App;
using TerrainEditor.Terrain;
using WorldEditor.Tests;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// A blueprint's picture (BuildingPicture) without the game's models: its pieces' boxes, filling the middle and leaving
/// the corners clear; the equality of its triangles (NaN and -0 included); and what a building costs in game
/// (PieceCost).
/// </summary>
public class BuildingPictureTests
{
    private static readonly List<BuildingPicture.Piece> Hut =
    [
        new("wood_floor", new Vector3(0, 0, 0), Vector3.Zero, 0),
        new("woodwall", new Vector3(0, 1, -1), Vector3.Zero, 0),
        new("woodwall", new Vector3(1, 1, 0), new Vector3(0, 90, 0), 0),
        new("no_such_piece_xyz", new Vector3(-1, 0, 0), Vector3.Zero, 0),
    ];

    [Fact]
    public void TheBuildingFillsThePictureAndItsCornersAreClear()
    {
        byte[] png = BuildingPicture.Draw(Hut, null, 128);
        using var bmp = SKBitmap.Decode(png);
        Assert.Equal((128, 128), (bmp.Width, bmp.Height));
        Assert.Equal(0, bmp.GetPixel(2, 2).Alpha);
        Assert.Equal(0, bmp.GetPixel(125, 125).Alpha);
        int opaque = 0;
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                opaque += bmp.GetPixel(x, y).Alpha > 200 ? 1 : 0;
            }
        }
        Assert.InRange(opaque, 128 * 128 / 8, 128 * 128 * 3 / 4);
        // Lit from one side: not all one colour.
        Assert.True(Enumerable.Range(0, 128).Select(x => bmp.GetPixel(x, 64)).Where(c => c.Alpha > 200).Distinct().Count() > 2);
        // Nothing to draw: a clear picture.
        using var none = SKBitmap.Decode(BuildingPicture.Draw([], null, 32));
        Assert.Equal(0, none.GetPixel(16, 16).Alpha);
    }

    // With the game's models (copied on this computer): written to look at, when asked.
    [Fact]
    public void WithTheGamesModelsWhenThereAreSome()
    {
        string? dir = Environment.GetEnvironmentVariable("VWE_TEST_MODELS"), outFile = Environment.GetEnvironmentVariable("VWE_TEST_PICTURE");
        if (dir == null || outFile == null || !File.Exists(Path.Combine(dir, "meshinfo.json")))
        {
            return;
        }
        var models = new ModelStore(dir);
        var pieces = Hut.ToList();
        if (Environment.GetEnvironmentVariable("VWE_TEST_BLUEPRINT") is string bp)
        {
            var parsed = BlueprintFormats.Parse(bp, File.ReadAllText(bp));
            pieces = [.. parsed.Pieces.Select(p => new BuildingPicture.Piece(p.Name, p.Position, p.Euler, p.Scale))];
        }
        File.WriteAllBytes(outFile, BuildingPicture.Draw(pieces, models));
        File.WriteAllBytes(Path.ChangeExtension(outFile, ".boxes.png"), BuildingPicture.Draw(pieces, null));
    }

    /// <summary>
    /// A triangle with every field set, its texture this one.
    /// </summary>
    private static BuildingPicture.Tri Triangle(ModelStore.ImageData? texture, float odd = 0.5f)
    {
        return new BuildingPicture.Tri
        {
            A = new Vector3(1, 2, 3),
            B = new Vector3(4, 5, 6),
            C = new Vector3(7, 8, odd),
            Ua = new Vector2(0, 0),
            Ub = new Vector2(1, 0),
            Uc = new Vector2(odd, 1),
            Colour = new Vector3(200, 100, 50),
            Texture = texture,
            UvTransform = new Vector4(1, 1, 0, odd),
            Cutoff = odd,
            Alpha = odd,
        };
    }

    /// <summary>
    /// Each field of a triangle changed alone, by name.
    /// </summary>
    private static readonly Dictionary<string, Func<BuildingPicture.Tri, BuildingPicture.Tri>> Changes = new()
    {
        ["A"] = t => { t.A = Vector3.Zero; return t; },
        ["B"] = t => { t.B = Vector3.Zero; return t; },
        ["C"] = t => { t.C = Vector3.Zero; return t; },
        ["Ua"] = t => { t.Ua = Vector2.One; return t; },
        ["Ub"] = t => { t.Ub = Vector2.One; return t; },
        ["Uc"] = t => { t.Uc = Vector2.Zero; return t; },
        ["Colour"] = t => { t.Colour = Vector3.Zero; return t; },
        ["Texture"] = t => { t.Texture = null; return t; },
        ["UvTransform"] = t => { t.UvTransform = Vector4.Zero; return t; },
        ["Cutoff"] = t => { t.Cutoff = 1; return t; },
        ["Alpha"] = t => { t.Alpha = 1; return t; },
    };

    /// <summary>
    /// The names of the triangle's fields.
    /// </summary>
    public static TheoryData<string> TriangleFields => [.. Changes.Keys];

    // Equal field by field, NaN equal to NaN and -0 to 0; the texture by its record equality, so the same pixels in
    // another array are another texture.
    [Theory]
    [InlineData(float.NaN, float.NaN)]
    [InlineData(-0f, 0f)]
    [InlineData(0.5f, 0.5f)]
    public void TrianglesWithTheSameFieldsAreEqual(float odd, float twin)
    {
        byte[] pixels = [1, 2, 3, 4];
        EqualityAsserts.Same(Triangle(new(1, 1, pixels), odd), Triangle(new(1, 1, pixels), twin));
        EqualityAsserts.Differ(Triangle(new(1, 1, pixels), odd), Triangle(new(1, 1, [1, 2, 3, 4]), twin));
    }

    [Theory]
    [MemberData(nameof(TriangleFields))]
    public void TrianglesDifferingInOneFieldAreNotEqual(string field)
    {
        var t = Triangle(new(1, 1, [1, 2, 3, 4]));
        EqualityAsserts.Differ(t, Changes[field](t));
    }

    [Fact]
    public void ABuildingCostsItsPiecesResources()
    {
        var cost = PieceCost.Of(["woodwall", "woodwall", "wood_floor", "stone_wall_2x1", "no_such_piece_xyz"]);
        Assert.Equal(("Wood", "Wood", 6), cost.Materials[0]);
        Assert.Contains(cost.Materials, m => m.Item == "Stone" && m.Amount == 4);
        Assert.Equal(new[] { "Stonecutter", "Workbench" }, cost.Stations);
        Assert.Equal(1, cost.Unknown["no_such_piece_xyz"]);
        Assert.Equal("Wood 6 · Stone 4 (needs Stonecutter, Workbench)", cost.Describe());
        Assert.Equal("Wood Wall", PieceCost.PieceName("woodwall"));
        Assert.Equal("Free", PieceCost.Of(Array.Empty<string>()).Describe());
        Assert.Equal("Cost unknown (pieces from mods?)", PieceCost.Of(["no_such_piece_xyz"]).Describe());
    }
}
