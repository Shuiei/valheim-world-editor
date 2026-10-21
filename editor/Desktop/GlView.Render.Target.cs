using Silk.NET.OpenGL;

namespace TerrainEditor.Desktop;

// The picture a frame is drawn into: the screen, or a smaller picture for a lower 3D resolution (stretched to the
// screen when it is done).
public sealed partial class GlView
{
    /// <summary>The smaller picture for a lower 3D resolution (made again when the size changes).</summary>
    private uint _lowFbo, _lowColor, _lowDepth;
    /// <summary>The smaller picture's size in pixels.</summary>
    private int _lowW, _lowH;

    /// <summary>A size the smaller picture could not be made at (incomplete), not tried again; null when none.</summary>
    private (int W, int H)? _lowFailed;

    /// <summary>
    /// The smaller picture, made again when <paramref name="w"/> × <paramref name="h"/> is not its size.
    /// </summary>
    /// <returns>False when the graphics card does not take it (incomplete; logged once a size): draw at full size.</returns>
    private bool LowResolution(int w, int h)
    {
        if (_lowFbo != 0 && w == _lowW && h == _lowH)
        {
            return true;
        }
        if (_lowFailed == (w, h))
        {
            return false;
        }
        DropLowResolution();
        (_lowW, _lowH) = (w, h);
        _lowFbo = _own.Framebuffer();
        _lowColor = _own.Renderbuffer();
        _lowDepth = _own.Renderbuffer();
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _lowColor);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.Rgba8, (uint)w, (uint)h);
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, _lowDepth);
        _gl.RenderbufferStorage(RenderbufferTarget.Renderbuffer, InternalFormat.DepthComponent24, (uint)w, (uint)h);
        _gl.BindRenderbuffer(RenderbufferTarget.Renderbuffer, 0);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _lowFbo);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, RenderbufferTarget.Renderbuffer, _lowColor);
        _gl.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, _lowDepth);
        var status = _gl.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
        if (status != GLEnum.FramebufferComplete)
        {
            Console.Error.WriteLine($"The {w}×{h} picture for a lower 3D resolution is incomplete ({status}): drawn at full resolution.");
            DropLowResolution();
            _lowFailed = (w, h);
            return false;
        }
        return true;
    }

    /// <summary>Deletes the smaller picture, if there is one.</summary>
    private void DropLowResolution()
    {
        if (_lowFbo == 0)
        {
            return;
        }
        _own.DeleteFramebuffer(_lowFbo);
        _own.DeleteRenderbuffer(_lowColor);
        _own.DeleteRenderbuffer(_lowDepth);
        _lowFbo = _lowColor = _lowDepth = 0;
        _lowW = _lowH = 0;
    }

    /// <summary>Binds the picture to draw into (the smaller one when low), sizes the viewport and clears it.</summary>
    /// <param name="fb">The screen's framebuffer.</param>
    /// <param name="low">Whether the frame is drawn into the smaller picture.</param>
    /// <param name="rw">The picture's width in pixels.</param>
    /// <param name="rh">The picture's height in pixels.</param>
    private void BeginPicture(int fb, bool low, int rw, int rh)
    {
        // Kept for the passes that draw elsewhere and come back (the selection's outline).
        (_frameFbo, _frameW, _frameH) = (low ? _lowFbo : (uint)fb, rw, rh);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _frameFbo);
        _gl.Viewport(0, 0, (uint)rw, (uint)rh);
        _gl.ClearColor(0.42f, 0.58f, 0.74f, 1);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _gl.Enable(EnableCap.DepthTest);
        _gl.DepthFunc(DepthFunction.Lequal);
    }

    /// <summary>Unbinds the frame's shaders and, when drawn small, stretches the smaller picture onto the screen.</summary>
    /// <param name="low">Whether the frame was drawn into the smaller picture.</param>
    /// <param name="fb">The screen's framebuffer.</param>
    /// <param name="rw">The picture's width in pixels.</param>
    /// <param name="rh">The picture's height in pixels.</param>
    /// <param name="pw">The screen's width in pixels.</param>
    /// <param name="ph">The screen's height in pixels.</param>
    private void EndPicture(bool low, int fb, int rw, int rh, int pw, int ph)
    {
        _gl.BindVertexArray(0);
        _gl.UseProgram(0);
        if (low)
        {
            _gl.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _lowFbo);
            _gl.BindFramebuffer(FramebufferTarget.DrawFramebuffer, (uint)fb);
            _gl.BlitFramebuffer(0, 0, rw, rh, 0, 0, pw, ph, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            _gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)fb);
        }
    }
}
