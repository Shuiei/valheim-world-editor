using System.Numerics;
using TerrainEditor.App;
using Xunit;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game's models made ready for the view (BundleModels): Unity's left-handed matrices and meshes mirrored into the
/// view's space, and materials judged as the old exporter did.
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
}
