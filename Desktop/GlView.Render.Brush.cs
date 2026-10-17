using System.Numerics;
using Avalonia;

namespace TerrainEditor.Desktop;

// The brush's hover on the ground and the stroke's steps, before a frame is drawn.
public sealed partial class GlView
{
    /// <summary>
    /// The brush: where it is on the ground, a step of the stroke while the button is held, and the changed ground
    /// to the graphics card.
    /// </summary>
    /// <param name="s">The area shown.</param>
    /// <param name="vp">The camera's view × projection of the frame.</param>
    /// <param name="dt">The seconds since the last frame.</param>
    private void UpdateBrush(WorldScene s, Matrix4x4 vp, float dt)
    {
        _hover = (Tool != null || Mode is ToolMode.Shape or ToolMode.Mountain or ToolMode.Path || Mode == ToolMode.Place && Place?.Tool.Mode == PlaceTool.Modes.Brush) && _pointer is Point at ? GroundAt(s, vp, at, _surfaceSize) : null;
        if (_brushDown && _hover is { } hv)
        {
            s.Session?.StrokeStep(hv.X, hv.Z, dt);
        }
        if (s.Session?.TakeDirty() is { } d && _terrainVao != 0)
        {
            UpdateTerrain(s, d);
            // The mountain preview and the overlays stand on the ground as it is now.
            _previewKey = null;
            if (!_overlaysDirty)
            {
                _overlaysDirty = _overlaysGroundOnly = true;
            }
        }
    }
}
