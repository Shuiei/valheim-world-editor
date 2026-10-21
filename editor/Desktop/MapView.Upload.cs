using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;
using MapData = ValheimGen.MapData;

namespace TerrainEditor.Desktop;

/// <summary>
/// Uploading map layers into the shader's textures.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// Uploads map layers as the shader's height, main and mask textures (and paint for the close-up).
    /// </summary>
    /// <param name="prefix">"g" for the whole world, "d" for the 1 m close-up.</param>
    /// <param name="l">The layers.</param>
    private unsafe void Upload(string prefix, MapData.Layers l)
    {
        void Set(string name, InternalFormat internalFormat, PixelFormat format, PixelType type, void* data) => Texture(name, _ =>
        {
            _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            _gl.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, (uint)l.Width, (uint)l.Height, 0, format, type, data);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        });
        fixed (Half* h = l.Heights)
        fixed (byte* b = l.Biome)
        fixed (byte* m = l.Mask)
        fixed (byte* p = l.Paint)
        {
            Set(prefix + "Height", InternalFormat.R16f, PixelFormat.Red, PixelType.HalfFloat, h);
            Set(prefix + "Main", InternalFormat.Srgb8Alpha8, PixelFormat.Rgba, PixelType.UnsignedByte, b);
            Set(prefix + "Mask", InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte, m);
            if (prefix == "d")
            {
                Set("dPaint", InternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte, p);
            }
        }
    }
}
