using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

/// <summary>
/// The mipmaps and filters of a repeating picture, shared by the OpenGL views that upload pictures (the 3D view's models, the map).
/// </summary>
internal static class MipmapFilters
{
    /// <summary>
    /// Generates the mipmaps of the 2D texture bound to the 2D target, then sets trilinear minification, linear
    /// magnification and repeating wrap on both axes.
    /// </summary>
    /// <param name="gl">The view's OpenGL, with the texture already uploaded and bound.</param>
    internal static void GenerateRepeating(GL gl)
    {
        gl.GenerateMipmap(TextureTarget.Texture2D);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
    }
}
