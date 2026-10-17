using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

/// <summary>
/// The look's textures sent to the graphics card: pictures as 2D and array textures, the noise stand-ins and the height map.
/// </summary>
public sealed partial class GameLookGl
{
    /// <summary>
    /// Sends a picture as a 2D texture.
    /// </summary>
    /// <param name="img">The picture.</param>
    /// <param name="mipmaps">Whether it gets mipmaps (and trilinear filtering).</param>
    /// <param name="repeat">Whether it repeats (else it is clamped at its edges).</param>
    private unsafe uint Tex2D(Image img, bool mipmaps = true, bool repeat = true)
    {
        uint t = _own.Texture();
        _gl.BindTexture(TextureTarget.Texture2D, t);
        fixed (byte* p = img.Rgba)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        if (mipmaps)
        {
            _gl.GenerateMipmap(TextureTarget.Texture2D);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)(mipmaps ? TextureMinFilter.LinearMipmapLinear : TextureMinFilter.Linear));
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        int wrap = (int)(repeat ? TextureWrapMode.Repeat : TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, wrap);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, wrap);
        return t;
    }

    /// <summary>
    /// Sends a picture of square slices one above the other as a texture array, with mipmaps, repeating.
    /// </summary>
    /// <param name="img">The picture: its width is the slices' size.</param>
    /// <param name="srgb">Whether its colours are sRGB.</param>
    private unsafe uint Array2D(Image img, bool srgb)
    {
        int s = img.Width, n = img.Height / s;
        uint t = _own.Texture();
        _gl.BindTexture(TextureTarget.Texture2DArray, t);
        fixed (byte* p = img.Rgba)
        {
            _gl.TexImage3D(TextureTarget.Texture2DArray, 0, srgb ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8, (uint)s, (uint)s, (uint)n, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        _gl.GenerateMipmap(TextureTarget.Texture2DArray);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        _gl.TexParameter(TextureTarget.Texture2DArray, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        return t;
    }

    /// <summary>
    /// Stand-ins for the game's global 3D noise textures (_CurlNoise, _SnowGlintOffsetNoise): random bytes, the same
    /// generator and seeds as look.js.
    /// </summary>
    /// <param name="size">Its size in texels along each side.</param>
    /// <param name="seed">The generator's seed.</param>
    private unsafe uint Noise3D(int size, uint seed)
    {
        byte[] data = new byte[size * size * size * 4];
        uint a = seed;
        for (int i = 0; i < data.Length; i++)
        {
            a += 0x6D2B79F5;
            uint t = a;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            data[i] = (byte)(((t ^ (t >> 14)) / 4294967296.0) * 256);
        }
        uint tex = _own.Texture();
        _gl.BindTexture(TextureTarget.Texture3D, tex);
        fixed (byte* p = data)
        {
            _gl.TexImage3D(TextureTarget.Texture3D, 0, InternalFormat.Rgba8, (uint)size, (uint)size, (uint)size, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture3D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        foreach (var w in new[] { TextureParameterName.TextureWrapS, TextureParameterName.TextureWrapT, TextureParameterName.TextureWrapR })
        {
            _gl.TexParameter(TextureTarget.Texture3D, w, (int)TextureWrapMode.Repeat);
        }
        return tex;
    }

    /// <summary>
    /// The ground's height per grid point (half floats: filterable everywhere), for the water's depth.
    /// </summary>
    private unsafe uint HeightTexture(WorldScene s)
    {
        Half[] h = [.. s.Heights.Select(v => (Half)v)];
        uint t = _own.Texture();
        _gl.BindTexture(TextureTarget.Texture2D, t);
        fixed (Half* p = h)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.R16f, (uint)s.W, (uint)s.H, 0, PixelFormat.Red, PixelType.HalfFloat, p);
        }
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        return t;
    }
}
