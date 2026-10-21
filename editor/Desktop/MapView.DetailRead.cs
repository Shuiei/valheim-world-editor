using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using TerrainEditor.App;
using MapData = ValheimGen.MapData;

namespace TerrainEditor.Desktop;

/// <summary>
/// The 1 m close-up's worker read: the layers are kept on the UI thread, for the world still shown.
/// </summary>
public sealed partial class MapView : OpenGlControlBase, IDisposable
{
    /// <summary>
    /// Reads a close-up on a worker thread; kept on the UI thread only if its world is still the one shown.
    /// </summary>
    /// <param name="data">The map data of the world the close-up is read from.</param>
    /// <param name="x0">The close-up's first world X, in metres.</param>
    /// <param name="z0">The close-up's first world Z, in metres.</param>
    /// <param name="size">The close-up's size, in metres.</param>
    /// <param name="version">The edits' version the close-up is read at.</param>
    private void ReadDetail(MapData data, int x0, int z0, int size, int version)
    {
        Task.Run(() =>
        {
            MapData.Layers? layers = null;
            bool failed = false;
            try
            {
                layers = data.Detail(x0, z0, size, version);
            }
            // A stopped build (another world shown) is no failure.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Caught(ex, "reading the map's close-up");
                failed = true;
            }
            finally
            {
                // As the world map: kept on the UI thread, only if this world is still the one shown. Asking again
                // only once it is there (or failed), else a frame drawn meanwhile would ask for the same close-up.
                Dispatcher.UIThread.Post(() =>
                {
                    if (layers != null && _data == data)
                    {
                        _detail = (layers, x0, z0, size, version);
                    }
                    if (failed && _data == data)
                    {
                        _detailFailed = (x0, z0, size, version);
                    }
                    _asking = false;
                    RequestNextFrameRendering();
                });
            }
        });
    }
}
