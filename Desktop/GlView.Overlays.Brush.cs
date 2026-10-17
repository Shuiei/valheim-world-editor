using System.Numerics;

namespace TerrainEditor.Desktop;

// The brush's ring on the ground, and the Mountain tool's preview (GlView.Overlays.Brush.Ring.cs, .Mountain.cs).
public sealed partial class GlView
{
    /// <summary>The brush ring's lines, and the mountain preview's, on the graphics card.</summary>
    private uint _ringVao, _ringVbo, _previewVao, _previewVbo;

    /// <summary>
    /// The brush's outline on the ground (and the Ring shape's inner edge), seen through what stands on it.
    /// Short segments, so a wide ring follows hills instead of cutting through them; none outside the open
    /// area (no ground there). For the Mountain tool: the mountain itself as a mesh, and the ring in red
    /// where it does not fit.
    /// </summary>
    private unsafe void DrawBrush(WorldScene s, Matrix4x4 vp)
    {
        if (_hover is not { } h || s.Session is not { } session || Tool == null && Mode is not (ToolMode.Shape or ToolMode.Mountain))
        {
            return;
        }
        float r = Tool == null ? ShapeRadius : session.Brush.Radius;
        int n = Math.Clamp((int)MathF.Ceiling(MathF.Tau * r / 1.5f), 96, 2048);
        bool fits = Mode != ToolMode.Mountain || MountainFits(s, h.X, h.Z, ShapeRadius);
        DrawLines(ref _ringVao, ref _ringVbo, BrushRing(s, h, session, n), vp, fits ? new Vector4(1, 1, 1, 1) : new Vector4(1, 0.35f, 0.3f, 1), width: 1.2f);
        if (Tool == null && Mode == ToolMode.Mountain && fits && MountainPreview is { } shape)
        {
            DrawMountainPreview(s, vp, h, shape);
        }
    }
}
