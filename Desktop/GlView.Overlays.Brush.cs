using System.Numerics;

namespace TerrainEditor.Desktop;

// The brush's ring on the ground, and the Mountain tool's preview.
public sealed partial class GlView
{
    /// <summary>The brush ring's lines, and the mountain preview's, on the graphics card.</summary>
    private uint _ringVao, _ringVbo, _previewVao, _previewVbo;
    /// <summary>The mountain preview's lines, kept while the pointer stays on the same grid point.</summary>
    private float[] _previewSegs = [];
    /// <summary>The grid point (and area size) the mountain preview was made for; null to make it again.</summary>
    private (int X, int Z, int W, int H)? _previewKey;

    /// <summary>Whether a Mountain click at a grid point fits in the open area (what EditSession.Mountain needs).</summary>
    internal static bool MountainFits(WorldScene s, float gx, float gz, float reach) => gx - reach >= 1 && gz - reach >= 1 && gx + reach <= s.W - 2 && gz + reach <= s.H - 2;

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
        var data = new List<float>();
        void Loop(List<(float X, float Z)> pts)
        {
            for (int i = 0; i < pts.Count; i++)
            {
                var (ax, az) = At(pts[i]);
                var (bx, bz) = At(pts[(i + 1) % pts.Count]);
                float ya = Picking.HeightAt(s, ax, az), yb = Picking.HeightAt(s, bx, bz);
                if (ya <= -1000 || yb <= -1000)
                {
                    continue;
                }
                data.AddRange([ax, ya + 0.3f, az, bx, yb + 0.3f, bz]);
            }
        }
        (float, float) At((float X, float Z) o) => GridToView(s, h.X + o.X, h.Z + o.Z);
        Loop(Tool == null ? [.. Enumerable.Range(0, n).Select(i => (MathF.Cos(i * MathF.Tau / n) * ShapeRadius, MathF.Sin(i * MathF.Tau / n) * ShapeRadius))] : session.Brush.Outline(n));
        if (Tool != null && session.Brush.InnerOutline(n) is { } inner)
        {
            Loop(inner);
        }
        bool fits = Mode != ToolMode.Mountain || MountainFits(s, h.X, h.Z, ShapeRadius);
        DrawLines(ref _ringVao, ref _ringVbo, [.. data], vp, fits ? new Vector4(1, 1, 1, 1) : new Vector4(1, 0.35f, 0.3f, 1), width: 1.2f);
        if (Tool == null && Mode == ToolMode.Mountain && fits && MountainPreview is { } shape)
        {
            var key = ((int)MathF.Round(h.X), (int)MathF.Round(h.Z), s.W, s.H);
            if (_previewKey != key)
            {
                _previewKey = key;
                _previewSegs = MountainMesh(s, key.Item1, key.Item2, ShapeRadius, shape);
            }
            DrawLines(ref _previewVao, ref _previewVbo, _previewSegs, vp, new Vector4(1, 0.78f, 0.35f, 0.9f), width: 1f);
        }
    }

    /// <summary>
    /// The mountain as it would rise at grid point (cx, cz): lines along x and z every few metres, each
    /// point at the ground plus the shape's height; flat ground (under half a metre added) left out.
    /// </summary>
    internal static float[] MountainMesh(WorldScene s, int cx, int cz, float reach, Func<float, float, float> shape)
    {
        const int Cells = 40;
        float step = 2 * reach / Cells;
        float[] y = new float[(Cells + 1) * (Cells + 1)];
        bool[] up = new bool[y.Length];
        for (int j = 0; j <= Cells; j++)
        {
            for (int i = 0; i <= Cells; i++)
            {
                float dx = -reach + i * step, dz = -reach + j * step;
                float add = shape(dx, dz);
                y[j * (Cells + 1) + i] = OnGround(s, cx + dx, cz + dz, 0).Y + add + 0.3f;
                up[j * (Cells + 1) + i] = add > 0.5f;
            }
        }
        var data = new List<float>();
        void Seg(int i0, int j0, int i1, int j1)
        {
            int a = j0 * (Cells + 1) + i0, b = j1 * (Cells + 1) + i1;
            if (!up[a] && !up[b])
            {
                return;
            }
            var (ax, az) = GridToView(s, cx - reach + i0 * step, cz - reach + j0 * step);
            var (bx, bz) = GridToView(s, cx - reach + i1 * step, cz - reach + j1 * step);
            data.AddRange([ax, y[a], az, bx, y[b], bz]);
        }
        for (int j = 0; j <= Cells; j++)
        {
            for (int i = 0; i <= Cells; i++)
            {
                if (i < Cells)
                {
                    Seg(i, j, i + 1, j);
                }
                if (j < Cells)
                {
                    Seg(i, j, i, j + 1);
                }
            }
        }
        return [.. data];
    }
}
