using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

/// <summary>
/// Blending shared by the OpenGL views (the 3D view, the map, the game-look pictures).
/// </summary>
internal static class GlBlend
{
    /// <summary>
    /// Turns blending on: the fragment is drawn over what is there by its alpha, and the framebuffer's alpha is kept as
    /// it is (opaque after the clear). With the same source-over factors applied to alpha, every see-through surface
    /// (water, glass, ghosts, overlays) lowered the alpha under it, and the view's pictures (the docs' PNGs, --shot)
    /// came out part-transparent there.
    /// </summary>
    /// <param name="gl">The view's OpenGL.</param>
    internal static void Over(GL gl)
    {
        gl.Enable(EnableCap.Blend);
        gl.BlendFuncSeparate(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha, BlendingFactor.Zero, BlendingFactor.One);
    }
}
