using Avalonia.Threading;
using TerrainEditor.App;

namespace TerrainEditor.Desktop;

// What changed since the last frame, built again before it is drawn: the groups, the shapes and the overlays.
public sealed partial class GlView
{
    /// <summary>
    /// Builds again the groups that changed (once their models are ready), the shapes, and the overlays that lie on
    /// the ground. Ground that changed moves the overlays lying on it (zone borders, rings): built again then too, a
    /// few times a second at most while a brush stroke goes on.
    /// </summary>
    /// <param name="s">The area shown.</param>
    /// <param name="now">The view's clock, in milliseconds.</param>
    private void RebuildDirty(WorldScene s, long now)
    {
        Group[] dirty;
        lock (_dirtyGroups)
        {
            dirty = [.. _dirtyGroups.Where(g => g.Ready)];
            _dirtyGroups.ExceptWith(dirty);
        }
        foreach (var g in dirty)
        {
            Rebuild(s, g);
        }
        if (_shapesDirty || _shapesGeneration != ModExtensions.Generation)
        {
            RebuildShapes(s);
        }
        if (_overlaysDirty && _terrainVao != 0 && (!_overlaysGroundOnly || now - _overlaysBuiltAt >= 200))
        {
            _overlaysDirty = _overlaysGroundOnly = false;
            _overlaysBuiltAt = now;
            Overlays.Built o;
            lock (s.Things)
            {
                o = Overlays.Build(s, s.Modifiers, i => NameOf(s.Things[i].Prefab));
            }
            UploadOverlays(o);
            Dispatcher.UIThread.Post(() => OverlaysBuilt?.Invoke(o));
        }
        else if (_overlaysDirty)
        {
            // Waiting for the 200 ms: another frame then.
            RequestNextFrameRendering();
        }
    }
}
