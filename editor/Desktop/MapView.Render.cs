using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

/// <summary>
/// Drawing the map each frame: the world map and the close-up, then the fills, lines and pins over it.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <inheritdoc/>
    protected override unsafe void OnOpenGlRender(GlInterface gl, int fb)
    {
        var g = _global;
        double scaling = Scaling;
        int w = Math.Max(1, (int)(Bounds.Width * scaling)), h = Math.Max(1, (int)(Bounds.Height * scaling));
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)fb);
        _gl.Viewport(0, 0, (uint)w, (uint)h);
        _gl.ClearColor(0.08f, 0.09f, 0.11f, 1);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        if (g == null)
        {
            return;
        }
        if (!_texturesLoaded)
        {
            LoadTextures();
        }
        if (!_globalShown)
        {
            Upload("g", g);
            _globalShown = true;
        }
        float mpp = MetersPerPixel / (float)scaling;
        AskDetail(mpp, w, h);
        if (_detail is { } dt && !_detailShown.Equals(_detail))
        {
            Upload("d", dt.Layers);
            _detailShown = _detail;
        }
        DrawMap(w, h, mpp);
        FramesDrawn++;
        ReportGlErrors();
        SavePicture(w, h, mpp);
    }

    /// <summary>
    /// The map's OpenGL errors, counted and logged when the test driver runs.
    /// </summary>
    private void ReportGlErrors()
    {
        if (Options.Driver)
        {
            for (var e = _gl.GetError(); e != GLEnum.NoError; e = _gl.GetError())
            {
                GlErrors++;
                Options.Say($"OpenGL error (map) {e}");
            }
        }
    }

    /// <summary>
    /// Close up (under 6 m a pixel): the ground in view at 1 m, with the edits, read in the background. True while one
    /// is being read.
    /// </summary>
    private bool _asking;
    /// <summary>
    /// The test driver's pending picture, saved once the map is drawn.
    /// </summary>
    private volatile GlPicture.Request? _picture;
}
