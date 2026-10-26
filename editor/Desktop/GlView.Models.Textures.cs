using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// Textures for the models: claimed by the loader that reads them, then uploaded on the drawing thread.
public sealed partial class GlView
{
    /// <summary>
    /// A loaded model's textures. Also for a model dropped before it was drawn (a new area): its loader
    /// claimed them, so no other loader reads them.
    /// </summary>
    private unsafe void UploadTextures(ReadyModel r)
    {
        foreach (var (file, img) in r.Images)
        {
            if (img == null || _textures.ContainsKey(file))
            {
                continue;
            }
            uint tex = _own.Texture();
            UploadMipmapped(tex, img);
            // With its mipmaps, a third more.
            _gpuBytes += (long)img.Width * img.Height * 4 * 4 / 3;
            lock (_textures)
            {
                _textures[file] = tex;
            }
        }
    }

    /// <summary>
    /// Uploads an image as an sRGB texture with its mipmaps, repeated, into <paramref name="tex"/>.
    /// </summary>
    /// <param name="tex">The texture, already made.</param>
    /// <param name="img">The image's pixels.</param>
    private unsafe void UploadMipmapped(uint tex, ModelStore.ImageData img)
    {
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        fixed (byte* p = img.Rgba)
        {
            _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Srgb8Alpha8, (uint)img.Width, (uint)img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        }
        MipmapFilters.GenerateRepeating(_gl);
    }

    /// <summary>
    /// Claims a texture for the loader that reads it, unless it is on the graphics card or another loader has it.
    /// </summary>
    /// <param name="file">The texture's file.</param>
    /// <returns>True when the loader may read it.</returns>
    internal bool ClaimTexture(string file)
    {
        lock (_textures)
        {
            if (_textures.ContainsKey(file) || _claimed.Contains(file))
            {
                return false;
            }
            _claimed.Add(file);
            return true;
        }
    }
}
