using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game's models made ready for the view (BundleModels): Unity's left-handed matrices and meshes mirrored into the
/// view's space, materials judged as the old exporter did (glass and other see-through ones blended), and Unity's own
/// primitives (Cube, Quad, Plane, Cylinder, Sphere, Capsule) made here.
/// </summary>
public class BundleModelsTests
{
    [Fact]
    public void UnitysSpaceIsMirroredIntoTheViews()
    {
        // A move of (1, 2, 3) in Unity is (1, 2, -3) in the view.
        var m = BundleModels.ViewMatrix(GameBundles.Matrix.Trs(1, 2, 3, 0, 0, 0, 1, 1, 1, 1));
        Assert.Equal(new Vector3(1, 2, -3), Vector3.Transform(Vector3.Zero, m));
        // Turned 90° about y (Unity: x goes to -z), then mirrored: the view's x goes to its +z.
        float s = MathF.Sqrt(0.5f);
        var turn = BundleModels.ViewMatrix(GameBundles.Matrix.Trs(0, 0, 0, 0, s, 0, s, 1, 1, 1));
        var x = Vector3.Transform(Vector3.UnitX, turn);
        Assert.Equal(0, x.X, 4);
        Assert.Equal(1, x.Z, 4);
        // Meshes: z mirrored, an up normal when there is none, uv as they are.
        var mesh = BundleModels.ViewMesh(new GameBundles.Mesh("m", 1, [1f, 2f, 3f], null, [0.25f, 0.75f], [[0, 0, 0]]));
        Assert.Equal(new[] { 1f, 2f, -3f, 0f, 1f, 0f, 0.25f, 0.75f }, mesh.Vertices);
        Assert.Equal(new uint[] { 0, 0, 0 }, mesh.Submeshes[0]);
    }

    /// <summary>
    /// A material with this shader and, when given, _Mode, _Cull and _Color; with a texture (under the name slot gives)
    /// unless texture is false.
    /// </summary>
    private static GameBundles.Material Mat(string shader, float? mode = null, float? cull = null, float[]? color = null, bool texture = true, string slot = "_MainTex")
    {
        var floats = new Dictionary<string, float>();
        if (mode is float m)
        {
            floats["_Mode"] = m;
        }
        if (cull is float c)
        {
            floats["_Cull"] = c;
        }
        var texs = new Dictionary<string, GameBundles.TexEnv>();
        if (texture)
        {
            texs[slot] = new GameBundles.TexEnv(new GameBundles.Where("b", 5), 2, 3, 0.5f, 0.25f);
        }
        var colors = new Dictionary<string, float[]>();
        if (color != null)
        {
            colors["_Color"] = color;
        }
        return new GameBundles.Material("m", shader, null, texs, floats, colors);
    }

    [Fact]
    public void MaterialsAreJudgedAsTheExporterDid()
    {
        var plain = BundleModels.ViewMaterial(Mat("Standard", color: [0.5f, 1f, 0f, 0.8f]));
        Assert.Equal(0, plain.Cutoff);
        Assert.False(plain.DoubleSided);
        Assert.Equal("b:5", plain.Map);
        Assert.Equal(new Vector4(2, 3, 0.5f, 0.25f), plain.UvTransform);
        Assert.Equal(MathF.Pow(0.5f, 2.2f), plain.Color.X, 5);
        Assert.Equal(0.8f, plain.Color.W);
        // Vegetation: cut out at half, both sides, its texture bled ("|cut").
        var leaves = BundleModels.ViewMaterial(Mat("Custom/Vegetation"));
        Assert.Equal(0.5f, leaves.Cutoff);
        Assert.True(leaves.DoubleSided);
        Assert.Equal("b:5|cut", leaves.Map);
        Assert.False(plain.Transparent);
        // No culling: both sides.
        Assert.True(BundleModels.ViewMaterial(Mat("Standard", cull: 0)).DoubleSided);
        Assert.Null(BundleModels.ViewMaterial(Mat("Standard", texture: false)).Map);
        Assert.Equal(new GameBundles.Where("bundle:x", -12), GameBundles.Where.FromKey("bundle:x:-12"));
        Assert.Null(GameBundles.Where.FromKey("nothing"));
    }

    [Theory]
    [InlineData("Standard", 2f)]
    [InlineData("Standard", 3f)]
    [InlineData("Autodesk Interactive", 2f)]
    [InlineData("Legacy Shaders/Transparent/Diffuse", null)]
    [InlineData("FX/Glass/Stained BumpDistort", 2f)]
    [InlineData("Particles/Standard Unlit", null)]
    public void SeeThroughMaterialsAreBlendedNotCutOut(string shader, float? mode)
    {
        var glass = BundleModels.ViewMaterial(Mat(shader, mode));

        Assert.True(glass.Transparent);
        Assert.Equal(0, glass.Cutoff);
        // Its texture bled, for the edges of what it shows.
        Assert.Equal("b:5|cut", glass.Map);
    }

