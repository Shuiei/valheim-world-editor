using Avalonia.OpenGL.Controls;

namespace TerrainEditor.Desktop;

/// <summary>
/// The 1 m close-up: read in the background for the part of the map in view, when zoomed in.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// Close up (under 6 m a pixel): reads a close-up covering the view (a bit more, in whole zones) in the background,
    /// unless the one read covers it at the current edit version.
    /// </summary>
    /// <param name="mpp">Metres per device pixel.</param>
    /// <param name="w">The view's width, in device pixels.</param>
    /// <param name="h">The view's height, in device pixels.</param>
    private void AskDetail(float mpp, int w, int h)
    {
        if (_data == null || _session == null || mpp >= 6 || _asking)
        {
            return;
        }
        int size = Math.Clamp((int)MathF.Ceiling(MathF.Max(w, h) * mpp * 1.3f / 64) * 64, 64, 2048);
        int x0 = (int)MathF.Floor((Center.X - size / 2f) / 64) * 64, z0 = (int)MathF.Floor((Center.Y - size / 2f) / 64) * 64;
        int version = _session.Edits.Version;
        if (_detail is { } d && d.Version == version
            && ((d.X0 <= Center.X - w * mpp / 2 && d.Z0 <= Center.Y - h * mpp / 2 && d.X0 + d.Size >= Center.X + w * mpp / 2 && d.Z0 + d.Size >= Center.Y + h * mpp / 2)
                // The largest close-up can be narrower than the view: the one there is that one, not asked for again
                // each frame.
                || (d.X0 == x0 && d.Z0 == z0 && d.Size == size)))
        {
            return;
        }
        if (_detailFailed == (x0, z0, size, version))
        {
            return;
        }
        _asking = true;
        ReadDetail(_data, x0, z0, size, version);
    }
}
