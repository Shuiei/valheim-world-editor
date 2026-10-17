using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>Materials and textures by id (BundleModels.Looks.cs), and what marks a material as see-through.</summary>
internal sealed partial class BundleModels
{
    /// <summary>
    /// The least alpha a see-through material is drawn with, and refracting glass's: glass the game draws all but clear
    /// still shows, as a tint; refracting glass tints what is behind it (by its colour and texture) instead of covering
    /// it, whatever its colour's alpha.
    /// </summary>
    internal const float LeastAlpha = 0.3f;

    /// <summary>
    /// The names a material's main texture is kept under, in the order looked for: Unity Standard's, then those of other
    /// shaders mods use (URP, HDRP, their own).
    /// </summary>
    internal static readonly string[] MainTextures = ["_MainTex", "_Albedo", "_BaseMap", "_BaseColorMap", "_MainTexture", "_Diffuse", "_BaseTexture"];

    /// <summary>A material by its id (see <see cref="ViewMaterial"/>); a plain white one when it cannot be read.</summary>
    public ModelStore.MaterialData Material(string id)
    {
        var none = new ModelStore.MaterialData(Vector4.One, null, 0, false, new Vector4(1, 1, 0, 0));
        if (Source(id) is not var (game, key) || GameBundles.Where.FromKey(key) is not { } at || game.LoadMaterial(at) is not { } m)
        {
            return none;
        }
        var material = ViewMaterial(m);
        // A mod's textures are in its bundles too.
        return id.StartsWith(Mod) && material.Map != null ? material with { Map = Mod + material.Map } : material;
    }

    /// <summary>
    /// A texture by its id, RGBA no larger than <see cref="TextureMax"/>; a "|cut" one has its edges bled. Null when
    /// it cannot be read.
    /// </summary>
    public ModelStore.ImageData? Texture(string id)
    {
        bool cut = id.EndsWith("|cut", StringComparison.Ordinal);
        if (Source(cut ? id[..^4] : id) is not var (game, key) || GameBundles.Where.FromKey(key) is not { } at || game.LoadTexture(at, TextureMax) is not { } t || GameTextures.Rgba(t) is not { } rgba)
        {
            return null;
        }
        if (cut)
        {
            GameTextures.Bleed(rgba, t.Width, t.Height);
        }
        return new ModelStore.ImageData(t.Width, t.Height, rgba);
    }
}
