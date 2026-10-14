using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game's models made ready for the view (BundleModels): Unity's left-handed matrices and meshes mirrored into the
/// view's space, materials judged as the old exporter did, and Unity's own primitives (Cube, Quad, Plane) made here.
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
    /// A material with this shader and, when given, _Mode, _Cull and _Color; with a _MainTex unless texture is false.
    /// </summary>
    private static GameBundles.Material Mat(string shader, float? mode = null, float? cull = null, float[]? color = null, bool texture = true)
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
            texs["_MainTex"] = new GameBundles.TexEnv(new GameBundles.Where("b", 5), 2, 3, 0.5f, 0.25f);
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
        // Transparent (Standard's fade mode): drawn cut out at half; no culling: both sides.
        Assert.Equal(0.5f, BundleModels.ViewMaterial(Mat("Standard", mode: 3)).Cutoff);
        Assert.True(BundleModels.ViewMaterial(Mat("Standard", cull: 0)).DoubleSided);
        Assert.Null(BundleModels.ViewMaterial(Mat("Standard", texture: false)).Map);
        Assert.Equal(new GameBundles.Where("bundle:x", -12), GameBundles.Where.FromKey("bundle:x:-12"));
        Assert.Null(GameBundles.Where.FromKey("nothing"));
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

    [Fact]
    public void AnyOtherPrimitiveIsNotMade()
    {
        Assert.Null(BundleModels.PrimitiveMesh("Sphere"));
    }
}
