using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Silk.NET.OpenGL;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

/// <summary>
/// The 3D view: the ground, the water and every object and building piece of a WorldScene, drawn with
/// OpenGL (OpenGL 3.3, or OpenGL ES 3.0 where Avalonia gives that, e.g. ANGLE on Windows). Objects are
/// drawn with GPU instancing: one draw per model part for all its copies. Models load on worker threads
/// and appear as they arrive. Drawn only while something happens (the camera moves, keys, the mouse),
/// a few times a second otherwise.
/// </summary>
public sealed partial class GlView : OpenGlControlBase
{
    /// <summary>The OpenGL calls, set up when the context is made.</summary>
    private GL _gl = null!;
    /// <summary>Every OpenGL object this view made, deleted with its context (see GlObjects).</summary>
    private GlObjects _own = null!;
    /// <summary>Its programs' uniform locations, asked once each; made again with the context.</summary>
    private GlUniforms _u = null!;
    /// <summary>True on OpenGL ES (the shaders get its header).</summary>
    private bool _es;

    /// <summary>The models of the area's objects, null when there are none (plain boxes).</summary>
    internal ModelStore? Models { get; private set; }
    /// <summary>
    /// A status line to show (OpenGL version, game look loaded or not…), in the user's language and in English (for
    /// the log); may be raised from a worker thread.
    /// </summary>
    public event Action<Said>? Status;

    // ---- View switches: which kinds of objects are drawn, and the water.
    /// <summary>An empty view; the idle timer runs only while it is shown.</summary>
    public GlView()
    {
        SelectTool = new SelectTool(this);
        // Idle: a few frames a second, 20 with the game look (its water and clouds move).
        // Only while the view is shown: a running timer is held by the dispatcher, and through its Tick
        // this view, its scene and its models, so a view that kept it after its window closed was never freed.
        var idle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        idle.Tick += (_, _) => { if (_look != null || _clock.ElapsedMilliseconds - _idleAt >= 250) { _idleAt = _clock.ElapsedMilliseconds; RequestNextFrameRendering(); } };
        AttachedToVisualTree += (_, _) => idle.Start();
        DetachedFromVisualTree += (_, _) => idle.Stop();
    }

    /// <summary>
    /// Asks for frames at full speed for a second. Avalonia takes frame requests on its own (UI) thread
    /// only: from a worker thread (a model or the game look finished loading) the request goes there.
    /// </summary>
    private void Wake()
    {
        _wokeAt = _clock.ElapsedMilliseconds;
        if (Dispatcher.UIThread.CheckAccess())
        {
            RequestNextFrameRendering();
        }
        else
        {
            Dispatcher.UIThread.Post(RequestNextFrameRendering);
        }
    }

    // ---------------------------------------------------------------- GL objects
    /// <summary>The ground's, the objects' and the water's shader programs.</summary>
    private uint _terrainProg, _objectProg, _waterProg;

    /// <summary>The area shown, null before one is.</summary>
    internal WorldScene? Scene { get; private set; }
}
