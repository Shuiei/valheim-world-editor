using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>How a material is drawn (BundleModels.ViewMaterial.cs): its colour, cut-out and see-through modes, and its main texture.</summary>
internal sealed partial class BundleModels
{
    /// <summary>
    /// How a material is drawn: cut out (Unity Standard's _Mode 1, cut-out shaders, vegetation, which is also seen from
    /// both sides), see-through (Standard's fade and transparent modes, _Mode 2 and 3, and transparent, glass and
    /// particle shaders: blended by an alpha no lower than <see cref="LeastAlpha"/>, that alpha for refracting glass
    /// (a glass shader that distorts what is behind it)), else opaque. A _Mode 2 or 3 left on a shader that has no such
    /// modes (the game's own pieces and rocks keep one) cuts out at half, as the exporter did.
    /// </summary>
    internal static ModelStore.MaterialData ViewMaterial(GameBundles.Material m)
    {
        string shader = m.Shader;
        bool vegetation = shader.Contains("Vegetation", StringComparison.Ordinal);
        float mode = m.Floats.GetValueOrDefault("_Mode", 0);
        var (cut, seeThrough, halfCut) = Modes(shader, mode, vegetation);
        float cutoff = Cutoff(m, cut, halfCut);
        bool doubleSided = m.Floats.GetValueOrDefault("_Cull", 2) == 0 || vegetation;
        float[] col = m.Colors.GetValueOrDefault("_Color") ?? [1f, 1f, 1f, 1f];
        float alpha = Alpha(col[3], shader, seeThrough);
        var color = new Vector4(ModelStore.Linear(col[0]), ModelStore.Linear(col[1]), ModelStore.Linear(col[2]), alpha);
        var (map, uv) = MainTexture(m, cut || seeThrough || halfCut);
        return new ModelStore.MaterialData(color, map, cutoff, doubleSided, uv, seeThrough);
    }

    /// <summary>The main texture's name and its uv transform (scale, then offset), the first of <see cref="MainTextures"/> that the material has.</summary>
    /// <param name="m">The material.</param>
    /// <param name="bleed">Whether the texture is a cut-out one (its name gets "|cut").</param>
    /// <returns>The texture's id (null: none) and the uv transform (1, 1, 0, 0 when none).</returns>
    internal static (string? Map, Vector4 Uv) MainTexture(GameBundles.Material m, bool bleed)
    {
        if (MainTextures.Select(m.Textures.GetValueOrDefault).FirstOrDefault(t => t?.Texture != null) is { Texture: { } tex } main)
        {
            return (tex.Key + (bleed ? "|cut" : ""), new Vector4(main.ScaleX, main.ScaleY, main.OffsetX, main.OffsetY));
        }
        return (null, new Vector4(1, 1, 0, 0));
    }

    /// <summary>The cut-out threshold of a material: its _Cutoff (0.5 when it has none), 0.5 for a half cut, else 0.</summary>
    /// <param name="m">The material.</param>
    /// <param name="cut">Whether it is cut out.</param>
    /// <param name="halfCut">Whether it is cut at half.</param>
    internal static float Cutoff(GameBundles.Material m, bool cut, bool halfCut) =>
        cut ? (m.Floats.GetValueOrDefault("_Cutoff", 0.5f) is float c and not 0 ? MathF.Round(c, 3) : 0.5f) : halfCut ? 0.5f : 0f;

    /// <summary>The alpha a material is drawn with: a see-through one is at least <see cref="LeastAlpha"/>, a refracting glass one exactly that.</summary>
    /// <param name="alpha">The colour's alpha.</param>
    /// <param name="shader">The shader's name.</param>
    /// <param name="seeThrough">Whether the material is see-through.</param>
    internal static float Alpha(float alpha, string shader, bool seeThrough)
    {
        if (!seeThrough)
        {
            return alpha;
        }
        bool glass = shader.Contains("Glass", StringComparison.OrdinalIgnoreCase);
        return glass && shader.Contains("Distort", StringComparison.OrdinalIgnoreCase) ? LeastAlpha : Math.Max(alpha, LeastAlpha);
    }
}