    [Theory]
    [InlineData("Custom/Vegetation", 3f, 0.5f)]
    [InlineData("Legacy Shaders/Transparent/Cutout/Bumped Diffuse", null, 0.5f)]
    [InlineData("Custom/Piece", 2f, 0.5f)]
    [InlineData("Custom/StaticRock", 3f, 0.5f)]
    [InlineData("Custom/Piece", null, 0f)]
    public void CutOutAndOpaqueMaterialsAreNotBlended(string shader, float? mode, float cutoff)
    {
        // Vegetation and cut-out shaders cut out whatever their mode; a mode left on a shader without modes (the game's
        // pieces and rocks keep one) cuts out at half, as before.
        var m = BundleModels.ViewMaterial(Mat(shader, mode));

        Assert.False(m.Transparent);
        Assert.Equal(cutoff, m.Cutoff);
    }

    [Theory]
    [InlineData("Standard", 3f, 0.02f, BundleModels.LeastAlpha)]
    [InlineData("Standard", 2f, 0.6f, 0.6f)]
    [InlineData("FX/Glass/Stained BumpDistort", 2f, 0.9f, BundleModels.LeastAlpha)]
    [InlineData("FX/Glass/Stained BumpDistort", 2f, 0f, BundleModels.LeastAlpha)]
    [InlineData("Custom/Glass", 0f, 0.6f, 0.6f)]
    [InlineData("Standard", 0f, 0.02f, 0.02f)]
    public void SeeThroughMaterialsAlphaIsKeptVisible(string shader, float mode, float given, float drawn)
    {
        // The game's crystal window has 0.38, some mods' glass 0 to 0.02: all but invisible blended, so at least a tint.
        // Refracting glass tints what is behind it; an opaque material's alpha is left alone.
        var m = BundleModels.ViewMaterial(Mat(shader, mode, color: [1f, 1f, 1f, given], texture: false));

        Assert.Equal(drawn, m.Color.W, 5);
        Assert.Null(m.Map);
    }

    [Theory]
    [InlineData("_MainTex")]
    [InlineData("_Albedo")]
    [InlineData("_BaseMap")]
    [InlineData("_BaseColorMap")]
    [InlineData("_MainTexture")]
    [InlineData("_Diffuse")]
    [InlineData("_BaseTexture")]
    public void TheMainTextureIsFoundUnderTheNamesShadersGiveIt(string slot)
    {
        var m = BundleModels.ViewMaterial(Mat("Custom/Thatch", slot: slot));

        Assert.Equal("b:5", m.Map);
        Assert.Equal(new Vector4(2, 3, 0.5f, 0.25f), m.UvTransform);
    }

    [Fact]
    public void TheFirstNameWithATextureIsTaken()
    {
        var two = Mat("Standard");
        two.Textures["_Albedo"] = new GameBundles.TexEnv(new GameBundles.Where("b", 7), 1, 1, 0, 0);
        two.Textures["_BumpMap"] = new GameBundles.TexEnv(new GameBundles.Where("b", 8), 1, 1, 0, 0);
        Assert.Equal("b:5", BundleModels.ViewMaterial(two).Map);
        // A slot named but empty: the next.
        var empty = Mat("Standard", slot: "_Albedo");
        empty.Textures["_MainTex"] = new GameBundles.TexEnv(null, 1, 1, 0, 0);
        Assert.Equal("b:5", BundleModels.ViewMaterial(empty).Map);
        Assert.Null(BundleModels.ViewMaterial(Mat("Standard", slot: "_BumpMap")).Map);
    }

    [Fact]
    public void UnitysCubeIsABoxSeenFromBothSides()
    {
        var cube = BundleModels.PrimitiveMesh("Cube")!;

        Assert.Equal(("Cube", 24), (cube.Name, cube.VertexCount));
        int[] tris = Assert.Single(cube.Submeshes);
        Assert.Equal(72, tris.Length);
        for (int v = 0; v < 24; v++)
        {
            var p = new Vector3(cube.Positions[v * 3], cube.Positions[v * 3 + 1], cube.Positions[v * 3 + 2]);
            var n = new Vector3(cube.Normals![v * 3], cube.Normals[v * 3 + 1], cube.Normals[v * 3 + 2]);
            // On the box's surface, its normal one of the axes, outward.
            Assert.Equal(0.5f, MathF.Max(MathF.Abs(p.X), MathF.Max(MathF.Abs(p.Y), MathF.Abs(p.Z))), 5);
            Assert.Equal(1, n.Length(), 5);
            Assert.Equal(1, MathF.Abs(n.X) + MathF.Abs(n.Y) + MathF.Abs(n.Z), 5);
            Assert.Equal(0.5f, Vector3.Dot(n, p), 5);
            Assert.InRange(cube.Uv![v * 2], 0, 1);
            Assert.InRange(cube.Uv[v * 2 + 1], 0, 1);
        }
        // Each triangle wound both ways.
        var both = Enumerable.Range(0, tris.Length / 3).Select(t => (tris[t * 3], tris[t * 3 + 1], tris[t * 3 + 2])).ToList();
        Assert.All(both, t => Assert.Contains((t.Item1, t.Item3, t.Item2), both));
    }

