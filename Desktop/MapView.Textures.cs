using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;
using TerrainEditor.App;
using MapData = ValheimGen.MapData;

namespace TerrainEditor.Desktop;

/// <summary>
/// The map's textures: the game's map pictures, or the plain stand-ins when they cannot be read.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// A texture on its own unit, bound to the sampler of that name.
    /// </summary>
    /// <param name="name">The map shader's sampler name; the texture is made on first use.</param>
    /// <param name="fill">Uploads the pixels and sets the filters, the texture already bound.</param>
    private unsafe void Texture(string name, Action<uint> fill)
    {
        if (!_tex.TryGetValue(name, out var t))
        {
            t = (_own.Texture(), _tex.Count);
            _tex[name] = t;
        }
        _gl.ActiveTexture(TextureUnit.Texture0 + t.Unit);
        _gl.BindTexture(TextureTarget.Texture2D, t.Tex);
        fill(t.Tex);
        _gl.UseProgram(_prog);
        _gl.Uniform1(_u.At(_prog, name), t.Unit);
    }

    /// <summary>
    /// Plain stand-ins (RGBA) when the game's map textures cannot be read (Valheim not found).
    /// </summary>
    private static readonly Dictionary<string, byte[]> Fallback = new()
    {
        ["_BackgroundTex"] = [200, 186, 150, 255],
        ["_FogLayerTex"] = [255, 255, 255, 0],
        ["_WaterTex"] = [128, 128, 128, 255],
        ["_lavaTex"] = [128, 128, 128, 255],
        ["_MountainTex"] = [255, 255, 255, 255],
        ["_CloudTex"] = [0, 0, 0, 0],
        ["_ForestTex"] = [128, 128, 128, 0],
        ["_SpaceTex"] = [10, 10, 16, 255],
    };

    /// <summary>
    /// The game's map textures as decoded for the last bundles reader asked for: decoded once, not again each time the
    /// map comes back (a new context uploads them again); another Valheim folder is read again.
    /// </summary>
    private static readonly KeptRead<GameBundles, IReadOnlyDictionary<string, GameLookData.Picture>> MapPictures = new(GameLookData.ReadMap);

    /// <summary>
    /// Uploads the game's map textures (or the plain stand-ins) and a small placeholder close-up.
    /// </summary>
    private unsafe void LoadTextures()
    {
        _texturesLoaded = true;
        IReadOnlyDictionary<string, GameLookData.Picture> pictures = new Dictionary<string, GameLookData.Picture>();
        try
        {
            if (GameLook.Bundles is { } game)
            {
                pictures = MapPictures.Of(game);
            }
        }
        catch (Exception e) when (e is IOException or InvalidDataException or NotSupportedException)
        {
            Log.Caught(e, "reading the game's map textures");
        }
        foreach (var (u, f) in new[] { ("Background", "background"), ("FogLayer", "foglayer"), ("Water", "water"), ("lava", "lava"), ("Mountain", "mountain"), ("Cloud", "cloud"), ("Forest", "forest"), ("Space", "space") })
        {
            string name = $"_{u}Tex";
            // The lava mask is data, every other map texture a colour.
            bool srgb = u != "lava";
            var bmp = pictures.GetValueOrDefault(f);
            Texture(name, _ =>
            {
                _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
                if (bmp == null)
                {
                    fixed (byte* p = Fallback[name])
                    {
                        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
                    }
                    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                    _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                    return;
                }
                // Top row first; the web uploaded the pictures as they are (no flip), so the same.
                fixed (byte* px = bmp.Rgba)
                {
                    _gl.TexImage2D(TextureTarget.Texture2D, 0, srgb ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8, (uint)bmp.Width, (uint)bmp.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, px);
                }
                MipmapFilters.GenerateRepeating(_gl);
            });
        }
        LoadIconTexture();
        // Placeholders until a close-up is loaded.
        Upload("d", new MapData.Layers(16, 16));
    }
}
