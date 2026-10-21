using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace TerrainEditor.App;

public sealed partial class GameBundles
{
    /// <summary>
    /// A sprite's pixels (RGBA, its texture's rows, bottom first), from its own texture or the sprite atlas packing it
    /// (its render data found by the sprite's key); null when it cannot be read, or the atlas turned it.
    /// </summary>
    /// <param name="bundle">The bundle the reference is made in.</param>
    /// <param name="sprite">The reference to the sprite.</param>
    public (int Width, int Height, byte[] Rgba)? LoadSprite(string bundle, AssetTypeValueField sprite)
    {
        lock (_sync)
        {
            if (Resolve(bundle, sprite) is not { } at || TypeOf(at.Bundle, at.PathId) != (int)AssetClassID.Sprite || Read(at.Bundle, at.PathId) is not { } s)
            {
                return null;
            }
            var (texture, data) = RenderData(at, s);
            return texture == null || Turned(data) ? null : Cropped(texture, data["textureRect"]);
        }
    }

    /// <summary>
    /// A sprite's texture and render data: its own, or, without its own texture, the sprite atlas's for its key; no
    /// texture when neither has one.
    /// </summary>
    private (Where? Texture, AssetTypeValueField Data) RenderData(Where at, AssetTypeValueField s)
    {
        var own = s["m_RD"];
        if (Resolve(at.Bundle, own["texture"]) is { } texture)
        {
            return (texture, own);
        }
        if (Resolve(at.Bundle, s["m_SpriteAtlas"]) is not { } atlasAt || Read(atlasAt.Bundle, atlasAt.PathId) is not { } atlas)
        {
            return (null, own);
        }
        var key = s["m_RenderDataKey"];
        var packed = atlas["m_RenderDataMap"]["Array"].Children.FirstOrDefault(p => SameKey(p["first"], key))?["second"];
        return packed == null ? (null, own) : (Resolve(atlasAt.Bundle, packed["texture"]), packed);
    }

    /// <summary>Whether the atlas packed it turned (settingsRaw's bits 2..5): not one of the map's icons.</summary>
    private static bool Turned(AssetTypeValueField data) => !data["settingsRaw"].IsDummy && ((data["settingsRaw"].AsUInt >> 2) & 0xF) != 0;

    /// <summary>Whether a render data key (a GUID's four words, then a number) is the sprite's.</summary>
    private static bool SameKey(AssetTypeValueField a, AssetTypeValueField b) =>
        a["first"].Children.Select(c => c.AsUInt).SequenceEqual(b["first"].Children.Select(c => c.AsUInt)) && a["second"].AsLong == b["second"].AsLong;

    /// <summary>A rectangle of a texture's largest level (RGBA, bottom row first); null when the texture cannot be read.</summary>
    private (int Width, int Height, byte[] Rgba)? Cropped(Where texture, AssetTypeValueField rect)
    {
        if (LoadTexture(texture, int.MaxValue) is not { } t || GameTextures.Rgba(t) is not { } all)
        {
            return null;
        }
        int x = (int)rect["x"].AsFloat, y = (int)rect["y"].AsFloat, w = (int)rect["width"].AsFloat, h = (int)rect["height"].AsFloat;
        if (w <= 0 || h <= 0 || x < 0 || y < 0 || x + w > t.Width || y + h > t.Height)
        {
            return null;
        }
        byte[] rgba = new byte[w * h * 4];
        for (int r = 0; r < h; r++)
        {
            Array.Copy(all, ((y + r) * t.Width + x) * 4, rgba, r * w * 4, w * 4);
        }
        return (w, h, rgba);
    }
}
