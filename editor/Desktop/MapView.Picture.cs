using Avalonia.OpenGL.Controls;

namespace TerrainEditor.Desktop;

/// <summary>
/// The test driver's pictures of the map.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// For the test driver: saves the picture once the map is drawn, when the world map is in, a few frames are drawn,
    /// and (zoomed in) the close-up is shown.
    /// </summary>
    /// <param name="w">The view's width, in device pixels.</param>
    /// <param name="h">The view's height, in device pixels.</param>
    /// <param name="mpp">Metres per device pixel.</param>
    private void SavePicture(int w, int h, float mpp)
    {
        if (_picture is { } req && FramesDrawn > 10 && (mpp >= 6 || (_detailShown != null && _detail == _detailShown && !_asking)))
        {
            _picture = null;
            try
            {
                GlPicture.Save(_gl, w, h, req.Path);
                req.Done.SetResult();
            }
            catch (Exception ex)
            {
                // Not logged here: handed to the one waiting for the picture (the test driver, Claude's tools), which
                // handles it.
                req.Done.SetException(ex);
            }
        }
    }

    /// <summary>
    /// For the test driver: saves a picture of the map once the world map is in, a few frames are drawn and (zoomed in)
    /// the close-up is shown.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <returns>Done when the picture is saved.</returns>
    internal Task Picture(string path)
    {
        var r = new GlPicture.Request { Path = path };
        _picture = r;
        RequestNextFrameRendering();
        return r.Done.Task;
    }
}
