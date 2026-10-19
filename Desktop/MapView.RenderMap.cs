using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;
using MapData = ValheimGen.MapData;

namespace TerrainEditor.Desktop;

/// <summary>
/// The map's drawing order and the map shader's uniforms.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// Draws the map shader's picture, then the shapes over it, with the map's own GL state set each frame.
    /// </summary>
    /// <param name="w">The view's width, in device pixels.</param>
    /// <param name="h">The view's height, in device pixels.</param>
    /// <param name="mpp">Metres per device pixel.</param>
    private void DrawMap(int w, int h, float mpp)
    {
        // The 3D editor draws in the same OpenGL context: its textures, depth test and culling are
        // still set when the map comes back, so the map sets its own every frame.
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
        _gl.Disable(EnableCap.ScissorTest);
        _gl.DepthMask(true);
        _gl.ColorMask(true, true, true, true);
        _gl.UseProgram(_prog);
        foreach (var (name, (tex, unit)) in _tex)
        {
            _gl.ActiveTexture(TextureUnit.Texture0 + unit);
            _gl.BindTexture(TextureTarget.Texture2D, tex);
            _gl.Uniform1(_u.At(_prog, name), unit);
        }
        _gl.ActiveTexture(TextureUnit.Texture0);
        SetMapUniforms(w, h, mpp);
        _gl.BindVertexArray(_vao);
        _gl.DrawArrays(PrimitiveType.Triangles, 0, 3);
        GlBlend.Over(_gl);
        _gl.UseProgram(_fillProg);
        _gl.Uniform4(_u.At(_fillProg, "uView"), Center.X, Center.Y, 2 / (w * mpp), 2 / (h * mpp));
        DrawBuildings();
        DrawZoneFills();
        DrawLines(w, h, mpp);
        _gl.Enable(EnableCap.Blend);
        _gl.UseProgram(_fillProg);
        DrawPins();
        _gl.Disable(EnableCap.Blend);
        _gl.BindVertexArray(0);
        _gl.UseProgram(0);
    }

    /// <summary>
    /// Sets the map shader's uniforms for the view: time, the zoom, the close-up, the view's centre and size, and the
    /// paint and cloud switches.
    /// </summary>
    /// <param name="w">The view's width, in device pixels.</param>
    /// <param name="h">The view's height, in device pixels.</param>
    /// <param name="mpp">Metres per device pixel.</param>
    private void SetMapUniforms(int w, int h, float mpp)
    {
        int U(string n) => _u.At(_prog, n);
        float time = _clock.ElapsedMilliseconds / 1000f;
        _gl.Uniform4(U("_Time"), time / 20, time, time * 2, time * 3);
        SetMaterials();
        _gl.Uniform3(U("_SunDir"), -0.35f, 0.86f, 0.36f);
        _gl.Uniform1(U("_normalIntensity"), 4f);
        float mapMeters = MapMeters;
        var d = _detailShown;
        _gl.Uniform1(U("_zoom"), w * mpp / mapMeters);
        // The game quantizes to ~3.5 m map "pixels"; the close-up goes down to 1 m.
        _gl.Uniform1(U("_quant"), d != null && mpp < 3.5f ? mapMeters : 7000f);
        _gl.Uniform1(U("normalWidthM"), d != null && mpp < 6 ? MathF.Max(1, mpp) : (_data?.TexelMeters ?? MapData.GlobalPixel) * 2.048f);
        _gl.Uniform3(U("_mapCenter"), Center.X, 0, Center.Y);
        _gl.Uniform3(U("_CloudOffset"), time * 0.0015f, 0, time * 0.001f);
        _gl.Uniform2(U("viewCenter"), Center.X, Center.Y);
        _gl.Uniform2(U("canvasSize"), (float)w, h);
        _gl.Uniform1(U("metersPerPixel"), mpp);
        _gl.Uniform1(U("mapMeters"), mapMeters);
        // Floats (a vec4): whole numbers would pick glUniform4i, which a float uniform refuses.
        _gl.Uniform4(U("detailRect"), d?.X0 ?? 0, d?.Z0 ?? 0, d?.Size ?? 1, d != null ? 1f : 0f);
        _gl.Uniform1(U("showPaint"), ShowPaint ? 1f : 0f);
        _gl.Uniform1(U("showClouds"), ShowClouds ? 1f : 0f);
    }
}
