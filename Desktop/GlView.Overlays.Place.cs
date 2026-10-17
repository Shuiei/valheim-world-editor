using System.Numerics;

namespace TerrainEditor.Desktop;

// The Place tool's marks: its brush ring, a post at each placement, and the line, zone or grid being drawn.
public sealed partial class GlView
{
    /// <summary>The Place tool's brush ring on the graphics card.</summary>
    private uint _placeRingVao, _placeRingVbo;

    /// <summary>The Place tool's posts and its line, zone or grid on the graphics card.</summary>
    private uint _placeVao, _placeVbo, _placeShapeVao, _placeShapeVbo;
    /// <summary>The prefabs drawn as ghosts this frame: they get no post.</summary>
    private HashSet<int> _ghostsDrawn = [];
    /// <summary>
    /// The Place tool: a post at each placement the preview (or a stroke) shows, and the line, zone or
    /// grid being drawn with its points.
    /// </summary>
    private void DrawPlace(WorldScene s, Matrix4x4 vp)
    {
        if (Mode != ToolMode.Place || Place is not { } pl)
        {
            return;
        }
        float ox = s.OriginX, oz = s.OriginZ;
        var posts = new List<float>();
        foreach (var o in pl.Shown)
        {
            if (_ghostsDrawn.Contains(Save.StableHash.Of(o.Name)))
            {
                continue;
            }
            var (x, z) = GridToView(s, o.Position.X - ox, o.Position.Z - oz);
            float y = o.Position.Y;
            posts.AddRange([x, y, z, x, y + 1.5f, z, x - 0.35f, y + 0.6f, z, x + 0.35f, y + 0.6f, z, x, y + 0.6f, z - 0.35f, x, y + 0.6f, z + 0.35f]);
        }
        DrawLines(ref _placeVao, ref _placeVbo, [.. posts], vp, new Vector4(0.62f, 0.88f, 1, 1));
        var t = pl.Tool;
        // The brush's outline under the cursor: where a stroke places.
        if (t.Mode == PlaceTool.Modes.Brush && !t.Building && _hover is { } h)
        {
            var ring = new List<float>();
            var outline = t.Brush.Outline(96);
            for (int i = 0; i < outline.Count; i++)
            {
                foreach (var (ox2, oz2) in new[] { outline[i], outline[(i + 1) % outline.Count] })
                {
                    var p = OnGround(s, h.X + ox2, h.Z + oz2, 0.3f);
                    ring.AddRange([p.X, p.Y, p.Z]);
                }
            }
            DrawLines(ref _placeRingVao, ref _placeRingVbo, [.. ring], vp, new Vector4(0.62f, 0.88f, 1, 1), width: 1.2f);
        }
        var data = new List<float>();
        void Strip(IReadOnlyList<Vector2> pts) => GroundPolyline(data, s, pts, 0.3f, closed: false);
        if (t.Mode == PlaceTool.Modes.Line)
        {
            foreach (var run in t.LineRuns())
            {
                Strip(run);
            }
        }
        else if (t.Mode == PlaceTool.Modes.Zone)
        {
            var z = pl.ShownPoints();
            if (z.Count > 1)
            {
                Strip([.. z, z[0]]);
            }
        }
        else if (t.Mode == PlaceTool.Modes.Grid && t.GridA is { } a && t.GridB is { } b)
        {
            Strip([.. new[] { a, new Vector2(b.X, a.Y), b, new Vector2(a.X, b.Y), a }.Select(t.Xf)]);
        }
        if (t.Mode is PlaceTool.Modes.Line or PlaceTool.Modes.Zone)
        {
            foreach (var p in pl.ShownPoints())
            {
                Marker(data, OnGround(s, p.X, p.Y, 0.3f));
            }
        }
        DrawLines(ref _placeShapeVao, ref _placeShapeVbo, [.. data], vp, new Vector4(0.62f, 0.88f, 1, 1));
    }
}
