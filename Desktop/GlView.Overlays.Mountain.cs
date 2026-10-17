using System.Numerics;

namespace TerrainEditor.Desktop;

// The Mountain tool's preview: the mountain as it would rise, its lines kept while the pointer stays on one grid point.
public sealed partial class GlView
{
    /// <summary>The mountain preview's lines, kept while the pointer stays on the same grid point.</summary>
    private float[] _previewSegs = [];
    /// <summary>The grid point (and area size) the mountain preview was made for; null to make it again.</summary>
    private (int X, int Z, int W, int H)? _previewKey;

    /// <summary>Whether a Mountain click at a grid point fits in the open area (what EditSession.Mountain needs).</summary>
    internal static bool MountainFits(WorldScene s, float gx, float gz, float reach) => gx - reach >= 1 && gz - reach >= 1 && gx + reach <= s.W - 2 && gz + reach <= s.H - 2;

    /// <summary>
    /// Draws the mountain preview at the grid point under the pointer, made again when the point changes.
    /// </summary>
    /// <param name="s">The area.</param>
    /// <param name="vp">The camera's view × projection.</param>
    /// <param name="h">The grid point under the pointer.</param>
    /// <param name="shape">The mountain's height at each offset from the point.</param>
    private void DrawMountainPreview(WorldScene s, Matrix4x4 vp, (float X, float Z) h, Func<float, float, float> shape)
    {
        var key = ((int)MathF.Round(h.X), (int)MathF.Round(h.Z), s.W, s.H);
        if (_previewKey != key)
        {
            _previewKey = key;
            _previewSegs = MountainMesh(s, key.Item1, key.Item2, ShapeRadius, shape);
        }
        DrawLines(ref _previewVao, ref _previewVbo, _previewSegs, vp, new Vector4(1, 0.78f, 0.35f, 0.9f), width: 1f);
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
