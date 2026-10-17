using System.Numerics;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// Showing an area: the scene, the camera over its middle, the session's changes followed, and the game look made for it.
public sealed partial class GlView
{
    /// <summary>
    /// Shows a scene: the camera over its middle, the selection cleared, the game look read again for it
    /// (on a worker thread) and its session's changes followed.
    /// </summary>
    /// <param name="scene">The area to show.</param>
    /// <param name="models">Its objects' models, null to draw them as boxes.</param>
    public void Show(WorldScene scene, ModelStore? models)
    {
        Scene = scene;
        Dungeon.Scene = scene;
        Dungeon.Changed -= Wake;
        Dungeon.Changed += Wake;
        Models = models;
        int g = (scene.H / 2) * scene.W + scene.W / 2;
        lock (_camLock)
        {
            _target = new Vector3(0, scene.Heights[g], 0);
        }
        // Another area: the selection goes now (its indices were the last one's), before anything is
        // selected in this one.
        lock (_selection)
        {
            _selection.Clear();
        }
        SelectionDone();
        _sceneDirty = true;
        _marksDirty = true;
        _spawnsDirty = true;
        // The game look's files do not depend on the area: kept from the last one unless another Valheim was chosen.
        _lookFiles = GameLookGl.Kept();
        // The game look is set up for the area's own textures (mask, heights): again for a new one. The
        // old one's textures go at the next frame (only the drawing may touch the graphics card).
        if (_look != null)
        {
            lock (_looksDropped)
            {
                _looksDropped.Add(_look);
            }
        }
        _look = null;
        // The last area's session is not followed any more: its late events hold its indices, not this scene's.
        if (_followed is { } old)
        {
            old.Changed -= Wake;
            old.ThingsChanged -= OnThingsChanged;
            old.ThingsReset -= RefreshNewMarkers;
            old.ThingsReset -= OnThingsReset;
        }
        _followed = scene.Session;
        if (scene.Session != null)
        {
            scene.Session.Changed += Wake;
            scene.Session.ThingsChanged += OnThingsChanged;
            scene.Session.ThingsReset += RefreshNewMarkers;
            scene.Session.ThingsReset += OnThingsReset;
        }
        Task.Run(() =>
        {
            try
            {
                _lookFiles = GameLookGl.Read();
                Status?.Invoke(_lookFiles == null ? Said.Of("world.msg.lookMissing") : Said.Of("world.msg.lookLoaded"));
            }
            catch (Exception ex)
            {
                Log.Caught(ex, "reading the game's look");
                Status?.Invoke(Said.Of("world.msg.lookUnreadable", ex.Message));
            }
            Wake();
        });
        Wake();
    }
    /// <summary>The session whose changes are followed (the shown scene's), null when none.</summary>
    private EditSession? _followed;

    /// <summary>
    /// The area read again (after a save): new indices, so the selection goes at once (not at the next frame).
    /// </summary>
    private void OnThingsReset()
    {
        lock (_selection)
        {
            _selection.Clear();
        }
        SelectionDone();
        _thingsReset = true;
        Wake();
    }

    /// <summary>The game look's files as read, null until read (or when Valheim was not found).</summary>
    private volatile GameLookGl.Files? _lookFiles;
    /// <summary>The game look set up for this area, null until set up (at a frame).</summary>
    private GameLookGl? _look;
    /// <summary>
    /// Game looks of earlier areas, deleted at the next frame (only the drawing may touch the graphics card).
    /// </summary>
    private readonly List<GameLookGl> _looksDropped = [];

    /// <summary>Deletes the game looks of earlier areas (drawing thread).</summary>
    private void DropLooks()
    {
        lock (_looksDropped)
        {
            foreach (var look in _looksDropped)
            {
                look.Delete();
            }
            _looksDropped.Clear();
        }
    }
}
