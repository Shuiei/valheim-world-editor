using System.Numerics;

namespace TerrainEditor.Desktop;

// The Area tool's selection and the zones marked for reset; the Paste tool's outlines.
public sealed partial class GlView
{
    /// <summary>The Area selection's (or the paste's) lines, and the reset zones', on the graphics card.</summary>
    private uint _areaVao, _areaVbo, _resetVao, _resetVbo;
    /// <summary>
    /// The Area selection (in the Area tool), following the ground, and the zones marked for reset (red, always).
    /// With the Paste tool, where the paste would go: its outlines, and a post at each object not drawn as a ghost.
    /// </summary>
    private void DrawArea(WorldScene s, Matrix4x4 vp)
    {
        void Ring(List<float> to, List<Vector2> pts, bool closed, float lift) => GroundPolyline(to, s, pts, lift, closed);
        if (Mode == ToolMode.Paste && Paste.At is { } pat && Paste.Clip != null)
        {
            // Where the paste would go: its outlines and a small post at each object whose model is not
            // drawn as a ghost (DrawGhosts).
            var (objs, outlines) = Paste.Preview(pat, GridHeight);
            var data = new List<float>();
            foreach (var o in outlines)
            {
                Ring(data, o, true, 0.3f);
            }
            var ghosted = _pasteGhosts;
            for (int k = 0; k < objs.Count; k++)
            {
                if (ghosted != null && ghosted.Contains(Paste.Clip.Objects[k % Paste.Clip.Objects.Count].Prefab))
                {
                    continue;
                }
                var o = objs[k];
                var (x, z) = GridToView(s, o.X, o.Z);
                data.AddRange([x, o.Y, z, x, o.Y + 1.2f, z, x - 0.3f, o.Y + 0.5f, z, x + 0.3f, o.Y + 0.5f, z]);
            }
            // Red where it would be refused: past the open area.
            var colour = Paste.Fits(pat, s.W, s.H) ? new Vector4(1, 0.76f, 0.29f, 1) : new Vector4(0.95f, 0.3f, 0.25f, 1);
            DrawLines(ref _areaVao, ref _areaVbo, [.. data], vp, colour);
        }
        if (Mode == ToolMode.Area)
        {
            var data = new List<float>();
            if (Area.Polygon() is { } poly)
            {
                Ring(data, poly, true, 0.3f);
            }
            else
            {
                List<Vector2> pts;
                lock (Area.Points)
                {
                    pts = [.. Area.Points];
                }
                if (pts.Count > 1)
                {
                    Ring(data, pts, false, 0.3f);
                }
            }
            DrawLines(ref _areaVao, ref _areaVbo, [.. data], vp, new Vector4(0.37f, 0.83f, 1, 1));
        }
        if (s.Session is { } session)
        {
            var data = new List<float>();
            foreach (var r in session.Resets)
            {
                float gx0 = (r.X - s.X0) * 64, gz0 = (r.Z - s.Z0) * 64;
                Ring(data, [new(gx0, gz0), new(gx0 + 64, gz0), new(gx0 + 64, gz0 + 64), new(gx0, gz0 + 64)], true, 0.6f);
            }
            DrawLines(ref _resetVao, ref _resetVbo, [.. data], vp, new Vector4(1, 0.31f, 0.25f, 1));
        }
    }
}
