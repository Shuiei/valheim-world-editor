using TerrainEditor.App;
using Xunit;

namespace WorldEditor.Tests;

/// <summary>
/// The deferred pass's fragment variants in the shader text (GameShader.FragmentVariants): each is keyed by its prefix,
/// whether it samples the full paint mask, its distinct G-buffer target count and whether it has fog, and the first
/// variant with a key is the one kept.
/// </summary>
public class GameShaderFragmentVariantTests
{
    private const string Terrain = "_DiffuseArrayTex\n";

    private static Dictionary<(string, bool, int, bool), string> Variants(string text) =>
        (Dictionary<(string, bool, int, bool), string>)PrivateCall.Static(typeof(GameShader), "FragmentVariants", text)!;

    /// <summary>A fragment variant that samples the terrain textures, with the given lines in its main body.</summary>
    private static string Variant(string body, string extra = "") =>
        $"#ifdef FRAGMENT\n{Terrain}{extra}void main()\n{{\n{body}\n}}\n#endif\n";

    [Fact]
    public void AVariantWithoutTheTerrainTexturesIsSkipped()
    {
        Assert.Empty(Variants("#ifdef FRAGMENT\nvoid main()\n{\nSV_Target0 = a;\n}\n#endif\n"));
    }

    [Fact]
    public void AVariantIsKeyedByItsPrefixAndItsTargetCount()
    {
        var keep = Variants(Variant("SV_Target0 = a;\nSV_Target1 = b;"));
        Assert.Contains(("vs", false, 2, false), keep.Keys);
    }

    [Fact]
    public void AVariantReadingTheDeferredTexcoordIsKeyedAsDeferred()
    {
        var keep = Variants(Variant("SV_Target0 = ds_TEXCOORD0;"));
        Assert.Contains(("ds", false, 1, false), keep.Keys);
    }

    [Fact]
    public void ARepeatedTargetCountsOnce()
    {
        var keep = Variants(Variant("SV_Target0 = a;\nSV_Target0 = b;\nSV_Target2 = c;"));
        Assert.Contains(("vs", false, 2, false), keep.Keys);
    }

    [Fact]
    public void AVariantThatSamplesTheFullPaintMaskIsKeyedAsSuch()
    {
        var keep = Variants(Variant("SV_Target0 = texture(_ClearedMaskTex, vs_TEXCOORD0.xy);"));
        Assert.Contains(("vs", true, 1, false), keep.Keys);
    }

    [Fact]
    public void AVariantWithFogParametersIsKeyedAsFoggy()
    {
        var keep = Variants(Variant("SV_Target0 = a;", extra: "uniform vec4 unity_FogParams;\n"));
        Assert.Contains(("vs", false, 1, true), keep.Keys);
    }

    [Fact]
    public void TheFirstVariantWithAKeyIsTheOneKept()
    {
        string first = Variant("SV_Target0 = first;");
        string second = Variant("SV_Target0 = second;");
        var keep = Variants(first + second);
        Assert.Single(keep);
        Assert.Contains("SV_Target0 = first;", keep[("vs", false, 1, false)]);
    }
}
