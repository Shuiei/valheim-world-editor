namespace TerrainEditor.Desktop;

// The test driver's pictures: once the area and its models are in, a picture is saved.
public sealed partial class GlView
{
    /// <summary>
    /// When the area and its models were all in, on the view's clock (-1 until then). --shot and --bench
    /// (Options) wait for it: once every model is loaded, save a picture, then turn the camera for the given
    /// time and print the frame rates, then close the app.
    /// </summary>
    private long _loadedAt = -1;
    /// <summary>The area <c>_loadedAt</c> was measured for (a picture of another area waits for it to load).</summary>
    private WorldScene? _loadedScene;
    /// <summary>The test driver's pictures: the next frame once the area and its models are in.</summary>
    private volatile GlPicture.Request? _picture;

    /// <summary>When the last frame was drawn to its end, on the view's clock (-1: none yet).</summary>
    private long _lastFrameAt = -1;

    /// <summary>
    /// The test driver: its pictures (once the area and its models are in) and its frame-rate measure
    /// (the camera turns on its own for a while, then the rates are given back).
    /// </summary>
    private void Automate(long now, int pw, int ph)
    {
        if (_picture == null && _bench == null)
        {
            return;
        }
        if (_loadedAt < 0)
        {
            if (Scene != null && _terrainVao != 0 && _pending == 0 && _ready.IsEmpty)
            {
                _loadedAt = now;
            }
            RequestNextFrameRendering();
            return;
        }
        AutomatePicture(now, pw, ph);
        AutomateBench(now, pw, ph);
    }

    /// <summary>Saves the waiting picture, once the area and its models have been in for half a second.</summary>
    /// <param name="now">The view's clock, in milliseconds.</param>
    /// <param name="pw">The screen's width in pixels.</param>
    /// <param name="ph">The screen's height in pixels.</param>
    private void AutomatePicture(long now, int pw, int ph)
    {
        if (_picture is { } req && now - _loadedAt > 500 && _pending == 0)
        {
            _picture = null;
            try
            {
                GlPicture.Save(_gl, pw, ph, req.Path);
                req.Done.SetResult();
            }
            catch (Exception ex)
            {
                // Handed to the one waiting for the picture (the driver), which logs it.
                req.Done.SetException(ex);
            }
        }
    }

    /// <summary>
    /// What a picture the test driver asked for waits on, in words (for its error when it times out): the frames, the
    /// area, the ground and the models.
    /// </summary>
    internal string PictureWaiting()
    {
        long last = Interlocked.Read(ref _lastFrameAt);
        string frames = last < 0 ? "no frame drawn yet" : $"the last frame ended {_clock.ElapsedMilliseconds - last} ms ago";
        return $"{frames}; {(Scene == null ? "no area" : "an area")} shown, its ground {(_terrainVao != 0 ? "on" : "not on")} the graphics card, "
            + $"{Volatile.Read(ref _pending)} model(s) being read, {_ready.Count} read and not uploaded, {(_loadedAt < 0 ? "not all in yet" : "all in")}";
    }

    /// <summary>Saves a picture of the view to <paramref name="path"/> once the area and its models are in.</summary>
    internal Task Picture(string path)
    {
        var r = new GlPicture.Request { Path = path };
        // Measured again for this area: the picture waits until it and its models are in.
        if (Scene != _loadedScene)
        {
            _loadedAt = -1;
            _loadedScene = Scene;
        }
        _picture = r;
        Wake();
        return r.Done.Task;
    }
}
