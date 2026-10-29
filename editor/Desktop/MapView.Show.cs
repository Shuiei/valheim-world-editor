using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using TerrainEditor.App;
using MapData = ValheimGen.MapData;

namespace TerrainEditor.Desktop;

/// <summary>
/// Showing a world: its map data is worked out in the background, and the whole-world layers are kept when they are ready.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// Shows a world (its map data is worked out in the background: a few seconds).
    /// </summary>
    public void Show(WorldSession session)
    {
        _session = session;
        // The last world's portals are not this one's: none until this world's are read.
        _portals = null;
        PortalsError = null;
        // The last world's map is no longer wanted: its build stops, and gives its threads back.
        StopBuild();
        var build = _build = new CancellationTokenSource();
        _data = new MapData(session.Terrain, session.Edits, build.Token);
        ShowIcons(session);
        _global = null;
        _globalShown = false;
        _detail = null;
        _detailShown = null;
        _detailFailed = null;
        ReadPieces();
        Status?.Invoke(Lang.T("map.msg.building"));
        var data = _data;
        // On a thread of its own (LongRunning), not one of the pool's: the build keeps all the cores but one
        // busy, and the pool keeps the rest for everything else.
        Task.Factory.StartNew(() =>
        {
            MapData.Layers g;
            try
            {
                g = BuildGlobal(data);
            }
            catch (OperationCanceledException)
            {
                // Stopped because another world is shown: not an error.
                Console.WriteLine("World map: building it stopped (another world is shown).");
                return;
            }
            catch (Exception ex)
            {
                // Said and logged, not lost in a task nobody waits for (the map left blank, still "Building…").
                Log.Caught(ex, "building the world map");
                Dispatcher.UIThread.Post(() =>
                {
                    if (_data == data)
                    {
                        Status?.Invoke(Lang.T("map.msg.buildFailed", ex.Message));
                    }
                });
                return;
            }
            // Kept on the UI thread, where Show and Dispose change _data: only if this world is still the one shown.
            Dispatcher.UIThread.Post(() =>
            {
                if (_data != data)
                {
                    return;
                }
                _global = g;
                Status?.Invoke("");
                RequestNextFrameRendering();
            });
        }, build.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Stops the world map's build, if one runs; one that finishes anyway is not shown.
    /// </summary>
    private void StopBuild()
    {
        _data = null;
        _build?.Cancel();
        _build?.Dispose();
        _build = null;
    }

    /// <summary>
    /// Leaving the map for good: its build is stopped.
    /// </summary>
    public void Dispose() => StopBuild();
}
