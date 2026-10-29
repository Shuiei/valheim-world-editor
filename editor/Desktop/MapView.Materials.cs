using Avalonia.OpenGL.Controls;

namespace TerrainEditor.Desktop;

/// <summary>
/// The map shader's material values: the game's "minimap shader" material and clear-midday lighting, made linear.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// Material values from the game's "minimap shader" material, and clear-midday lighting. sRGB colours, made linear
    /// when set.
    /// </summary>
    private static readonly (string Name, float[] V)[] Material =
    [
        ("_ForestColor", new[] { 0.9191f, 0.8218f, 0.6758f, 1 }), ("_WaterColor", new[] { 0.8429f, 0.9373f, 1.0917f, 1 }), ("_WaterColorDeep", new[] { 0.343f, 0.5337f, 0.7647f, 1 }),
        ("_WaterColorAshlands", new[] { 0.2078f, 0.2078f, 0.2078f, 1 }), ("_WaterColorAshlandsDeep", new[] { 0.2235f, 0.251f, 0.3686f, 1 }),
        ("_lightColor", new[] { 1.2f, 1.2f, 1.2f }), ("_ambientLightColor", new[] { 0.8088f, 0.8418f, 1.0f }), ("_lavaColor1", new[] { 0.4157f, 0.0235f, 0.0f }), ("_lavaColor2", new[] { 4.5126f, 0.2599f, 0.0f }),
        ("_SunColor", new[] { 1.0f, 0.97f, 0.9f, 1 }), ("_AmbientColor", new[] { 0.36f, 0.38f, 0.44f, 1 }), ("_SunFogColor", new[] { 0.9f, 0.9f, 1, 1 }),
    ];

    /// <summary><see cref="Material"/> as set: its colours (the first three values) made linear once.</summary>
    private static readonly (string Name, float[] V)[] MaterialLinear = [.. Material.Select(m => (m.Name, m.V.Select((x, i) => i < 3 ? ToLinear(x) : x).ToArray()))];

    /// <summary>
    /// One sRGB channel (0..1) to linear.
    /// </summary>
    private static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);

    /// <summary>Sets the map shader's material values: the game's minimap material, in the shader's program.</summary>
    private void SetMaterials()
    {
        foreach (var (name, c) in MaterialLinear)
        {
            int loc = _u.At(_prog, name);
            if (loc < 0)
            {
                continue;
            }
            if (c.Length == 3)
            {
                _gl.Uniform3(loc, c[0], c[1], c[2]);
            }
            else
            {
                _gl.Uniform4(loc, c[0], c[1], c[2], c[3]);
            }
        }
    }
}