    [Theory]
    [InlineData("Quad", 2, 0, 0, -1)]
    [InlineData("Plane", 1, 0, 1, 0)]
    public void UnitysQuadAndPlaneAreFlatSquares(string name, int flat, float nx, float ny, float nz)
    {
        var mesh = BundleModels.PrimitiveMesh(name)!;

        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(12, Assert.Single(mesh.Submeshes).Length);
        float size = name == "Quad" ? 0.5f : 5;
        for (int v = 0; v < 4; v++)
        {
            Assert.Equal(0, mesh.Positions[v * 3 + flat]);
            Assert.All(Enumerable.Range(0, 3).Where(a => a != flat), a => Assert.Equal(size, MathF.Abs(mesh.Positions[v * 3 + a])));
            Assert.Equal(new Vector3(nx, ny, nz), new Vector3(mesh.Normals![v * 3], mesh.Normals[v * 3 + 1], mesh.Normals[v * 3 + 2]));
        }
        Assert.Equal([0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f], mesh.Uv!);
    }

    [Theory]
    [InlineData("Cylinder", 150, 864, 1f)]
    [InlineData("Sphere", 425, 4608, 0.5f)]
    [InlineData("Capsule", 450, 4896, 1f)]
    public void UnitysRoundPrimitivesAreClosedFacingOutAndSeenFromBothSides(string name, int vertices, int indices, float half)
    {
        var mesh = BundleModels.PrimitiveMesh(name)!;

        Assert.Equal((name, vertices), (mesh.Name, mesh.VertexCount));
        int[] tris = Assert.Single(mesh.Submeshes);
        Assert.Equal(indices, tris.Length);
        Vector3 min = new(float.MaxValue), max = new(float.MinValue);
        for (int v = 0; v < vertices; v++)
        {
            var p = new Vector3(mesh.Positions[v * 3], mesh.Positions[v * 3 + 1], mesh.Positions[v * 3 + 2]);
            var n = new Vector3(mesh.Normals![v * 3], mesh.Normals[v * 3 + 1], mesh.Normals[v * 3 + 2]);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);
            // On the surface: half a metre from the axis (or inside an end's disc), from the centre (the sphere), or from
            // the segment between the ends' centres (the capsule).
            float r = MathF.Sqrt(p.X * p.X + p.Z * p.Z);
            switch (name)
            {
                case "Cylinder":
                    Assert.True(MathF.Abs(r - 0.5f) < 1e-5f || MathF.Abs(MathF.Abs(p.Y) - 1) < 1e-5f && r <= 0.5f + 1e-5f, $"{p} is not on the cylinder");
                    break;
                case "Sphere":
                    Assert.Equal(0.5f, p.Length(), 5);
                    break;
                default:
                    Assert.Equal(0.5f, (p - new Vector3(0, Math.Clamp(p.Y, -0.5f, 0.5f), 0)).Length(), 5);
                    break;
            }
            // Outward (each is convex around its centre), of unit length; uv from 0 to 1.
            Assert.Equal(1, n.Length(), 5);
            Assert.True(Vector3.Dot(n, p) > 0, $"the normal at {p} points in");
            Assert.InRange(mesh.Uv![v * 2], 0, 1);
            Assert.InRange(mesh.Uv[v * 2 + 1], 0, 1);
        }
        // Unity's bounds: half a metre across, as high as told.
        Assert.True(Vector3.Distance(new Vector3(-0.5f, -half, -0.5f), min) < 1e-5f, $"lowest corner {min}");
        Assert.True(Vector3.Distance(new Vector3(0.5f, half, 0.5f), max) < 1e-5f, $"highest corner {max}");
        var both = Enumerable.Range(0, tris.Length / 3).Select(t => (tris[t * 3], tris[t * 3 + 1], tris[t * 3 + 2])).ToHashSet();
        Assert.All(both, t => Assert.Contains((t.Item1, t.Item3, t.Item2), both));
    }

    [Fact]
    public void AnyOtherPrimitiveIsNotMade()
    {
        Assert.Null(BundleModels.PrimitiveMesh("Torus"));
    }
}
