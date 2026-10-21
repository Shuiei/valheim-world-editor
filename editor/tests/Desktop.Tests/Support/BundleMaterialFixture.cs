using TerrainEditor.App;

namespace TerrainEditor.Desktop.Tests;

/// <summary>
/// The game's materials as the bundle tests build them: a material of a shader with its colour, mode, culling and main
/// texture as the test gives them.
/// </summary>
internal static class BundleMaterialFixture
{
    /// <summary>
    /// A material with this shader and, when given, _Mode, _Cull and _Color; with a texture (under the name slot gives)
    /// unless texture is false.
    /// </summary>
    internal static GameBundles.Material Mat(string shader, float? mode = null, float? cull = null, float[]? color = null, bool texture = true, string slot = "_MainTex")
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
}
